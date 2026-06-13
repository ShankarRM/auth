namespace KeycloakDemo;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me", (HttpContext ctx) =>
        {
            var sub      = ctx.User.FindFirst("sub")?.Value;
            var username = ctx.User.FindFirst("preferred_username")?.Value;
            var email    = ctx.User.FindFirst("email")?.Value;

            return Results.Ok(new { sub, username, email });
        })
        .RequireAuthorization()
        .WithName("GetMe")
        .WithTags("Identity")
        .WithSummary("Returns sub, preferred_username, and email from the validated JWT.");

        return app;
    }
}
