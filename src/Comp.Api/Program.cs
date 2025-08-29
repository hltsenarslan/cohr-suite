using Common.Tenancy;
using Comp.Api.Endpoints;
using Comp.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

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
builder.Services.AddTenancy();

var app = builder.Build();
app.UseMiddleware<TenantContextMiddleware>();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CompDbContext>();
    db.Database.Migrate(); // prod/dev’de migration
}


app.MapCompHealthEndpoints();
app.UseTenantContext();
app.MapCompMeEndpoints();

app.Run();

public partial class Program
{
}