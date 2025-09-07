using Core.Api.Domain;
using Core.Api.Infrastructure;

public static class CoreTestSeed
{
    public static void Run(CoreDbContext db)
    {
        db.DomainMappings.RemoveRange(db.DomainMappings);
        db.TenantDomains.RemoveRange(db.TenantDomains);
        db.Tenants.RemoveRange(db.Tenants);
        db.SaveChanges();

        var firm1 = new Tenant { Id = Guid.Parse("a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac"), Name="Firm 1", Slug="firm1", Status="active", CreatedAt=DateTime.UtcNow };
        var firm2 = new Tenant { Id = Guid.Parse("44709835-d55a-ef2a-2327-5fdca19e55d8"), Name="Firm 2", Slug="firm2", Status="active", CreatedAt=DateTime.UtcNow };

        db.Tenants.AddRange(firm1, firm2);

        db.TenantDomains.AddRange(
            new TenantDomain { Id=Guid.Parse("33333333-3333-3333-3333-333333333331"), TenantId=firm1.Id, Host="pys.local", IsDefault=true },
            new TenantDomain { Id=Guid.Parse("33333333-3333-3333-3333-333333333332"), TenantId=firm2.Id, Host="pay.local", IsDefault=true }
        );

        db.DomainMappings.AddRange(
            new DomainMapping {
                Id=Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Host="pys.local",
                Module=ModuleKind.performance,
                TenantId=null,
                PathMode=PathMode.slug,
                TenantSlug=null,
                IsActive=true
            },
            new DomainMapping {
                Id=Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Host="pay.local",
                Module=ModuleKind.compensation,
                TenantId=firm2.Id,
                PathMode=PathMode.host,
                TenantSlug=null,
                IsActive=true
            }
        );

        db.SaveChanges();
    }
}