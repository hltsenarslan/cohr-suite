using Core.Api.Domain;
using Core.Api.Infrastructure;
using Core.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Core.Api.Endpoints;

public static class AdminSubscriptionEndpoints
{
    public sealed record AssignReq(
        Guid TenantId,
        Guid PlanId,
        DateOnly? StartsAtUtc,
        DateOnly? EndsAtUtc,
        string Status // "active" | "pending" | "canceled"
    );

    public sealed record AssignRes(
        Guid Id,
        Guid TenantId,
        Guid PlanId,
        DateOnly StartsAtUtc,
        DateOnly? EndsAtUtc,
        string Status
    );

    // ---- Yeni DTO'lar ----
    public sealed record FeatureStatusDto(
        string featureKey,
        int? userLimit,
        int activeUsers,
        int? remaining,
        bool enabled
    );

    public sealed record SubscriptionWithFeaturesRes(
        Guid Id,
        Guid TenantId,
        Guid PlanId,
        DateOnly StartsAtUtc,
        DateOnly? EndsAtUtc,
        string Status,
        string PlanName,
        IReadOnlyList<FeatureStatusDto> Features
    );

    // On-prem için "etkin" özet
    public sealed record EffectiveFeaturesRes(
        string Mode, // onprem|cloud
        Guid TenantId,
        IReadOnlyList<FeatureStatusDto> Features
    );

    public sealed record FeatureView(string FeatureKey, int? UserQuota, int ActiveUsers, int? Remaining);

    public sealed record SubscriptionView(
        Guid Id,
        Guid TenantId,
        Guid PlanId,
        string PlanName,
        DateOnly PeriodStart,
        DateOnly? PeriodEnd,
        string Status,
        List<FeatureView> Features
    );

    public static IEndpointRouteBuilder MapAdminSubscriptionEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/admin/subscriptions")
            .RequireAuthorization("RequireAdmin");

        // Plan atama / güncelle (overlap engeller, çakışma varsa 409)
        g.MapPost("/assign", async (AssignReq req, CoreDbContext db, ILicenseCache lic, CancellationToken ct) =>
        {
            if (lic.Snapshot.Mode.Equals("onprem", StringComparison.OrdinalIgnoreCase))
                return Results.StatusCode(StatusCodes.Status409Conflict);

            var starts = (req.StartsAtUtc ?? DateOnly.FromDateTime(DateTime.UtcNow));
            DateOnly? ends = req.EndsAtUtc;

            var existsTenant = await db.Tenants.AnyAsync(t => t.Id == req.TenantId, ct);
            var existsPlan = await db.Plans.AnyAsync(p => p.Id == req.PlanId, ct);
            if (!existsTenant || !existsPlan)
                return Results.NotFound(new { error = "not_found", tenant = !existsTenant, plan = !existsPlan });

            if (ends is DateOnly e && e <= starts)
                return Results.BadRequest(new { error = "invalid_period" });

            var overlaps = await db.TenantSubscriptions
                .Where(s => s.TenantId == req.TenantId &&
                            s.Status != "canceled" &&
                            (s.PeriodEnd == null || s.PeriodEnd > starts) &&
                            (ends == null || s.PeriodStart < ends))
                .AnyAsync(ct);

            if (overlaps)
                return Results.Conflict(new { error = "period_overlap" });

            var sub = new TenantSubscription
            {
                Id = Guid.NewGuid(),
                TenantId = req.TenantId,
                PlanId = req.PlanId,
                PeriodStart = starts,
                PeriodEnd = ends,
                Status = string.IsNullOrWhiteSpace(req.Status) ? "active" : req.Status.ToLowerInvariant(),
            };

            db.TenantSubscriptions.Add(sub);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new AssignRes(sub.Id, sub.TenantId, sub.PlanId, sub.PeriodStart, sub.PeriodEnd,
                sub.Status));
        });

        // Tenant’ın abonelikleri + plan özellikleri + anlık kullanıcı durumu
        g.MapGet("/{tenantId:guid}", async (Guid tenantId, CoreDbContext db, ILicenseCache lic, CancellationToken ct) =>
        {
            // Aktif kullanıcı sayısı (tenant bazlı)
            var activeUsers = await db.UserTenants
                .Where(ut => ut.TenantId == tenantId)
                .Join(db.Users, ut => ut.UserId, u => u.Id, (ut, u) => u)
                .CountAsync(u => u.IsActive, ct);

            // ON-PREM: DB yerine lisans dosyasındaki özellikleri raporla
            if (!lic.Snapshot.IsCloud)
            {
                var feats = lic.Snapshot.Features
                    .Select(f =>
                    {
                        int? remaining = f.userLimit is int lim ? Math.Max(lim - activeUsers, 0) : null;
                        return new FeatureStatusDto(
                            featureKey: f.key,
                            userLimit: f.userLimit,
                            activeUsers: activeUsers,
                            remaining: remaining,
                            enabled: true
                        );
                    })
                    .ToList();

                // On-prem’de “abonelik listesi” kavramı yok; sadece etkin özellikleri döndür.
                var onprem = new EffectiveFeaturesRes(
                    Mode: "onprem",
                    TenantId: tenantId,
                    Features: feats
                );
                return Results.Ok(onprem);
            }

            // CLOUD: Tüm abonelikleri DTO şeklinde projekte et
            var subs = await db.TenantSubscriptions
                .Where(s => s.TenantId == tenantId)
                .Include(s => s.Plan)
                .ThenInclude(p => p.Features)
                .OrderByDescending(s => s.PeriodStart)
                .AsNoTracking()
                .Select(s => new SubscriptionView(
                    s.Id, s.TenantId, s.PlanId, s.Plan.Name,
                    s.PeriodStart, s.PeriodEnd, s.Status,
                    s.Plan.Features
                        .Where(pf => pf.Enabled)
                        .Select(pf => new FeatureView(
                            pf.FeatureKey!, pf.UserQuota, activeUsers,
                            pf.UserQuota == null ? null : Math.Max(0, pf.UserQuota.Value - activeUsers)
                        ))
                        .ToList()
                ))
                .ToListAsync(ct);

            var result = subs.Select(s =>
            {
                var featDtos = s.Features.Select(f =>
                {
                    int? remaining = f.UserQuota is int lim ? Math.Max(lim - activeUsers, 0) : (int?)null;
                    return new FeatureStatusDto(
                        featureKey: f.FeatureKey,
                        userLimit: f.UserQuota,
                        activeUsers: activeUsers,
                        remaining: remaining,
                        enabled: true
                    );
                }).ToList();

                return new SubscriptionWithFeaturesRes(
                    Id: s.Id,
                    TenantId: s.TenantId,
                    PlanId: s.PlanId,
                    StartsAtUtc: s.PeriodStart,
                    EndsAtUtc: s.PeriodEnd,
                    Status: s.Status,
                    PlanName: s.PlanName,
                    Features: featDtos
                );
            }).ToList();

            return Results.Ok(result);
        });

        // Mevcut bir aboneliği iptal et (şimdi)
        g.MapPost("/{id:guid}/cancel", async (Guid id, CoreDbContext db, CancellationToken ct) =>
        {
            var s = await db.TenantSubscriptions.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (s is null) return Results.NotFound();

            s.Status = "canceled";
            s.PeriodEnd ??= DateOnly.FromDateTime(DateTime.UtcNow);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        return app;
    }
}