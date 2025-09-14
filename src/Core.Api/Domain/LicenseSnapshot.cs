namespace Core.Api.Domain;

public sealed class LicenseSnapshot
{
    public string Mode { get; init; } = "cloud";
    public string Fingerprint { get; init; } = "";
    public IReadOnlyCollection<FeatureSpec> Features { get; init; } = Array.Empty<FeatureSpec>();
    public IReadOnlyDictionary<string, int> UserLimits { get; init; } = new Dictionary<string, int>();
    public DateTime LoadedAt { get; init; } = DateTime.UtcNow;
    public DateTime? NotBefore { get; init; }
    public DateTime? NotAfter { get; init; }

    public bool IsCloud
    {
        get { return Mode == "cloud" ? true : false; }
    }
}

public record FeatureSpec(string key, int? userLimit);