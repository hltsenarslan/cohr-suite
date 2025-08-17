using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

string serviceName = "perf-api";

builder.Services.AddOpenTelemetry()
    .WithTracing(t =>
    {
        t.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(o =>
            {
                o.Endpoint = new Uri(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                                     ?? "http://localhost:4317");
            });
    });

var app = builder.Build();

app.MapGet("/echo-tenant", (HttpContext ctx) =>
{
    var t = ctx.Request.Headers["X-Tenant-Id"].ToString();
    return Results.Ok(new { tenant = t });
});

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = serviceName }));
app.MapGet("/ready", () => Results.Ok(new { ready = true, service = serviceName }));

app.Run("http://0.0.0.0:8080");