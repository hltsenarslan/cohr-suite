using Perf.Api.Infrastructure;

public static class PerfTestSeed
{
    public static void Run(PerfDbContext db)
    {
        db.SaveChanges();
    }
}