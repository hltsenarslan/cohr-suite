using Core.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Core.Api.Endpoints;

public static class InternalTenantEndpoints
{
    public static IEndpointRouteBuilder MapInternalTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/internal/tenants");

        grp.MapGet("/resolve/{slug}", async (string slug, CoreDbContext db) =>
        {
            var s = slug.Trim().ToLowerInvariant();

            var id = await db.Tenants.AsNoTracking()
                .Where(t => t.Slug == s)
                .Select(t => t.Id)
                .FirstOrDefaultAsync();

            if (id == Guid.Empty)
                return Results.BadRequest(new { error = "tenant_not_found", slug = s });

            return Results.Ok(new { tenantId = id });
        });

        grp.MapGet("/by-host/{host}", async (string host, CoreDbContext db) =>
        {
            var h = host.Trim().ToLowerInvariant();

            var id = await db.TenantDomains.AsNoTracking()
                .Where(d => d.Host == h)
                .OrderByDescending(d => d.IsDefault)
                .Select(d => d.TenantId)
                .FirstOrDefaultAsync();

            if (id == Guid.Empty)
                return Results.BadRequest(new { error = "tenant_not_found_for_host", host = h });

            return Results.Ok(new { tenantId = id });
        });

        return app;
    }
}