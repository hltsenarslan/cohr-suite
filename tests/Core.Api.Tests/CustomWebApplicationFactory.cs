using System.Linq;
using Core.Api.Infrastructure;             // CoreDbContext
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Api.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _testConnectionString;

    public CustomWebApplicationFactory(string testConnectionString)
    {
        _testConnectionString = testConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Var olan DbContextOptions<CoreDbContext> kaydını kaldır
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<CoreDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            // Testcontainers Postgres connection string'i ile yeniden ekle
            services.AddDbContext<CoreDbContext>(opt => opt.UseNpgsql(_testConnectionString));
            
            
        });
    }
}