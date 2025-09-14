namespace Core.Api.Services;

public interface IFeatureGate
{
    Task<FeatureGate.FeatureCheckResult> IsEnabledAsync(Guid tenantId, string featureKey, CancellationToken ct = default);

    /// <summary>
    /// Quota’yı kontrol eder ve gerekiyorsa usage counter’ı artırır.
    /// Quota yoksa true döner.
    /// Aşılırsa false döner.
    /// </summary>
    Task<bool> CheckQuotaAsync(Guid tenantId, string featureKey, int increment = 1, CancellationToken ct = default);
}