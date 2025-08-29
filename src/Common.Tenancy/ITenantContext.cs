namespace Common.Tenancy;

public interface ITenantContext
{
    string? TenantId { get; }
    void Set(string tenantId);
}