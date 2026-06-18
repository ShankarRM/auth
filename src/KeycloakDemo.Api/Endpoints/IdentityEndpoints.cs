using KeycloakDemo.Application.Abstractions;

namespace KeycloakDemo.Api.Endpoints;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me", (ICurrentUser user) =>
            Results.Ok(new { sub = user.UserId, username = user.Username, email = user.Email }))
            .RequireAuthorization()
            .WithName("GetMe")
            .WithTags("Identity")
            .WithSummary("Returns sub, username, and email from the validated JWT.");

        return app;
    }
}
