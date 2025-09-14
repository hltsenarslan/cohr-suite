using Core.Api.Domain;
using Core.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Core.Api.Services;

public sealed class FeatureGate : IFeatureGate
{
    private readonly ILicenseCache _license;
    private readonly CoreDbContext _db;

    public FeatureGate(ILicenseCache license, CoreDbContext db)
    {
        _license = license;
        _db = db;
    }

    public record FeatureCheckResult(
        bool Enabled,
        int? UserQuota
    );

    public async Task<FeatureCheckResult> IsEnabledAsync(
        Guid tenantId,
        string featureKey,
        CancellationToken ct = default)
    {
        var snap = _license.Snapshot;

        if (!snap.IsCloud)
        {
            var feat = snap.Features?.FirstOrDefault(f => f.key == featureKey);
            return feat is null
                ? new FeatureCheckResult(false, null)
                : new FeatureCheckResult(true, feat.userLimit);
        }

        // Cloud: PlanFeatures üzerinden
        var feature = await _db.PlanFeatures
            .Where(f => f.FeatureKey == featureKey && f.Enabled) // key birebir eşleşsin, Enabled ise
            .Join(_db.TenantSubscriptions.Where(s => s.TenantId == tenantId && s.Status == "active"),
                f => f.PlanId,
                s => s.PlanId,
                (f, s) => f)
            .FirstOrDefaultAsync(ct);

        return feature is null
            ? new FeatureCheckResult(false, null)
            : new FeatureCheckResult(true, feature.UserQuota);
    }

    public async Task<bool> CheckQuotaAsync(Guid tenantId, string featureKey, int increment = 1,
        CancellationToken ct = default)
    {
        var snap = _license.Snapshot;

        if (!snap.IsCloud)
        {
            // OnPrem: dosya quota’sı
            var f = snap.Features.FirstOrDefault(x => x.key == featureKey);
            if (f == null) return false;
            if (f.userLimit is null) return true;
            // Şimdilik memory’de tutulan basit sayaç
            // (İstersen redis gibi distributed cache kullanırız)
            return true;
        }

        // Cloud: DB quota
        var feature = await _db.PlanFeatures.FirstOrDefaultAsync(f => f.FeatureKey == featureKey, ct);
        if (feature == null) return false;

        if (feature.Quota is null) return true;

        var nowPeriod = DateOnly.FromDateTime(DateTime.UtcNow);

        var counter = await _db.UsageCounters
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.FeatureKey == featureKey && u.Period == nowPeriod,
                ct);

        if (counter == null)
        {
            counter = new UsageCounter
            {
                TenantId = tenantId,
                FeatureKey = featureKey,
                Period = nowPeriod,
                Used = 0
            };
            _db.UsageCounters.Add(counter);
        }

        if (counter.Used + increment > feature.Quota)
            return false;

        counter.Used += increment;
        await _db.SaveChangesAsync(ct);

        return true;
    }
}