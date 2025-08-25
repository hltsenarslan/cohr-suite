using Core.Api.Infrastructure;
using Core.Api.Endpoints;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

/* --- Services --- */
builder.Services.AddDbContext<CoreDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddOpenTelemetry()
    .WithTracing(t =>
    {
        t.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("core-api"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(o =>
            {
                var ep = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://jaeger:4317";
                o.Endpoint = new Uri(ep);
            });
    });

var app = builder.Build();

/* --- Auto-migrate --- */
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    db.Database.Migrate();
}

/* --- Map endpoints --- */
app.MapHealthEndpoints();            // /health, /ready
app.MapInternalDomainEndpoints();    // /internal/domains/{host}
app.MapInternalTenantEndpoints();    // /internal/tenants/...
app.MapAdminTenantEndpoints();       // /internal/admin/tenants
app.MapAdminDomainEndpoints();       // /internal/admin/domains

app.Run("http://0.0.0.0:8080");

public partial class Program { }