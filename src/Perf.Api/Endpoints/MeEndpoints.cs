using Common.Tenancy;
using Perf.Api.Infrastructure;

namespace Perf.Api.Endpoints;

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapPerfMeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me", (ITenantContext tctx) =>
        {
            // Demo response
            return Results.Ok(new { service = "perf", tenantId = tctx.TenantId });
        });

        // Örn: slug’lı demo endpoint sadece test için
        app.MapGet("/{slug}/me", (string slug, ITenantContext tctx, HttpContext ctx) =>
        {
            // (Gateway slug’tan tenant’ı çözüp header’a koyuyor)
            return Results.Ok(new { service = "perf", slug, tenantId = tctx.TenantId, host = ctx.Request.Headers["X-Host"].ToString() });
        });

        return app;
    }
}