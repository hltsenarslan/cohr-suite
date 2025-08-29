namespace Perf.Api.Endpoints;

public static class PerfHealthEndpoints
{
    public static IEndpointRouteBuilder MapPerfHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "perf-api" }));
        app.MapGet("/ready",  () => Results.Ok(new { ready = true,  service = "perf-api" }));
        return app;
    }
}