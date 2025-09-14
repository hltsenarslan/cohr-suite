namespace Core.Api.Domain;

public class Plan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public string Description { get; set; }
    public List<PlanFeature> Features { get; set; } = new();
    public List<TenantSubscription> Subscriptions { get; set; } = new(); // <- önemli
}