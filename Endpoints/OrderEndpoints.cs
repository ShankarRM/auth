namespace KeycloakDemo;

public static class OrderEndpoints
{
    // Orders 1–3 belong to alice (api-reader), 4–5 to bob (api-admin).
    // Readers see only their own; admins see all five.
    private static readonly IReadOnlyList<(int Id, string Item, int Quantity, string Status, string CreatedBy)> _orders =
    [
        (1, "Widget A — bulk",    3, "Shipped",   "alice"),
        (2, "Widget B — express", 1, "Pending",   "alice"),
        (3, "Widget C — sample",  7, "Delivered", "alice"),
        (4, "Gadget X — trial",   2, "Shipped",   "bob"),
        (5, "Gadget Y — promo",   5, "Pending",   "bob"),
    ];

    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/orders", (HttpContext ctx) =>
        {
            var username = ctx.User.FindFirst("preferred_username")?.Value ?? string.Empty;
            var isAdmin  = ctx.User.IsInRole(DomainRole.Admin.ToKeycloakRole());

            var visible = isAdmin
                ? _orders
                : _orders.Where(o => o.CreatedBy == username).ToList();

            return Results.Ok(visible.Select(o => new
            {
                o.Id, o.Item, o.Quantity, o.Status, o.CreatedBy
            }));
        })
        .RequireAuthorization(AuthorizationPolicies.ReadAccess)
        .WithName("GetOrders")
        .WithTags("Orders")
        .WithSummary("List orders. Admins see all; Readers see only their own.");

        return app;
    }
}
