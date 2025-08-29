using Common.Tenancy;
using Microsoft.EntityFrameworkCore;
using Perf.Api.Infrastructure;
using Perf.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<ITenantContext, TenantContext>();
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<PerfDbContext>(opt =>
    {
        var cs = builder.Configuration.GetConnectionString("Default")
                 ?? "Host=perf-db;Database=perf;Username=postgres;Password=postgres";
        opt.UseNpgsql(cs);
    });
}


builder.Services.AddTenancy();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();
app.UseMiddleware<TenantContextMiddleware>();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PerfDbContext>();
    db.Database.Migrate(); // prod/dev’de migration
}

app.MapPerfHealthEndpoints();
app.UseTenantContext();
app.MapPerfMeEndpoints();

app.Run();

public partial class Program { }