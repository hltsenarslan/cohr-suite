using Microsoft.EntityFrameworkCore;

namespace Core.Api.Domain;

public enum ModuleKind { performance, compensation }

public class DomainMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Host { get; set; } = default!;                 // pys.co-hr.com.tr, pys.firm1.com
    public ModuleKind Module { get; set; }                       // performance | compensation
    public Guid? TenantId { get; set; }                          // özel domain -> sabit tenant
    public PathMode PathMode { get; set; } = PathMode.slug;      // default hostlarda slug
    public string? TenantSlug { get; set; }                      // firm1 gibi
    public bool IsActive { get; set; } = true;
}