using KeycloakDemo.Application.Abstractions;
using KeycloakDemo.Domain.Entities;

namespace KeycloakDemo.Infrastructure.Persistence;

/// <summary>Singleton in-memory repository — no database required for Parts 1-3.</summary>
internal sealed class InMemoryOrderRepository : IOrderRepository
{
    // Two users seeded to demonstrate Reader (own orders only) vs Admin (all orders).
    // testuser  = sub from Keycloak; replace with real sub if testing end-to-end.
    // adminuser = second user for cross-user visibility test.
    private static readonly IReadOnlyList<Order> _orders =
    [
        new() { Id = 1, Description = "Widget A — bulk",    CreatedBy = "testuser"  },
        new() { Id = 2, Description = "Widget B — express", CreatedBy = "testuser"  },
        new() { Id = 3, Description = "Widget C — sample",  CreatedBy = "testuser"  },
        new() { Id = 4, Description = "Gadget X — trial",   CreatedBy = "adminuser" },
        new() { Id = 5, Description = "Gadget Y — promo",   CreatedBy = "adminuser" },
    ];

    public Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult(_orders);

    public Task<Order?> GetByIdAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(_orders.FirstOrDefault(o => o.Id == id));

    public Task<IReadOnlyList<Order>> GetByUserAsync(string userId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Order>>(
            _orders.Where(o => o.CreatedBy == userId).ToList());
}
