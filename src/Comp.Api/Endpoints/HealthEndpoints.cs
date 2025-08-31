namespace Comp.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealth(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { ok = true, service = "comp" }))
            .WithName("Health");
        app.MapGet("/ready",  () => Results.Ok(new { ready = true, service = "comp" }))
            .WithName("Ready");
        return app;
    }
}