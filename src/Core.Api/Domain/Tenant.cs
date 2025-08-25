namespace Core.Api.Domain;

public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; } // <-- initializer YOK
    public ICollection<TenantDomain> Domains { get; set; } = new List<TenantDomain>();
}

public class TenantDomain
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Host { get; set; } = default!;
    public bool IsDefault { get; set; } = true;
    public Tenant? Tenant { get; set; }
}