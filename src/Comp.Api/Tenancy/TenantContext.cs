using Comp.Api.Infrastructure;

namespace Comp.Api.Tenancy;

public sealed class TenantContext : ITenantContext
{
    public Guid Id { get; private set; }
    public bool IsSet { get; private set; }

    public void Set(Guid id)
    {
        Id = id;
        IsSet = true;
    }
}