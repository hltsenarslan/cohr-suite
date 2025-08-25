using Core.Api.Contracts;
using Core.Api.Domain;
using Core.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection; 

namespace Core.Api.Endpoints;

public static class AdminDomainEndpoints
{
    public static IEndpointRouteBuilder MapAdminDomainEndpoints(this IEndpointRouteBuilder app)
    {
        var config = app.ServiceProvider.GetRequiredService<IConfiguration>();

        var admin = app.MapGroup("/internal/admin").UseAdminAuthFromConfig(config);

        admin.MapPost("/domains", async (DomainCreateRequest req, CoreDbContext db) =>
        {
            var host = req.Host.Trim().ToLowerInvariant();
            var exists = await db.TenantDomains.AnyAsync(x => x.Host == host);
            if (exists) return Results.Conflict(new { error = "host_already_mapped", host });

            var tenantExists = await db.Tenants.AnyAsync(x => x.Id == req.TenantId);
            if (!tenantExists) return Results.BadRequest(new { error = "tenant_not_found", id = req.TenantId });

            if (req.IsDefault)
            {
                var oldDefaults = await db.TenantDomains
                                          .Where(x => x.TenantId == req.TenantId && x.IsDefault)
                                          .ToListAsync();
                foreach (var od in oldDefaults) od.IsDefault = false;
            }

            var d = new TenantDomain { Id = Guid.NewGuid(), TenantId = req.TenantId, Host = host, IsDefault = req.IsDefault };
            db.TenantDomains.Add(d);
            await db.SaveChangesAsync();

            Console.WriteLine($"[admin] domain added: {host} -> {req.TenantId}");
            return Results.Created($"/internal/admin/domains/{d.Id}", new { d.Id, d.TenantId, d.Host, d.IsDefault });
        });

        admin.MapGet("/domains", async (Guid? tenantId, CoreDbContext db,int page = 1, int size = 50) =>
        {
            page = Math.Max(page, 1); size = Math.Clamp(size, 1, 200);
            var q = db.TenantDomains.AsNoTracking().OrderBy(x => x.Host);
            if (tenantId is not null) q = q.Where(x => x.TenantId == tenantId) as IOrderedQueryable<TenantDomain>;
            var total = await q.CountAsync();
            var items = await q.Skip((page-1)*size).Take(size)
                               .Select(x => new { x.Id, x.TenantId, x.Host, x.IsDefault })
                               .ToListAsync();
            return Results.Ok(new { total, page, size, items });
        });

        admin.MapDelete("/domains/{id:guid}", async (Guid id, CoreDbContext db) =>
        {
            var d = await db.TenantDomains.FirstOrDefaultAsync(x => x.Id == id);
            if (d is null) return Results.NotFound(new { error = "domain_not_found", id });

            db.TenantDomains.Remove(d);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }
}