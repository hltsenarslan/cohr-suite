using Core.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Core.Api.Endpoints;

public static class InternalTenantEndpoints
{
    public static IEndpointRouteBuilder MapInternalTenantEndpoints(this IEndpointRouteBuilder app)
    {
        // slug -> tenantId
        app.MapGet("/internal/tenants/resolve/{slug}", async (string slug, CoreDbContext db) =>
        {
            var t = await db.Tenants.AsNoTracking()
                .Where(x => x.Slug == slug)
                .Select(x => new { tenantId = x.Id })
                .FirstOrDefaultAsync();

            return t is null
                ? Results.NotFound(new { error = "tenant_not_found", slug })
                : Results.Ok(t);
        });

        // opsiyonel: host -> tenantId
        app.MapGet("/internal/tenants/by-host/{host}", async (string host, CoreDbContext db) =>
        {
            var t = await db.TenantDomains.AsNoTracking()
                .Where(d => d.Host == host)
                .Select(d => new { tenantId = d.TenantId })
                .FirstOrDefaultAsync();

            return t is null
                ? Results.NotFound(new { error = "domain_not_mapped", host })
                : Results.Ok(t);
        });

        return app;
    }
}