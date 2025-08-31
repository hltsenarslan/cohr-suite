using System.IdentityModel.Tokens.Jwt;
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
builder.Services.AddHttpClient("core", c => c.BaseAddress = new Uri(builder.Configuration["YarpConf:core"] ?? ""));

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
            { ["d1"] = new DestinationConfig { Address = builder.Configuration["YarpConf:core"] ?? "" } }
        },
        new ClusterConfig
        {
            ClusterId = "perf",
            Destinations = new Dictionary<string, DestinationConfig>
            { ["d1"] = new DestinationConfig { Address = builder.Configuration["YarpConf:perf"] ?? "" } }
        },
        new ClusterConfig
        {
            ClusterId = "comp",
            Destinations = new Dictionary<string, DestinationConfig>
            { ["d1"] = new DestinationConfig { Address = builder.Configuration["YarpConf:comp"] ?? "" } }
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
            tenantId = map.TenantId; // hard mapping
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

    // 2) header
    tenantId ??= ctx.Request.Headers["X-Tenant-Id"].FirstOrDefault();

    // 3) JWT fallback (Authorization: Bearer ...)
    tenantId ??= GetTenantIdFromJwt(ctx);

    // (İsteğe bağlı) slug varsa ve JWT'den gelen tenantId farklıysa 403 yap:
    if (!string.IsNullOrEmpty(slug) && !string.IsNullOrEmpty(tenantId) && map?.PathMode == 1)
    {
        // slug -> id tekrar çöz ve uyuşmazlıkta 403 ver
        var check = await http.GetFromJsonAsync<TenantResolveDto>(
            $"/internal/tenants/resolve/{slug}",
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            ctx.RequestAborted);
        if (check?.tenantId is string resolved && !string.Equals(resolved, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new { error = "tenant_mismatch", slug, jwtTenantId = tenantId });
            return;
        }
    }

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

static string? GetTenantIdFromJwt(HttpContext ctx)
{
    // Eğer Gateway’de UseAuthentication çalışıyorsa önce buradan deneyebilirsin:
    var claimFromUser = ctx.User?.FindFirst("tenantId")?.Value
                        ?? ctx.User?.FindFirst("tid")?.Value
                        ?? ctx.User?.FindFirst("tenant_id")?.Value;
    if (!string.IsNullOrEmpty(claimFromUser))
        return claimFromUser;

    // Aksi halde sadece parse et (validation yok, salt claim okuma)
    var auth = ctx.Request.Headers.Authorization.FirstOrDefault();
    if (string.IsNullOrWhiteSpace(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        return null;

    var token = auth.Substring("Bearer ".Length).Trim();
    try
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        return jwt.Claims.FirstOrDefault(c =>
                c.Type == "tenantId" || c.Type == "tid" || c.Type == "tenant_id")
            ?.Value;
    }
    catch
    {
        return null;
    }
}

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

var urls = builder.Configuration["ASPNETCORE_URLS"];
app.Run(urls);

/* DTOs & Enums (Core API sayısal enum döndürüyor) */
public enum PathModeDto { host = 0, slug = 1 }

record DomainMapDto(string Host, int Module, string? TenantId, int PathMode, string? TenantSlug, bool IsActive);
record TenantResolveDto(string tenantId);

public partial class Program { }