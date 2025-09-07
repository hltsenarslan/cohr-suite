using Comp.Api.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Comp.Api.Infrastructure;

public class CompDbContext : DbContext
{
    private readonly ITenantContext _tenant;

    public CompDbContext(DbContextOptions<CompDbContext> options, ITenantContext tenant) : base(options)
        => _tenant = tenant;

    public DbSet<Salary> Salaries => Set<Salary>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Salary>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Employee).IsRequired();
            e.Property(x => x.Amount).HasPrecision(18,2);
            e.Property(x => x.TenantId).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.Employee });
        });

        b.Entity<Salary>().HasQueryFilter(x => x.TenantId == _tenant.Id);
    }
}