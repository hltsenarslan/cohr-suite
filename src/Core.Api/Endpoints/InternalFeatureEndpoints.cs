// Core.Api/Endpoints/InternalFeatureEndpoints.cs

using Core.Api.Infrastructure;
using Core.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Core.Api.Endpoints;

public static class InternalFeatureEndpoints
{
    public record EnforceReq(Guid tenantId, string feature);

    public record EnforceRes(
        bool enabled,
        int? userLimit,
        int activeUsers,
        bool allowed, // = enabled && (userLimit == null || activeUsers <= userLimit)
        string? error // "feature_not_enabled" | "quota_exceeded" | null
    );

    public static IEndpointRouteBuilder MapInternalFeatureEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/internal/feature");

        g.MapPost("/enforce", async (
            EnforceReq req,
            IFeatureGate gate,
            CoreDbContext db,
            CancellationToken ct) =>
        {
            // 1) feature enable + limit (license veya subscription üzerinden)
            var res = await gate.IsEnabledAsync(req.tenantId, req.feature, ct);
            if (!res.Enabled)
                return Results.Ok(new EnforceRes(false, 0, 0, false, "feature_not_enabled"));

            // 2) aktif kullanıcı sayısı (tenant için)
            var activeUsers = await db.UserTenants
                .Where(ut => ut.TenantId == req.tenantId)
                .Join(db.Users, ut => ut.UserId, u => u.Id, (ut, u) => u)
                .CountAsync(u => u.IsActive, ct);

            // 3) karar
            // if (limit is int lim && lim > 0 && activeUsers > lim)
            //     return Results.Ok(new EnforceRes(true, limit, activeUsers, false, "quota_exceeded"));

            return Results.Ok(new EnforceRes(true, 0, activeUsers, true, null));
        });

        g.MapGet("/check",
            async (Guid tenantId, string featureKey, IFeatureGate gate, CancellationToken ct) =>
            {
                var res = await gate.IsEnabledAsync(tenantId, featureKey, ct);
                return Results.Ok(new { enabled = res.Enabled, userQuota = res.UserQuota });
            });

        return app;
    }
}