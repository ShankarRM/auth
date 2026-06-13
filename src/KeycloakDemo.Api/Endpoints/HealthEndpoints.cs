namespace KeycloakDemo.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok("OK"))
           .AllowAnonymous()
           .WithName("Health")
           .WithTags("System")
           .WithSummary("Liveness probe — no token required.");

        return app;
    }
}
