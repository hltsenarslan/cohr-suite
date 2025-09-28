using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using NSwag;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Yarp.ReverseProxy.Configuration;
using Swashbuckle.AspNetCore.SwaggerUI;
using OpenApiInfo = Microsoft.OpenApi.Models.OpenApiInfo;

static async Task<T?> GetJsonOrNull<T>(HttpClient http, string url, JsonSerializerOptions opts, CancellationToken ct)
{
    try
    {
        using var res = await http.GetAsync(url, ct);
        if (!res.IsSuccessStatusCode) return default;
        return await res.Content.ReadFromJsonAsync<T>(opts, ct);
    }
    catch
    {
        return default;
    }
}

var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("gateway", new OpenApiInfo { Title = "Gateway Swagger", Version = "v1" });
});
builder.Services.AddHttpClient("swagger");

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("core", c => c.BaseAddress = new Uri(builder.Configuration["YarpConf:core"] ?? ""));

builder.Services.AddReverseProxy().LoadFromMemory(
    routes: new[]
    {
        new RouteConfig
        {
            RouteId = "core",
            ClusterId = "core",
            Match = new RouteMatch { Path = "/api/core/{**catch-all}" },
            Transforms = new[] { new Dictionary<string, string> { { "PathRemovePrefix", "/api/core" } } }
        },
        new RouteConfig
        {
            RouteId = "perf",
            ClusterId = "perf",
            Match = new RouteMatch { Path = "/api/perf/{**catch-all}" },
            Transforms = new[] { new Dictionary<string, string> { { "PathRemovePrefix", "/api/perf" } } }
        },
        new RouteConfig
        {
            RouteId = "comp",
            ClusterId = "comp",
            Match = new RouteMatch { Path = "/api/comp/{**catch-all}" },
            Transforms = new[] { new Dictionary<string, string> { { "PathRemovePrefix", "/api/comp" } } }
        },
        new RouteConfig
        {
            RouteId = "files",
            ClusterId = "files",
            Match = new RouteMatch { Path = "/api/files/{**catch-all}" }
        },
        new RouteConfig
        {
            RouteId = "notify",
            ClusterId = "notify",
            Match = new RouteMatch { Path = "/api/notify/{**catch-all}" }
        },
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
        },
        new ClusterConfig
        {
            ClusterId = "files",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["d1"] = new DestinationConfig { Address = builder.Configuration["YarpConf:files"] ?? "" }
            }
        },
        new ClusterConfig
        {
            ClusterId = "notify",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["d1"] = new DestinationConfig { Address = builder.Configuration["YarpConf:notify"] ?? "" }
            }
        },
    }
);

var app = builder.Build();

app.MapGet("/swagger/proxy/{service}", async (string service, IConfiguration cfg, IHttpClientFactory hcf) =>
    {
        var services = cfg.GetSection("Swagger:Services").Get<List<SwaggerSvc>>() ?? [];
        var target = services.FirstOrDefault(s => s.name.Equals(service, StringComparison.OrdinalIgnoreCase))?.url;
        if (string.IsNullOrWhiteSpace(target))
            return Results.NotFound(new { error = "service_not_found", service });

        var client = hcf.CreateClient("swagger");
        using var resp = await client.GetAsync(target);
        if (!resp.IsSuccessStatusCode)
            return Results.StatusCode((int)resp.StatusCode);

        var contentType = resp.Content.Headers.ContentType?.ToString() ?? "application/json";
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        return Results.Bytes(bytes, contentType);
    })
    .WithName("SwaggerProxy");

// 2) UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    var services = app.Configuration.GetSection("Swagger:Services").Get<List<SwaggerSvc>>() ?? [];
    foreach (var s in services)
        c.SwaggerEndpoint($"/swagger/proxy/{s.name}", $"{s.name.ToUpper()} API");
    
    c.SwaggerEndpoint("/swagger/aggregate/v1/swagger.json", "Unified API");

    // (İstersen) tekleştirilmiş birleşik JSON’u da ekleyebilirsin:
    // c.SwaggerEndpoint("/swagger/aggregate/v1/swagger.json", "UNIFIED API");

    c.DocExpansion(DocExpansion.List);
    c.DisplayOperationId();
    c.RoutePrefix = "swagger"; // http://localhost:5000/swagger
});

