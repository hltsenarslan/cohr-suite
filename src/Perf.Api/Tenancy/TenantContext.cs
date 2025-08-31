namespace Perf.Api.Tenancy;

public sealed class TenantContext : ITenantContext
{
    public Guid Id { get; private set; }
    public bool HasValue { get; private set; }

    public void Set(Guid id)
    {
        Id = id;
        HasValue = id != Guid.Empty;
    }
}