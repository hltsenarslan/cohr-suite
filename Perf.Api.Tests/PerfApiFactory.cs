using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Perf.Api.Infrastructure;
using Xunit;

public sealed class PerfWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private DbConnection? _conn;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<PerfDbContext>>();
            services.RemoveAll<PerfDbContext>();

            _conn = new SqliteConnection("DataSource=:memory:;Cache=Shared");
            _conn.Open();

            services.AddDbContext<PerfDbContext>(o => o.UseSqlite(_conn));

            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PerfDbContext>();
            db.Database.EnsureCreated();

            // ---- PERF SEED ---- (TenantId zorunlu alanları DOLDUR!)
            // Ör: db.Goals.Add(new Goal { TenantId = "...", ... });
            // db.SaveChanges();
        });
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() { if (_conn!=null) await _conn.DisposeAsync(); }
}