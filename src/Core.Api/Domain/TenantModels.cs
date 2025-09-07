using Microsoft.EntityFrameworkCore;

namespace Core.Api.Domain;

public enum ModuleKind { performance, compensation }

public class DomainMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Host { get; set; } = default!;
    public ModuleKind Module { get; set; }
    public Guid? TenantId { get; set; }
    public PathMode PathMode { get; set; } = PathMode.slug;
    public string? TenantSlug { get; set; }
    public bool IsActive { get; set; } = true;
}