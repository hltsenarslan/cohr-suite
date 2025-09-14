// comp.Api/Enforcement/compFeatureMiddleware.cs

using Comp.Api.CoreClient;
using Comp.Api.Tenancy;

namespace Comp.Api.Enforcement;

public sealed class CompFeatureMiddleware(RequestDelegate next)
{
    public async Task Invoke(
        HttpContext ctx,
        ITenantContext tenantCtx,
        ICoreFeatureClient core)
    {
        var p = ctx.Request.Path.Value?.ToLowerInvariant() ?? "/";

        if (!Guid.TryParse(ctx.Request.Headers["X-Tenant-Id"].FirstOrDefault(), out var tenantId))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsJsonAsync(new { error = "tenant_missing" });
            return;
        }

        var decision = await core.EnforceAsync(tenantId, "comp", ctx.RequestAborted);

        if (!decision.enabled)
        {
            ctx.Response.StatusCode = 403;
            await ctx.Response.WriteAsJsonAsync(new { error = "feature_not_enabled", feature = "comp" });
            return;
        }

        if (!decision.allowed && decision.error == "quota_exceeded")
        {
            ctx.Response.StatusCode = 402; // quota
            await ctx.Response.WriteAsJsonAsync(new
            {
                error = "quota_exceeded",
                feature = "comp",
                limit = decision.userLimit,
                current = decision.activeUsers
            });
            return;
        }

        await next(ctx);
    }
}