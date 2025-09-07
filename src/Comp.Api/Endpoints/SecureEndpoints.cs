using System.Security.Claims;
using Comp.Api.Infrastructure;
using Comp.Api.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Comp.Api.Endpoints;

public static class SecureEndpoints
{
    public static IEndpointRouteBuilder MapSecureEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/secure/ping", (ClaimsPrincipal u) =>
            {
                var sub = u.FindFirstValue(ClaimTypes.NameIdentifier) ?? u.FindFirstValue("sub");
                var roles = u.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray();
                return Results.Ok(new { ok = true, sub, roles });
            })
            .RequireAuthorization();

        app.MapGet("/secure/admin-only", () => Results.Ok(new { ok = true }))
            .RequireAuthorization(policy => policy.RequireRole("admin"));

        return app;
    }
}