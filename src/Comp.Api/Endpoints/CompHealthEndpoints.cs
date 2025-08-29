namespace Comp.Api.Endpoints;

public static class CompHealthEndpoints
{
    public static IEndpointRouteBuilder MapCompHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "comp-api" }));
        app.MapGet("/ready",  () => Results.Ok(new { ready = true,  service = "comp-api" }));
        return app;
    }
}