using System.ComponentModel.DataAnnotations;

namespace Core.Api.Contracts;

public record TenantCreateRequest(
    [Required, MaxLength(200)] string Name,
    [Required, MaxLength(100)] string Slug
);

public record TenantUpdateRequest(
    [Required, MaxLength(200)] string Name,
    [Required, MaxLength(50)]  string Status
);

public record DomainCreateRequest(
    [Required] Guid TenantId,
    [Required, MaxLength(255)] string Host,
    bool IsDefault = true
);