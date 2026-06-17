using KeycloakDemo.Application.Abstractions;
using KeycloakDemo.Domain.Entities;

namespace KeycloakDemo.Infrastructure.Persistence;

/// <summary>Singleton in-memory repository — no database required for Parts 1-3.</summary>
internal sealed class InMemoryOrderRepository : IOrderRepository
{
    // Orders 1–3 belong to alice (api-reader), orders 4–5 to bob (api-admin).
    // Readers see only their own; admins see all five — no database required.
    private static readonly IReadOnlyList<Order> _orders =
    [
        new() { Id = 1, Description = "Widget A — bulk",    CreatedBy = "alice" },
        new() { Id = 2, Description = "Widget B — express", CreatedBy = "alice" },
        new() { Id = 3, Description = "Widget C — sample",  CreatedBy = "alice" },
        new() { Id = 4, Description = "Gadget X — trial",   CreatedBy = "bob"   },
        new() { Id = 5, Description = "Gadget Y — promo",   CreatedBy = "bob"   },
    ];

    public Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult(_orders);

    public Task<Order?> GetByIdAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(_orders.FirstOrDefault(o => o.Id == id));

    public Task<IReadOnlyList<Order>> GetByUserAsync(string userId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Order>>(
            _orders.Where(o => o.CreatedBy == userId).ToList());
}
