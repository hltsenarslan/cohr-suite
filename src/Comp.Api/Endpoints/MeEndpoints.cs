using Comp.Api.Infrastructure;
using Comp.Api.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Comp.Api.Endpoints;

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMe(this IEndpointRouteBuilder app)
    {
        // GET /me  (tenant zorunlu, filter çalışır)
        app.MapGet("/me", async (CompDbContext db, ITenantContext t) =>
        {
            var last3 = await db.Salaries
                .OrderByDescending(x => x.Period)
                .Take(3)
                .Select(x => new { x.Employee, x.Amount, x.Period })
                .ToListAsync();

            return Results.Ok(new { tenant = t.Id, last3 });
        });

        return app;
    }
}