namespace Core.Api.Domain;

public class UsageCounter
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string FeatureKey { get; set; } = default!;
    public string PeriodKey { get; set; } = default!; // "2025-09" gibi
    public int Used { get; set; }
    public DateOnly Period { get; set; }
}