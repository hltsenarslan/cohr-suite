using Comp.Api.Infrastructure;
using Comp.Api.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Comp.Api.Endpoints;

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMe(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me", (ITenantContext tctx) =>
        {
            return Results.Ok(new { service = "perf", tenantId = tctx.Id });
        });

        app.MapGet("/{slug}/me", (string slug, ITenantContext tctx, HttpContext ctx) =>
        {
            return Results.Ok(new { service = "perf", slug, tenantId = tctx.Id, host = ctx.Request.Headers["X-Host"].ToString() });
        });

        return app;
    }
}