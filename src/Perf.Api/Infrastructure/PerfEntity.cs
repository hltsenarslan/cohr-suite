namespace Perf.Api.Infrastructure;

public abstract class PerfEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
}