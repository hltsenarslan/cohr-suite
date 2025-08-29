using Common.Tenancy;
using Comp.Api.Infrastructure;

namespace Comp.Api.Endpoints;

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapCompMeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me", (ITenantContext tctx) =>
            Results.Ok(new { service = "comp", tenantId = tctx.TenantId }));

        app.MapGet("/{slug}/me", (string slug, ITenantContext tctx, HttpContext ctx) =>
            Results.Ok(new { service = "comp", slug, tenantId = tctx.TenantId, host = ctx.Request.Headers["X-Host"].ToString() }));

        return app;
    }
}