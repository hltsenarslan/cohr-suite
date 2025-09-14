// Perf.Api/Enforcement/PerfFeatureMiddleware.cs

using Perf.Api.CoreClient;
using Perf.Api.Tenancy;

namespace Perf.Api.Enforcement;

public sealed class PerfFeatureMiddleware(RequestDelegate next)
{
    public async Task Invoke(
        HttpContext ctx,
        ITenantContext tenantCtx,
        ICoreFeatureClient core)
    {
        if (!Guid.TryParse(ctx.Request.Headers["X-Tenant-Id"].FirstOrDefault(), out var tenantId))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsJsonAsync(new { error = "tenant_missing" });
            return;
        }

        var decision = await core.EnforceAsync(tenantId, "perf", ctx.RequestAborted);

        if (!decision.enabled)
        {
            ctx.Response.StatusCode = 403;
            await ctx.Response.WriteAsJsonAsync(new { error = "feature_not_enabled", feature = "perf" });
            return;
        }

        if (!decision.allowed && decision.error == "quota_exceeded")
        {
            ctx.Response.StatusCode = 402; // quota
            await ctx.Response.WriteAsJsonAsync(new
            {
                error = "quota_exceeded",
                feature = "perf",
                limit = decision.userLimit,
                current = decision.activeUsers
            });
            return;
        }

        await next(ctx);
    }
}