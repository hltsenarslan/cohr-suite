// Perf.Api/CoreClient/ICoreFeatureClient.cs  (Comp.Api için de kopyalayın)

namespace Comp.Api.CoreClient;

public interface ICoreFeatureClient
{
    Task<CoreFeatureDecision> EnforceAsync(Guid tenantId, string feature, CancellationToken ct = default);
}

public record CoreFeatureDecision(bool enabled, int? userLimit, int activeUsers, bool allowed, string? error);

public sealed class CoreFeatureClient(HttpClient http) : ICoreFeatureClient
{
    private readonly HttpClient _http = http;
    public async Task<CoreFeatureDecision> EnforceAsync(Guid tenantId, string feature, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync("/internal/feature/enforce",
            new { tenantId, feature }, ct);
        res.EnsureSuccessStatusCode();
        var dto = await res.Content.ReadFromJsonAsync<CoreFeatureDecision>(cancellationToken: ct)
                  ?? throw new InvalidOperationException("empty core response");
        return dto;
    }
}