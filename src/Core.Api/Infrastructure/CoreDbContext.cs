using Core.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Core.Api.Infrastructure;

public class CoreDbContext : DbContext
{
    public CoreDbContext(DbContextOptions<CoreDbContext> options) : base(options) { }

    public DbSet<DomainMapping> DomainMappings => Set<DomainMapping>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<DomainMapping>().HasIndex(x => x.Host).IsUnique();
        b.Entity<DomainMapping>().Property(x => x.Module).HasConversion<string>();
        b.Entity<DomainMapping>().Property(x => x.PathMode).HasConversion<string>();

        // Seed (M1 demo)
        b.Entity<DomainMapping>().HasData(
            new DomainMapping {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Host = "pys.local",
                Module = ModuleKind.performance,
                TenantId = null,
                PathMode = PathMode.slug,
                TenantSlug = null,
                IsActive = true
            },
            new DomainMapping {
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