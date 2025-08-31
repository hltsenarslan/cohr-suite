using Microsoft.EntityFrameworkCore;
using Perf.Api.Infrastructure;
using Perf.Api.Endpoints;
using Perf.Api.Tenancy;

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


builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PerfDbContext>();
    db.Database.Migrate(); // prod/dev’de migration
}

app.MapPerfHealthEndpoints();
app.UseMiddleware<TenantContextMiddleware>();
app.MapPerfMeEndpoints();


// Örnek: tenant-filtered liste
app.MapGet("/objectives", async (PerfDbContext db) =>
{
    var list = await db.Objectives.OrderBy(x => x.Title).ToListAsync();
    return Results.Ok(list);
});


app.Run();

public partial class Program
{
}