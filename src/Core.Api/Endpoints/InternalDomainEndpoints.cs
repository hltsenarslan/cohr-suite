using Core.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Core.Api.Endpoints;

public static class InternalDomainEndpoints
{
    public static IEndpointRouteBuilder MapInternalDomainEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/internal/domains/{host}", async (string host, CoreDbContext db) =>
        {
            var map = await db.DomainMappings.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Host == host && x.IsActive);
            return map is null
                ? Results.NotFound(new { error = "domain_not_mapped", host })
                : Results.Ok(map);
        });

        return app;
    }
}