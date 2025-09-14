namespace Core.Api.Domain;

public class PlanFeature
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public string? FeatureKey { get; set; } = default!; // "perf.module" | "comp.module"
    public int? UserQuota { get; set; } // null = sınırsız
    public int? MonthlyQuota { get; set; } // istersen kullanım kotası
    public bool Enabled { get; set; } = true;

    public Plan? Plan { get; set; }
    public int? Quota { get; set; }
}