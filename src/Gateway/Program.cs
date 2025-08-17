using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry
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

// YARP: prefixleri kırpıyoruz ki backend /health'e düşsün
builder.Services.AddReverseProxy().LoadFromMemory(
    routes: new[]
    {
        new RouteConfig
        {
            RouteId = "core",
            ClusterId = "core",
            Match = new RouteMatch { Path = "/api/core/{**catch-all}" },
            Transforms = new[]
            {
                new Dictionary<string,string> { ["PathRemovePrefix"] = "/api/core" }
            }
        },
        new RouteConfig
        {
            RouteId = "perf",
            ClusterId = "perf",
            Match = new RouteMatch { Path = "/api/perf/{**catch-all}" },
            Transforms = new[]
            {
                new Dictionary<string,string> { ["PathRemovePrefix"] = "/api/perf" }
            }
        },
        new RouteConfig
        {
            RouteId = "comp",
            ClusterId = "comp",
            Match = new RouteMatch { Path = "/api/comp/{**catch-all}" },
            Transforms = new[]
            {
                new Dictionary<string,string> { ["PathRemovePrefix"] = "/api/comp" }
            }
        }
    },
    clusters: new[]
    {
        new ClusterConfig
        {
            ClusterId = "core",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["d1"] = new DestinationConfig { Address = "http://core-api:8080/" }
            }
        },
        new ClusterConfig
        {
            ClusterId = "perf",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["d1"] = new DestinationConfig { Address = "http://perf-api:8080/" }
            }
        },
        new ClusterConfig
        {
            ClusterId = "comp",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["d1"] = new DestinationConfig { Address = "http://comp-api:8080/" }
            }
        }
    }
);

var app = builder.Build();

// Correlation-Id
app.Use(async (ctx, next) =>
{
    var cid = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault()
              ?? Guid.NewGuid().ToString("N");
    ctx.Response.Headers["X-Correlation-Id"] = cid;
    await next();
});

// Health
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "gateway" }));
app.MapGet("/ready",  () => Results.Ok(new { ready = true }));

// Basit statik demo
app.UseDefaultFiles();
app.UseStaticFiles();

// Proxy
app.MapReverseProxy();

app.Run("http://0.0.0.0:8080");