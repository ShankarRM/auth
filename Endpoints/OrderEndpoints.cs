namespace KeycloakDemo;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/orders", () =>
        {
            var orders = new[]
            {
                new { Id = 1, Item = "Widget A", Quantity = 3, Status = "Shipped"   },
                new { Id = 2, Item = "Widget B", Quantity = 1, Status = "Pending"   },
                new { Id = 3, Item = "Widget C", Quantity = 7, Status = "Delivered" },
            };

            return Results.Ok(orders);
        })
        .RequireAuthorization(AuthorizationPolicies.ReadAccess)
        .WithName("GetOrders")
        .WithTags("Orders")
        .WithSummary("List orders. Requires api-reader or api-admin role.");

        return app;
    }
}
