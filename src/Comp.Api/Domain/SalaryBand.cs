public class Salary
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Employee { get; set; } = default!;
    public decimal Amount { get; set; }
    public DateOnly Period { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
}