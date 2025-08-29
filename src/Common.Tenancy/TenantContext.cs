using System.Threading;

namespace Common.Tenancy;

public sealed class TenantContext : ITenantContext
{
    private static readonly AsyncLocal<string?> _current = new();

    public string? TenantId => _current.Value;

    public void Set(string tenantId) => _current.Value = tenantId;
}