using KeycloakDemo.Domain.Entities;

namespace KeycloakDemo.Application.Abstractions;

/// <summary>
/// Persistence contract for orders, defined by the Application layer.
/// The concrete implementation (in-memory, EF Core, etc.) lives in Infrastructure.
/// </summary>
public interface IOrderRepository
{
    Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken ct = default);
    Task<Order?>               GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Returns only orders whose <see cref="Order.CreatedBy"/> matches <paramref name="userId"/>.</summary>
    Task<IReadOnlyList<Order>> GetByUserAsync(string userId, CancellationToken ct = default);
}
