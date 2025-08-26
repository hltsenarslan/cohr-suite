using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Yarp.ReverseProxy.Configuration;

var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

var builder = WebApplication.CreateBuilder(args);

// --- OpenTelemetry ---
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

// --- Services ---
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("core", c => c.BaseAddress = new Uri("http://core-api:8080"));

// --- YARP (in-memory) ---
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
app.Use(async (HttpContext ctx, RequestDelegate next) =>
{
    var cid = ctx.Request.Headers.TryGetValue("X-Correlation-Id", out var v) && !StringValues.IsNullOrEmpty(v)
        ? v.ToString()
        : Guid.NewGuid().ToString("N");

    ctx.Response.OnStarting(() =>
    {
        ctx.Response.Headers["X-Correlation-Id"] = cid;
        return Task.CompletedTask;
    });

    await next(ctx);
});

/* ------------- Tenant Resolver --------------- */
app.Use(async (HttpContext ctx, RequestDelegate next) =>
{
    var path = ctx.Request.Path.Value ?? "/";
    var segs = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

    bool isApi  = segs.Length >= 1 && segs[0].Equals("api",  StringComparison.OrdinalIgnoreCase);
    bool isPerf = isApi && segs.Length >= 2 && segs[1].Equals("perf", StringComparison.OrdinalIgnoreCase);
    bool isComp = isApi && segs.Length >= 2 && segs[1].Equals("comp", StringComparison.OrdinalIgnoreCase);

    if (!(isPerf || isComp))
    {   await next(ctx); return; }

    if (segs.Length >= 3 && (segs[2].Equals("health",  StringComparison.OrdinalIgnoreCase) ||
                             segs[2].Equals("ready",   StringComparison.OrdinalIgnoreCase) ||
                             segs[2].Equals("metrics", StringComparison.OrdinalIgnoreCase)))
    {   await next(ctx); return; }

    string? slug = segs.Length >= 3 ? segs[2] : null;
    var host = ctx.Request.Host.Host.ToLowerInvariant();

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
            return await res.Content.ReadFromJsonAsync<DomainMapDto>(
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                ctx.RequestAborted);
        }
        catch { return null; }
    });

    string? tenantId = null;

    if (map is not null)
    {
        if (!string.IsNullOrEmpty(map.TenantId))
        {
            tenantId = map.TenantId; // hard mapping (özel domain)
        }
        else if (map.PathMode == 1 /* slug */ && !string.IsNullOrEmpty(slug))
        {
            var dto = await http.GetFromJsonAsync<TenantResolveDto>(
                $"/internal/tenants/resolve/{slug}",
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                ctx.RequestAborted);
            tenantId = dto?.tenantId;
        }
        else if (map.PathMode == 0 /* host */)
        {
            var dto = await http.GetFromJsonAsync<TenantResolveDto>(
                $"/internal/tenants/by-host/{host}",
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                ctx.RequestAborted);
            tenantId = dto?.tenantId;
        }
    }

    tenantId ??= ctx.Request.Headers["X-Tenant-Id"].FirstOrDefault();

    if (string.IsNullOrEmpty(tenantId))
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await ctx.Response.WriteAsJsonAsync(new { error = "tenant_resolve_failed", host, pathMode = map?.PathMode });
        return;
    }

    ctx.Request.Headers["X-Tenant-Id"] = tenantId;
    ctx.Request.Headers["X-Host"] = host;

    await next(ctx);
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

/* DTOs & Enums (Core API sayısal enum döndürüyor) */
public enum PathModeDto { host = 0, slug = 1 }

record DomainMapDto(string Host, int Module, string? TenantId, int PathMode, string? TenantSlug, bool IsActive);
record TenantResolveDto(string tenantId);

public partial class Program { }