using System.Data.Common;
using Comp.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

public class CompWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private DbConnection? _conn;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<CompDbContext>>();
            services.RemoveAll<CompDbContext>();

            _conn = new SqliteConnection("DataSource=:memory:");
            _conn.Open();

            services.AddDbContext<CompDbContext>(o => o.UseSqlite(_conn));

            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CompDbContext>();
            db.Database.EnsureCreated();

            var firm1 = Guid.Parse("a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac");
            var firm2 = Guid.Parse("44709835-d55a-ef2a-2327-5fdca19e55d8");

            if (!db.Salaries.Any())
            {
                db.Salaries.AddRange(
                    new Salary { TenantId = firm1, Employee="U1", Amount=1000, Period=new DateOnly(2025,1,1) },
                    new Salary { TenantId = firm1, Employee="U1", Amount=1100, Period=new DateOnly(2025,2,1) },
                    new Salary { TenantId = firm2, Employee="V1", Amount=2000, Period=new DateOnly(2025,1,1) }
                );
                db.SaveChanges();
            }
        });
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_conn != null) await _conn.DisposeAsync();
    }
}