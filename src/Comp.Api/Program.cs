using Comp.Api.Endpoints;
using Comp.Api.Infrastructure;
using Comp.Api.Tenancy;
using Microsoft.EntityFrameworkCore;
using ITenantContext = Comp.Api.Tenancy.ITenantContext;
using TenantContext = Comp.Api.Tenancy.TenantContext;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<ITenantContext, TenantContext>();
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<CompDbContext>(opt =>
    {
        var cs = builder.Configuration.GetConnectionString("Default")
                 ?? "Host=comp-db;Database=comp;Username=postgres;Password=postgres";
        opt.UseNpgsql(cs);
    });
}

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CompDbContext>();
    db.Database.Migrate(); // prod/dev’de migration
}

app.MapHealth();

app.UseMiddleware<TenantContextMiddleware>();
app.MapMe();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CompDbContext>();
    db.Database.Migrate();

    var firm1 = Guid.Parse("a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac");
    var firm2 = Guid.Parse("44709835-d55a-ef2a-2327-5fdca19e55d8");

    if (!db.Salaries.Any())
    {
        db.Salaries.AddRange(
            new Salary { TenantId = firm1, Employee = "U1", Amount = 1000, Period = new DateOnly(2025, 1, 1) },
            new Salary { TenantId = firm1, Employee = "U1", Amount = 1100, Period = new DateOnly(2025, 2, 1) },
            new Salary { TenantId = firm2, Employee = "V1", Amount = 2000, Period = new DateOnly(2025, 1, 1) }
        );
        db.SaveChanges();
    }
}

app.Run();

public partial class Program
{
}