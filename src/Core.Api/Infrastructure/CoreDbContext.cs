using Core.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Core.Api.Infrastructure;

public class CoreDbContext : DbContext
{
    public CoreDbContext(DbContextOptions<CoreDbContext> options) : base(options)
    {
    }

    public DbSet<DomainMapping> DomainMappings => Set<DomainMapping>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantDomain> TenantDomains => Set<TenantDomain>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<DomainMapping>().HasIndex(x => x.Host).IsUnique();
        b.Entity<DomainMapping>().Property(x => x.Module).HasConversion<string>();
        b.Entity<DomainMapping>().Property(x => x.PathMode).HasConversion<string>();

        b.Entity<Tenant>().HasIndex(x => x.Slug).IsUnique();
        b.Entity<Tenant>().Property(x => x.Name).HasMaxLength(200);
        b.Entity<Tenant>().Property(x => x.Slug).HasMaxLength(100);
        // DB default (deterministik model)
        b.Entity<Tenant>().Property(x => x.CreatedAt)
            .HasDefaultValueSql("NOW() AT TIME ZONE 'utc'");

        b.Entity<TenantDomain>().HasIndex(x => x.Host).IsUnique();
        b.Entity<TenantDomain>()
            .HasOne(x => x.Tenant)
            .WithMany(t => t.Domains)
            .HasForeignKey(x => x.TenantId);


        var firm1 = Guid.Parse("a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac");
        var firm2 = Guid.Parse("44709835-d55a-ef2a-2327-5fdca19e55d8");
        var seedTime = new DateTime(2025, 8, 25, 0, 0, 0, DateTimeKind.Utc); // SABİT

        b.Entity<Tenant>().HasData(
            new Tenant { Id = firm1, Name = "Firm 1", Slug = "firm1", Status = "active", CreatedAt = seedTime },
            new Tenant { Id = firm2, Name = "Firm 2", Slug = "firm2", Status = "active", CreatedAt = seedTime }
        );

        b.Entity<TenantDomain>().HasData(
            new TenantDomain { Id = Guid.Parse("33333333-3333-3333-3333-333333333331"), TenantId = firm1, Host = "pys.local", IsDefault = true },
            new TenantDomain { Id = Guid.Parse("33333333-3333-3333-3333-333333333332"), TenantId = firm2, Host = "pay.local", IsDefault = true }
        );
        
        // Seed (M1 demo)
        b.Entity<DomainMapping>().HasData(
            new DomainMapping
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Host = "pys.local",
                Module = ModuleKind.performance,
                TenantId = null,
                PathMode = PathMode.slug,
                TenantSlug = null,
                IsActive = true
            },
            new DomainMapping
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Host = "pay.local",
                Module = ModuleKind.compensation,
                TenantId = null,
                PathMode = PathMode.slug,
                TenantSlug = null,
                IsActive = true
            }
        );
    }
}