using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .WithTracing(t =>
    {
        t.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("gateway"))
         .AddAspNetCoreInstrumentation()
         .AddHttpClientInstrumentation()
         .AddOtlpExporter(o =>
         {
             var ep = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://jaeger:4317";
             o.Endpoint = new Uri(ep);
         });
    });

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("core", c => c.BaseAddress = new Uri("http://core-api:8080"));

builder.Services.AddReverseProxy().LoadFromMemory(
    routes: new[]
    {
        new RouteConfig
        {
            RouteId = "core",
            ClusterId = "core",
            Match = new RouteMatch { Path = "/api/core/{**catch-all}" },
            Transforms = new[] { new Dictionary<string,string>{{"PathRemovePrefix","/api/core"}} }
        },
        new RouteConfig
        {
            RouteId = "perf",
            ClusterId = "perf",
            Match = new RouteMatch { Path = "/api/perf/{**catch-all}" },
            Transforms = new[] { new Dictionary<string,string>{{"PathRemovePrefix","/api/perf"}} }
        },
        new RouteConfig
        {
            RouteId = "comp",
            ClusterId = "comp",
            Match = new RouteMatch { Path = "/api/comp/{**catch-all}" },
            Transforms = new[] { new Dictionary<string,string>{{"PathRemovePrefix","/api/comp"}} }
        }
    },
    clusters: new[]
    {
        new ClusterConfig
        {
            ClusterId = "core",
            Destinations = new Dictionary<string, DestinationConfig>
            { ["d1"] = new DestinationConfig { Address = "http://core-api:8080/" } }
        },
        new ClusterConfig
        {
            ClusterId = "perf",
            Destinations = new Dictionary<string, DestinationConfig>
            { ["d1"] = new DestinationConfig { Address = "http://perf-api:8080/" } }
        },
        new ClusterConfig
        {
            ClusterId = "comp",
            Destinations = new Dictionary<string, DestinationConfig>
            { ["d1"] = new DestinationConfig { Address = "http://comp-api:8080/" } }
        }
    }
);

var app = builder.Build();

/* ---------------- Correlation ---------------- */
app.Use(async (ctx, next) =>
{
    var cid = ctx.Request.Headers.TryGetValue("X-Correlation-Id", out var v) && !StringValues.IsNullOrEmpty(v)
        ? v.ToString()
        : Guid.NewGuid().ToString("N");

    // Body yazımı başlamadan ekle
    ctx.Response.OnStarting(() =>
    {
        ctx.Response.Headers["X-Correlation-Id"] = cid;
        return Task.CompletedTask;
    });

    await next();
});

/* ------------- Tenant Resolver --------------- */
/* Sadece /api/perf/** ve /api/comp/** için çalışsın. /api/core/**, /health, /ready vb. bypass. */
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "/";
    bool isPerf = path.StartsWith("/api/perf/", StringComparison.OrdinalIgnoreCase);
    bool isComp = path.StartsWith("/api/comp/", StringComparison.OrdinalIgnoreCase);

    if (!(isPerf || isComp))
    {
        await next();   // core/internal ve diğer tüm istekler doğrudan proxy'ye gitsin
        return;
    }

    var host = ctx.Request.Host.Host.ToLowerInvariant();
    var firstSeg = path.Split('/', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault(); // perf|comp'dan sonrası

    var cache = ctx.RequestServices.GetRequiredService<IMemoryCache>();
    var http  = ctx.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("core");

    // Host → DomainMappings (cache 90s)
    var map = await cache.GetOrCreateAsync($"dom:{host}", async entry =>
    {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(90);
        try
        {
            using var res = await http.GetAsync($"/internal/domains/{host}", ctx.RequestAborted);
            if (!res.IsSuccessStatusCode) return null;
            var json = await res.Content.ReadAsStringAsync(ctx.RequestAborted);
            return System.Text.Json.JsonSerializer.Deserialize<DomainMapDto>(json);
        }
        catch { return null; }
    });

    string? tenantId = null;

    if (map?.TenantId is not null)
    {
        tenantId = map.TenantId; // özel domain
    }
    else if (map?.PathMode == "slug" && !string.IsNullOrEmpty(firstSeg))
    {
        using var res = await http.GetAsync($"/internal/tenants/resolve/{firstSeg}", ctx.RequestAborted);
        if (res.IsSuccessStatusCode)
        {
            var dto = System.Text.Json.JsonSerializer.Deserialize<TenantResolveDto>(
                await res.Content.ReadAsStringAsync(ctx.RequestAborted));
            tenantId = dto?.tenantId;
        }
    }

    // tenant bulunamadıysa perf/comp için hata ver
    if (string.IsNullOrEmpty(tenantId))
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await ctx.Response.WriteAsJsonAsync(new { error = "tenant_resolve_failed", host, pathMode = map?.PathMode });
        return;
    }

    // Propagate
    ctx.Request.Headers["X-Tenant-Id"] = tenantId;
    ctx.Request.Headers["X-Host"] = host;

    await next();
});

var serviceName = "gateway";

/* --------------- Health/Ready ---------------- */
app.MapGet("/health", () =>
        Results.Json(new { status = "ok", service = serviceName, ts = DateTime.UtcNow }))
   .WithName("Health");

app.MapGet("/ready", () => Results.Json(new { ready = true, service = serviceName }));

app.UseDefaultFiles();
app.UseStaticFiles();

/* ------------------ Proxy -------------------- */
app.MapReverseProxy();

app.Run("http://0.0.0.0:8080");

/* DTOs */
record DomainMapDto(string Host, string Module, string? TenantId, string PathMode, string? TenantSlug, bool IsActive);
record TenantResolveDto(string tenantId);

public partial class Program { }