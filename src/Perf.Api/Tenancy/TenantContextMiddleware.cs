using Microsoft.AspNetCore.Http;

namespace Perf.Api.Tenancy;

public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    public TenantContextMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx, ITenantContext tenant)
    {
        var path = ctx.Request.Path.Value ?? "/";
        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/ready",  StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/metrics",StringComparison.OrdinalIgnoreCase))
        {
            await _next(ctx);
            return;
        }

        var raw = ctx.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw) || !Guid.TryParse(raw, out var tid) || tid == Guid.Empty)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsJsonAsync(new { error = "tenant_required" });
            return;
        }

        tenant.Set(tid);
        await _next(ctx);
    }
}