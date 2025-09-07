using System;
using System.Data.Common;
using System.Linq;
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

            if (!db.Objectives.Any())
            {
                db.Objectives.AddRange(
                    new Objective
                    {
                        Id = Guid.NewGuid(), TenantId = Guid.Parse("a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac"),
                        Title = "Firm1 Obj A"
                    },
                    new Objective
                    {
                        Id = Guid.NewGuid(), TenantId = Guid.Parse("a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac"),
                        Title = "Firm1 Obj B"
                    },
                    new Objective
                    {
                        Id = Guid.NewGuid(), TenantId = Guid.Parse("44709835-d55a-ef2a-2327-5fdca19e55d8"),
                        Title = "Firm2 Obj X"
                    }
                );
                db.SaveChanges();
            }
        });
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() { if (_conn!=null) await _conn.DisposeAsync(); }
}