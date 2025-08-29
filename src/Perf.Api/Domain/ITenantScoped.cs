namespace Perf.Api.Domain;

public interface ITenantScoped
{
    Guid TenantId { get; set; }
}