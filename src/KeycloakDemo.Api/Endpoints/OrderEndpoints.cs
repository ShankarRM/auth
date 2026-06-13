using KeycloakDemo.Api.Models;
using KeycloakDemo.Application.Abstractions;
using KeycloakDemo.Application.UseCases;
using KeycloakDemo.Infrastructure.Auth;

namespace KeycloakDemo.Api.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        // RequireAuthorization() here ensures unauthenticated requests get 401 before
        // the handler runs. The use case provides a second check (role-based filtering)
        // and throws UnauthorizedAccessException for insufficient role — caught below → 403.
        // Two layers of defense: HTTP middleware + application logic.
        app.MapGet("/orders", async (GetOrdersUseCase useCase, CancellationToken ct) =>
        {
            try
            {
                var orders = await useCase.ExecuteAsync(ct);
                return Results.Ok(orders.Select(OrderResponse.From));
            }
            catch (UnauthorizedAccessException)
            {
                // Use case threw — caller is authenticated but lacks the required role.
                return Results.Forbid();
            }
        })
        .RequireAuthorization()
        .WithName("GetOrders")
        .WithTags("Orders")
        .WithSummary("List orders. Admins see all; Readers see only their own.");

        app.MapGet("/orders/{id:int}", async (int id, IOrderRepository repository, CancellationToken ct) =>
        {
            // No dedicated use case yet — a real project would have GetOrderByIdUseCase.
            var order = await repository.GetByIdAsync(id, ct);
            return order is null ? Results.NotFound() : Results.Ok(OrderResponse.From(order));
        })
        .RequireAuthorization(AuthorizationPolicies.ReadAccess)
        .WithName("GetOrderById")
        .WithTags("Orders")
        .WithSummary("Get a single order by ID. Requires api-reader or api-admin role.");

        return app;
    }
}
