using Core.Api.Domain;
using Core.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// DB
var cs = builder.Configuration.GetConnectionString("Default")
         ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
         ?? "Host=postgres;Database=core;Username=postgres;Password=postgres";
builder.Services.AddDbContext<CoreDbContext>(o => o.UseNpgsql(cs));
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()); // enum -> "string"
});

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("core-api"))
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(o => o.Endpoint =
            new Uri(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://jaeger:4317"))
    );

var app = builder.Build();

// 🔹 Otomatik migration
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    db.Database.Migrate();
}


// migrate & seed (demo)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    await db.Database.MigrateAsync();

    if (!await db.DomainMappings.AnyAsync())
    {
        // Varsayılan domainler slug’lı çalışsın
        db.DomainMappings.AddRange(
            new DomainMapping{ Host="pys.local", Module=ModuleKind.performance, PathMode=PathMode.slug },
            new DomainMapping{ Host="pay.local", Module=ModuleKind.compensation, PathMode=PathMode.slug }
        );
        await db.SaveChangesAsync();
    }
}

// health
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "core-api" }));

// --- Internal endpoints (Gateway kullanacak) ---

// Host → mapping
app.MapGet("/internal/domains/{host}", async (string host, CoreDbContext db) =>
{
    var map = await db.DomainMappings.AsNoTracking()
               .FirstOrDefaultAsync(x => x.Host == host && x.IsActive);
    return map is null ? Results.NotFound() : Results.Ok(map);
});

// Slug → tenantId (demo: basit sabitleme)
app.MapGet("/internal/tenants/resolve/{slug}", (string slug) =>
{
    // M1’de demo: slug -> deterministic GUID; M2’de gerçek tenant tablosu ile değiştiririz
    var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(slug));
    var guid = new Guid(bytes.Take(16).ToArray());
    return Results.Ok(new { tenantId = guid });
});

// Tenant → modules (demo amaçlı)
app.MapGet("/internal/tenants/{tenantId:guid}/modules", (Guid tenantId, CoreDbContext db) =>
{
    // M1 demo: her tenant iki modüle sahipmiş gibi; sonraki milestone’da gerçek tablo
    string[] modules = ["performance","compensation"];
    return Results.Ok(modules);
});


// ---- AUTO MIGRATION ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    db.Database.Migrate();
}
// ------------------------
app.Run("http://0.0.0.0:8080");

public partial class Program { } // WebApplicationFactory için