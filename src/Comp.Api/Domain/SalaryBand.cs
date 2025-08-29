namespace Comp.Api.Domain;

public class Salary
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = null!;
    public string Employee { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime EffectiveDate { get; set; }
}