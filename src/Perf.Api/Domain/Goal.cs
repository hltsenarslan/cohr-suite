namespace Perf.Api.Domain;

public class Goal
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = null!;
    public string Title { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}