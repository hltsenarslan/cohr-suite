using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Core.Api.Infrastructure; // CoreDbContext
using Core.Api.Domain;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit; // Tenant, DomainMapping, enums

public class CoreWebAppFactory : WebApplicationFactory<Program>,IAsyncLifetime
{
    private DbConnection? _conn;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<CoreDbContext>>();
            services.RemoveAll<CoreDbContext>();
            

            // Tek bir açık in-memory SQLite connection (test sınıfı boyunca)
            _conn = new SqliteConnection("DataSource=:memory:");
            _conn.Open();

            services.AddDbContext<CoreDbContext>(opts =>
            {
                opts.UseSqlite(_conn);
                // Postgres’e özgü uyarıları testte kapatmak istersen:
                // opts.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
            });

            // Provider değişikliği sonrası yeni provider ile bir scope yaratıp DB’yi kur+seed et
            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

            // Migrate yerine EnsureCreated (SQLite’ta hızlı ve temiz)
            db.Database.EnsureCreated();

            // --- Test seed ---
            if (!db.Tenants.Any())
            {
                var firm1 = new Tenant
                {
                    Id = Guid.Parse("a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac"),
                    Name = "Firm 1",
                    Slug = "firm1",
                    Status = "active",
                    CreatedAt = DateTime.UtcNow
                };
                var firm2 = new Tenant
                {
                    Id = Guid.Parse("44709835-d55a-ef2a-2327-5fdca19e55d8"),
                    Name = "Firm 2",
                    Slug = "firm2",
                    Status = "active",
                    CreatedAt = DateTime.UtcNow
                };
                db.Tenants.AddRange(firm1, firm2);
            }

            if (!db.DomainMappings.Any())
            {
                db.DomainMappings.AddRange(
                    new DomainMapping
                    {
                        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                        Host = "pys.local",
                        Module = ModuleKind.performance,
                        TenantId = null,         // slug mode: null
                        PathMode = PathMode.slug,
                        TenantSlug = null,
                        IsActive = true
                    },
                    new DomainMapping
                    {
                        Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                        Host = "pay.local",
                        Module = ModuleKind.compensation,
                        TenantId = Guid.Parse("44709835-d55a-ef2a-2327-5fdca19e55d8"), // host mode: hard map
                        PathMode = PathMode.host,
                        TenantSlug = null,
                        IsActive = true
                    }
                );
            }

            if (!db.TenantDomains.Any())
            {
                db.TenantDomains.AddRange(
                    new TenantDomain
                    {
                        Id = Guid.Parse("33333333-3333-3333-3333-333333333331"),
                        TenantId = Guid.Parse("a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac"),
                        Host = "pys.local",
                        IsDefault = true
                    },
                    new TenantDomain
                    {
                        Id = Guid.Parse("33333333-3333-3333-3333-333333333332"),
                        TenantId = Guid.Parse("44709835-d55a-ef2a-2327-5fdca19e55d8"),
                        Host = "pay.local",
                        IsDefault = true
                    }
                );
            }

            db.SaveChanges();
        });
    }

    public Task InitializeAsync() => Task.CompletedTask;
    
    public async Task DisposeAsync() { if (_conn!=null) await _conn.DisposeAsync(); }
}