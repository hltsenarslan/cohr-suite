using Common.Tenancy;
using Microsoft.EntityFrameworkCore;
using Perf.Api.Domain;
using Perf.Api.Infrastructure;

namespace Perf.Api.Infrastructure;

public class PerfDbContext : DbContext
{
    private readonly ITenantContext _tctx;
    public PerfDbContext(DbContextOptions<PerfDbContext> opt, ITenantContext tctx) : base(opt) => _tctx = tctx;


    public DbSet<Goal> Goals => Set<Goal>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Goal>().HasKey(x => x.Id);
        b.Entity<Goal>().HasIndex(x => new { x.TenantId, x.CreatedAt });

        // GLOBAL QUERY FILTER
        b.Entity<Goal>().HasQueryFilter(x => x.TenantId == _tctx.TenantId);

        base.OnModelCreating(b);
    }
}