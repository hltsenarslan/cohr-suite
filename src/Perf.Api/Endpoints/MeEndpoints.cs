using Perf.Api.Infrastructure;
using Perf.Api.Tenancy;

namespace Perf.Api.Endpoints;

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapPerfMeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me", (ITenantContext tctx) =>
        {
            return Results.Ok(new { service = "perf", tenantId = tctx.Id });
        });

        app.MapGet("/{slug}/me", (string slug, ITenantContext tctx, HttpContext ctx) =>
        {
            return Results.Ok(new { service = "perf", slug, tenantId = tctx.Id, host = ctx.Request.Headers["X-Host"].ToString() });
        }).RequireAuthorization("RequireAdmin");

        return app;
    }
}