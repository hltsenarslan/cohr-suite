using System.Data.Common;
using Comp.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

public sealed class CompWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private DbConnection? _conn;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<CompDbContext>>();
            services.RemoveAll<CompDbContext>();

            _conn = new SqliteConnection("DataSource=:memory:;Cache=Shared");
            _conn.Open();

            services.AddDbContext<CompDbContext>(o => o.UseSqlite(_conn));

            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CompDbContext>();
            db.Database.EnsureCreated();

            // ---- COMP SEED ----
            // Hatan: "NOT NULL constraint failed: Salaries.Employee"
            // => Seed’te Employee alanını ve diğer zorunluları mutlaka doldur:
            //
            // db.Salaries.Add(new Salary {
            //     TenantId = "a0cb8...", Employee = "e1",
            //     Amount = 1000m, Period = "2025-08"
            // });
            // db.SaveChanges();
            
            CompTestSeed.Run(db);
        });
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() { if (_conn!=null) await _conn.DisposeAsync(); }
}