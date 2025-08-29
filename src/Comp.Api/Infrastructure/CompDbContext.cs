using Common.Tenancy;
using Comp.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Comp.Api.Infrastructure;

public class CompDbContext : DbContext
{
    private readonly ITenantContext _tctx;
    public CompDbContext(DbContextOptions<CompDbContext> opt, ITenantContext tctx) : base(opt) => _tctx = tctx;


    public DbSet<Salary> Salaries => Set<Salary>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Salary>().HasKey(x => x.Id);
        b.Entity<Salary>().HasIndex(x => new { x.TenantId, x.EffectiveDate });
        b.Entity<Salary>().HasQueryFilter(x => x.TenantId == _tctx.TenantId);
        base.OnModelCreating(b);
    }
}