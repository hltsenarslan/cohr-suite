using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Perf.Api.Infrastructure;
using Perf.Api.Tenancy;

namespace Perf.Api.Endpoints;

public static class TestEndpoints
{
    public static IEndpointRouteBuilder MapTestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/objectives", async (PerfDbContext db) =>
        {
            var list = await db.Objectives.OrderBy(x => x.Title).ToListAsync();
            return Results.Ok(list);
        });
        
        app.MapGet("/debug/headers", (HttpContext c) =>
        {
            var dict = c.Request.Headers.ToDictionary(k => k.Key, v => v.Value.ToString());
            return Results.Json(dict);
        });
        return app;
        
    }
}