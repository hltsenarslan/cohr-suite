namespace Core.Api.Domain;

public class TenantSubscription
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid PlanId { get; set; } // <- net FK
    public Plan Plan { get; set; } = default!; // <- navigation

    public string Status { get; set; } = "active";
    public DateOnly PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
}