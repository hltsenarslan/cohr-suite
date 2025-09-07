using Microsoft.EntityFrameworkCore;
using Perf.Api.Tenancy;

namespace Perf.Api.Infrastructure;

public class PerfDbContext : DbContext
{
    private readonly ITenantContext _tenant;
    public PerfDbContext(DbContextOptions<PerfDbContext> opts, ITenantContext tenant) : base(opts)
    {
        _tenant = tenant;
    }

    public DbSet<Objective> Objectives => Set<Objective>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Objective>().HasQueryFilter(o => !_tenant.HasValue || o.TenantId == _tenant.Id);
        base.OnModelCreating(b);
    }
}