app.MapGet("/swagger/aggregate/v1/swagger.json", async (IConfiguration cfg, IHttpClientFactory hcf) =>
    {
        var services = cfg.GetSection("Swagger:Services").Get<List<SwaggerSvc>>() ?? [];
        if (services.Count == 0)
            return Results.Problem("No swagger services configured");

        var client = hcf.CreateClient("swagger");

        // Tüm dökümanları çek
        var docs = new List<OpenApiDocument>();
        foreach (var s in services)
        {
            using var resp = await client.GetAsync(s.url);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            var doc = await OpenApiDocument.FromJsonAsync(json);

            // Çakışmaları önlemek için operationId'lere prefix ekle (opsiyonel ama önerilir)
            if (cfg.GetValue("Swagger:OperationIdPrefixByService", true))
            {
                foreach (var path in doc.Paths.Values)
                {
                    foreach (var op in path.Values)
                    {
                        if (!string.IsNullOrWhiteSpace(op.OperationId))
                            op.OperationId = $"{s.name}_{op.OperationId}";
                    }
                }
            }

            // Server bilgisini Gateway'e işaret edecek şekilde normalize edebilirsin (opsiyonel)
            // doc.Servers = new List<OpenApiServer> { new() { Url = "http://localhost:8080" } };

            docs.Add(doc);
        }

        // Merge: NSwag, doğrudan “tek doc’a ekle” API’si sunmuyor ancak ana doc’u alıp diğerlerini içine aktarabiliriz.
        var baseDoc = docs[0];

        for (int i = 1; i < docs.Count; i++)
        {
            var d = docs[i];

            // Paths
            foreach (var (path, item) in d.Paths)
            {
                // Çakışmayı önlemek için aynı path varsa sonrakine suffix ekleyebilirsin (örn. /users → /notify_users)
                if (baseDoc.Paths.ContainsKey(path))
                {
                    var newPath = $"/{services[i].name}{(path.StartsWith("/") ? "" : "/")}{path}".Replace("//", "/");
                    baseDoc.Paths[newPath] = item;
                }
                else
                {
                    baseDoc.Paths[path] = item;
                }
            }

            // Şemalar
            foreach (var (name, schema) in d.Components.Schemas)
            {
                var newName = name;
                if (baseDoc.Components.Schemas.ContainsKey(newName))
                    newName = $"{services[i].name}_{name}";
                baseDoc.Components.Schemas[newName] = schema;
            }

            // SecuritySchemes (varsa)
            foreach (var (name, sec) in d.Components.SecuritySchemes)
            {
                var newName = name;
                if (baseDoc.Components.SecuritySchemes.ContainsKey(newName))
                    newName = $"{services[i].name}_{name}";
                baseDoc.Components.SecuritySchemes[newName] = sec;
            }

            // Tags (UI için güzel olur)
            foreach (var tag in d.Tags)
            {
                if (!baseDoc.Tags.Any(t => t.Name == tag.Name))
                    baseDoc.Tags.Add(tag);
            }
        }

        // Başlık
        baseDoc.Info.Title = "Co-HR Suite Unified API";
        baseDoc.Info.Version = "v1";

        var mergedJson = baseDoc.ToJson(); // tekleştirilmiş JSON
        return Results.Content(mergedJson, "application/json");
    })
    .WithName("UnifiedSwagger")
    .WithTags("Swagger");


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

