using Microsoft.AspNetCore.Http;

namespace Common.Tenancy;

public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx, ITenantContext tenantCtx)
    {
        var path = ctx.Request.Path.Value ?? "/";

        // Health/ready/metrics BYPASS
        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/ready",  StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/metrics",StringComparison.OrdinalIgnoreCase))
        {
            await _next(ctx);
            return;
        }

        // X-Tenant-Id zorunlu
        var tenantId = ctx.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsJsonAsync(new { error = "tenant_required" });
            return;
        }

        tenantCtx.Set(tenantId);
        await _next(ctx);
    }
}