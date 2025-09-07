using Microsoft.AspNetCore.Http;

namespace Comp.Api.Tenancy;

public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    private const string TenantHeader = "X-Tenant-Id";

    public TenantContextMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx, ITenantContext tenantCtx)
    {
        var path = ctx.Request.Path.Value ?? "/";

        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/ready",  StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/metrics",StringComparison.OrdinalIgnoreCase))
        {
            await _next(ctx);
            return;
        }

        if (!ctx.Request.Headers.TryGetValue(TenantHeader, out var v) ||
            !Guid.TryParse(v.ToString(), out var tid))
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsJsonAsync(new { error = "tenant_required" });
            return;
        }

        tenantCtx.Set(tid);
        await _next(ctx);
    }
}