app.Use(async (HttpContext ctx, RequestDelegate next) =>
{
    var path = ctx.Request.Path.Value ?? "/";
    var segs = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

    bool isApi = segs.Length >= 1 && segs[0].Equals("api", StringComparison.OrdinalIgnoreCase);
    bool isPerf = isApi && segs.Length >= 2 && segs[1].Equals("perf", StringComparison.OrdinalIgnoreCase);
    bool isComp = isApi && segs.Length >= 2 && segs[1].Equals("comp", StringComparison.OrdinalIgnoreCase);
    bool isFiles = isApi && segs.Length >= 2 && segs[1].Equals("files", StringComparison.OrdinalIgnoreCase);
    bool isNotify = isApi && segs.Length >= 2 && segs[1].Equals("notify", StringComparison.OrdinalIgnoreCase);

    if (!(isPerf || isComp || isFiles || isNotify))
    {
        await next(ctx);
        return;
    }

    if (segs.Length >= 3 && (segs[2].Equals("health", StringComparison.OrdinalIgnoreCase) ||
                             segs[2].Equals("ready", StringComparison.OrdinalIgnoreCase) ||
                             segs[2].Equals("metrics", StringComparison.OrdinalIgnoreCase)))
    {
        await next(ctx);
        return;
    }

    string? slug = segs.Length >= 3 ? segs[2] : null;
    var host = ctx.Request.Host.Host.ToLowerInvariant();

    var cache = ctx.RequestServices.GetRequiredService<IMemoryCache>();
    var http = ctx.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("core");

    var map = await cache.GetOrCreateAsync($"dom:{host}", async entry =>
    {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(90);
        return await GetJsonOrNull<DomainMapDto>(http, $"/internal/domains/{host}", jsonOpts, ctx.RequestAborted);
    });

    string? tenantId = null;

    if (map is not null)
    {
        if (!string.IsNullOrEmpty(map.TenantId))
        {
            tenantId = map.TenantId;
        }
        else if (map.PathMode == (int)PathModeDto.slug && !string.IsNullOrEmpty(slug))
        {
            var dto = await GetJsonOrNull<TenantResolveDto>(http, $"/internal/tenants/resolve/{slug}", jsonOpts,
                ctx.RequestAborted);
            tenantId = dto?.tenantId;
        }
        else if (map.PathMode == (int)PathModeDto.host)
        {
            var dto = await GetJsonOrNull<TenantResolveDto>(http, $"/internal/tenants/by-host/{host}", jsonOpts,
                ctx.RequestAborted);
            tenantId = dto?.tenantId;
        }
    }

    tenantId ??= ctx.Request.Headers["X-Tenant-Id"].FirstOrDefault();

    tenantId ??= GetTenantIdFromJwt(ctx);

    if (!string.IsNullOrEmpty(slug) && !string.IsNullOrEmpty(tenantId) && map?.PathMode == (int)PathModeDto.slug)
    {
        var check = await GetJsonOrNull<TenantResolveDto>(http, $"/internal/tenants/resolve/{slug}", jsonOpts,
            ctx.RequestAborted);
        if (check?.tenantId is string resolved &&
            !string.Equals(resolved, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new { error = "tenant_mismatch", slug, jwtTenantId = tenantId });
            return;
        }
    }

    if (string.IsNullOrEmpty(tenantId) && !isFiles)
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
    var claimFromUser = ctx.User?.FindFirst("tenantId")?.Value
                        ?? ctx.User?.FindFirst("tid")?.Value
                        ?? ctx.User?.FindFirst("tenant_id")?.Value;
    if (!string.IsNullOrEmpty(claimFromUser))
        return claimFromUser;

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

app.MapGet("/health", () =>
        Results.Json(new { status = "ok", service = serviceName, ts = DateTime.UtcNow }))
    .WithName("Health");

app.MapGet("/ready", () => Results.Json(new { ready = true, service = serviceName }));

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapReverseProxy();

app.Use(async (ctx, next) =>
{
    var auth = ctx.Request.Headers.Authorization.ToString();
    var tenant = ctx.Request.Headers["X-Tenant-Id"].ToString();
    Console.WriteLine($"GW IN → Auth: {(!string.IsNullOrEmpty(auth))}, X-Tenant-Id: {tenant}");
    await next();
});

var urls = builder.Configuration["ASPNETCORE_URLS"];
app.Run(urls);

public enum PathModeDto
{
    host = 0,
    slug = 1
}

record DomainMapDto(string Host, int Module, string? TenantId, int PathMode, string? TenantSlug, bool IsActive);

record TenantResolveDto(string tenantId);

record SwaggerSvc(string name, string title, string url);

public partial class Program
{
}