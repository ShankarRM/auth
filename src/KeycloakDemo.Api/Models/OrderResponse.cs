using KeycloakDemo.Domain.Entities;

namespace KeycloakDemo.Api.Models;

/// <summary>
/// API response shape for an order. Domain entities never cross the API boundary
/// as-is — this record controls exactly what is serialized to the client.
/// </summary>
/// <remarks>
/// If <see cref="Order"/> grows internal fields (e.g. cost, supplier data),
/// they stay invisible to the API consumer until explicitly added here.
/// </remarks>
public sealed record OrderResponse(int Id, string Description, string CreatedBy)
{
    public static OrderResponse From(Order order) =>
        new(order.Id, order.Description, order.CreatedBy);
}
