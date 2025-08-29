using Perf.Api.Infrastructure;

public static class PerfTestSeed
{
    public static void Run(PerfDbContext db)
    {
        // Örnek: zorunlu alanları doldur (TenantId, Name vs.)
        // db.Database.ExecuteSqlRaw("DELETE FROM ..."); // gerekirse

        // ... seed’lerin
        db.SaveChanges();
    }
}