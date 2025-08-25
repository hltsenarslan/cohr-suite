using Core.Api.Contracts;
using Core.Api.Domain;
using Core.Api.Infrastructure;
using Core.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Core.Api.Endpoints;

public static class AdminTenantEndpoints
{
    public static IEndpointRouteBuilder MapAdminTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var config = app.ServiceProvider.GetRequiredService<IConfiguration>();
        
        var admin = app.MapGroup("/internal/admin").UseAdminAuthFromConfig(config);

        admin.MapPost("/tenants", async (TenantCreateRequest req, CoreDbContext db) =>
        {
            var exists = await db.Tenants.AnyAsync(x => x.Slug == req.Slug);
            if (exists) return Results.Conflict(new { error = "slug_already_exists", slug = req.Slug });

            var t = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = req.Name.Trim(),
                Slug = req.Slug.Trim().ToLowerInvariant(),
                Status = "active"
            };

            db.Tenants.Add(t);
            await db.SaveChangesAsync();
            return Results.Created($"/internal/admin/tenants/{t.Id}", new { t.Id, t.Name, t.Slug, t.Status });
        });

        admin.MapGet("/tenants", async (CoreDbContext db, int page = 1, int size = 50) =>
        {
            page = Math.Max(page, 1); size = Math.Clamp(size, 1, 200);
            var q = db.Tenants.AsNoTracking().OrderBy(x => x.Slug);
            var total = await q.CountAsync();
            var items = await q.Skip((page-1)*size).Take(size)
                               .Select(x => new { x.Id, x.Name, x.Slug, x.Status, x.CreatedAt })
                               .ToListAsync();
            return Results.Ok(new { total, page, size, items });
        });

        admin.MapGet("/tenants/{id:guid}", async (Guid id, CoreDbContext db) =>
        {
            var t = await db.Tenants.AsNoTracking()
                        .Where(x => x.Id == id)
                        .Select(x => new { x.Id, x.Name, x.Slug, x.Status, x.CreatedAt })
                        .FirstOrDefaultAsync();

            return t is null ? Results.NotFound(new { error = "tenant_not_found", id }) : Results.Ok(t);
        });

        admin.MapPut("/tenants/{id:guid}", async (Guid id, TenantUpdateRequest req, CoreDbContext db) =>
        {
            var t = await db.Tenants.FirstOrDefaultAsync(x => x.Id == id);
            if (t is null) return Results.NotFound(new { error = "tenant_not_found", id });

            t.Name = req.Name.Trim();
            t.Status = req.Status.Trim().ToLowerInvariant();
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        admin.MapDelete("/tenants/{id:guid}", async (Guid id, CoreDbContext db) =>
        {
            var t = await db.Tenants.Include(x => x.Domains).FirstOrDefaultAsync(x => x.Id == id);
            if (t is null) return Results.NotFound(new { error = "tenant_not_found", id });

            if (t.Domains.Any())
                db.TenantDomains.RemoveRange(t.Domains);

            db.Tenants.Remove(t);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }
}