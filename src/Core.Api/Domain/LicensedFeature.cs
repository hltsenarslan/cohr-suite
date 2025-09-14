namespace Core.Api.Domain;

public sealed class LicensedFeature
{
    public string Key { get; init; } = ""; // örn: "perf.module", "comp.module"
    public int? MaxUsers { get; init; } // null => sınırsız
}