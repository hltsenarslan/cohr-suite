namespace Core.Api.Domain;

public sealed class UserTenant
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = default!;
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = default!;
}