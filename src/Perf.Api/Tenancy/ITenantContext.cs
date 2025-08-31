namespace Perf.Api.Tenancy;

public interface ITenantContext
{
    Guid Id { get; }
    bool HasValue { get; }
    void Set(Guid id);
}