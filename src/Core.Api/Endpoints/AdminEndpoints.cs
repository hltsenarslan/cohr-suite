using Core.Api.Domain;
using Core.Api.Infrastructure;
using Core.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Core.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdmin(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/admin").RequireAuthorization("RequireAdmin");

        // ---- Plans CRUD ----
        g.MapGet("/plans", async (CoreDbContext db) =>
            Results.Ok(await db.Plans.Include(p => p.Features).ToListAsync()));

        g.MapPost("/plans", async (CoreDbContext db, Plan p) =>
        {
            p.Id = p.Id == Guid.Empty ? Guid.NewGuid() : p.Id;
            db.Plans.Add(p);
            await db.SaveChangesAsync();
            return Results.Created($"/api/core/admin/plans/{p.Id}", p);
        });

        g.MapPut("/plans/{id:guid}", async (CoreDbContext db, Guid id, Plan dto) =>
        {
            var p = await db.Plans.FindAsync(id);
            if (p is null) return Results.NotFound();
            p.Name = dto.Name;
            p.Description = dto.Description;
            p.IsActive = dto.IsActive;
            await db.SaveChangesAsync();
            return Results.Ok(p);
        });

        g.MapDelete("/plans/{id:guid}", async (CoreDbContext db, Guid id) =>
        {
            var p = await db.Plans.FindAsync(id);
            if (p is null) return Results.NotFound();
            db.Plans.Remove(p);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ---- PlanFeatures ----
        g.MapPost("/plans/{planId:guid}/features", async (CoreDbContext db, Guid planId, PlanFeature f) =>
        {
            if (await db.Plans.AnyAsync(x => x.Id == planId) is false) return Results.NotFound("plan_not_found");
            f.Id = f.Id == Guid.Empty ? Guid.NewGuid() : f.Id;
            f.PlanId = planId;
            db.PlanFeatures.Add(f);
            await db.SaveChangesAsync();
            return Results.Created($"/api/core/admin/plans/{planId}/features/{f.Id}", f);
        });

        g.MapPut("/plans/{planId:guid}/features/{id:guid}",
            async (CoreDbContext db, Guid planId, Guid id, PlanFeature dto) =>
            {
                var f = await db.PlanFeatures.FirstOrDefaultAsync(x => x.Id == id && x.PlanId == planId);
                if (f is null) return Results.NotFound();
                f.FeatureKey = dto.FeatureKey;
                f.UserQuota = dto.UserQuota;
                f.MonthlyQuota = dto.MonthlyQuota;
                f.Enabled = dto.Enabled;
                await db.SaveChangesAsync();
                return Results.Ok(f);
            });

        g.MapDelete("/plans/{planId:guid}/features/{id:guid}", async (CoreDbContext db, Guid planId, Guid id) =>
        {
            var f = await db.PlanFeatures.FirstOrDefaultAsync(x => x.Id == id && x.PlanId == planId);
            if (f is null) return Results.NotFound();
            db.PlanFeatures.Remove(f);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ---- TenantSubscriptions ----
        g.MapGet("/tenant-subscriptions/{tenantId:guid}", async (CoreDbContext db, Guid tenantId) =>
            Results.Ok(await db.TenantSubscriptions.Where(x => x.TenantId == tenantId).ToListAsync()));

        g.MapPost("/tenant-subscriptions", async (CoreDbContext db, TenantSubscription s) =>
        {
            s.Id = s.Id == Guid.Empty ? Guid.NewGuid() : s.Id;
            db.TenantSubscriptions.Add(s);
            await db.SaveChangesAsync();
            return Results.Created($"/api/core/admin/tenant-subscriptions/{s.Id}", s);
        });

        g.MapPut("/tenant-subscriptions/{id:guid}", async (CoreDbContext db, Guid id, TenantSubscription dto) =>
        {
            var s = await db.TenantSubscriptions.FindAsync(id);
            if (s is null) return Results.NotFound();
            s.PlanId = dto.PlanId;
            s.Status = dto.Status;
            s.PeriodStart = dto.PeriodStart;
            s.PeriodEnd = dto.PeriodEnd;
            await db.SaveChangesAsync();
            return Results.Ok(s);
        });

        g.MapDelete("/tenant-subscriptions/{id:guid}", async (CoreDbContext db, Guid id) =>
        {
            var s = await db.TenantSubscriptions.FindAsync(id);
            if (s is null) return Results.NotFound();
            db.TenantSubscriptions.Remove(s);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ---- Usage report (basit) ----
        g.MapGet("/usage/{tenantId:guid}", async (CoreDbContext db, Guid tenantId) =>
        {
            var list = await db.UsageCounters.Where(x => x.TenantId == tenantId)
                .OrderByDescending(x => x.PeriodKey)
                .ToListAsync();
            return Results.Ok(list);
        });

        // ---- License mgmt ----
        g.MapPost("/license/reload", async (ILicenseCache lic) =>
        {
            await lic.ReloadAsync();
            return Results.Ok(new { lic.Mode, lic.Fingerprint, lic.LoadedAt });
        });

        g.MapGet("/license/status", (ILicenseCache lic) =>
            Results.Ok(new { lic.Mode, lic.Fingerprint, lic.LoadedAt }));

        return app;
    }
}