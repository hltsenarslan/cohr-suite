using Core.Api.Domain;
using Core.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Core.Api.Endpoints;

public static class InternalDomainEndpoints
{
    public static IEndpointRouteBuilder MapInternalDomainEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/internal/domains/{host}", async (string host, CoreDbContext db) =>
        {
            var h = host.Trim().ToLowerInvariant();

            var map = await db.DomainMappings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Host == h && x.IsActive);

            if (map is null)
                return Results.NotFound(new { error = "domain_not_found", host = h });

            var dto = new
            {
                id         = map.Id,
                host       = map.Host,
                module     = (int)map.Module,
                tenantId   = map.TenantId,
                pathMode   = map.PathMode == PathMode.slug ? 1 : 0,
                tenantSlug = map.TenantSlug,
                isActive   = map.IsActive
            };

            return Results.Json(dto);
        });

        return app;
    }
}