namespace Comp.Api.Tenancy;

public interface ITenantContext
{
    Guid Id { get; }
    bool IsSet { get; }
    void Set(Guid id);
}