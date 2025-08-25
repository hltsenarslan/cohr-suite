namespace Core.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var serviceName = "core-api";

        app.MapGet("/health", () =>
                Results.Json(new { status = "ok", service = serviceName, ts = DateTime.UtcNow }))
            .WithName("Health");

        app.MapGet("/ready", () =>
            Results.Json(new { ready = true, service = serviceName }));

        return app;
    }
}