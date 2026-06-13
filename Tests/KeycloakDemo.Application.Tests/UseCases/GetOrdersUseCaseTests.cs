using KeycloakDemo.Application.Abstractions;
using KeycloakDemo.Application.UseCases;
using KeycloakDemo.Domain;
using KeycloakDemo.Domain.Entities;
using Xunit;

namespace KeycloakDemo.Application.Tests.UseCases;

// ── Fakes ─────────────────────────────────────────────────────────────────────
// No mocking framework — plain implementations with init-only properties.
// Zero ASP.NET Core dependencies in this file. The compiler enforces this
// because the test project references only KeycloakDemo.Application.

file sealed class FakeCurrentUser : ICurrentUser
{
    public string  UserId         { get; init; } = "user-1";
    public string  Username       { get; init; } = "testuser";
    public string  Email          { get; init; } = "test@example.com";
    public bool    IsAuthenticated { get; init; } = true;

    private readonly HashSet<DomainRole> _roles;

    public FakeCurrentUser(params DomainRole[] roles) => _roles = [..roles];

    public bool IsInRole(DomainRole role) => _roles.Contains(role);
}

file sealed class FakeOrderRepository : IOrderRepository
{
    // Three orders across two user IDs — enough to verify role-based filtering.
    private readonly List<Order> _orders =
    [
        new() { Id = 1, Description = "Alpha", CreatedBy = "user-1" },
        new() { Id = 2, Description = "Beta",  CreatedBy = "user-1" },
        new() { Id = 3, Description = "Gamma", CreatedBy = "user-2" },
    ];

    public Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Order>>(_orders);

    public Task<Order?> GetByIdAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(_orders.FirstOrDefault(o => o.Id == id));

    public Task<IReadOnlyList<Order>> GetByUserAsync(string userId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Order>>(_orders.Where(o => o.CreatedBy == userId).ToList());
}

// ── Tests ──────────────────────────────────────────────────────────────────────

public class GetOrdersUseCaseTests
{
    private static GetOrdersUseCase Build(ICurrentUser user) =>
        new(user, new FakeOrderRepository());

    [Fact]
    public async Task ExecuteAsync_UnauthenticatedUser_ThrowsUnauthorizedAccessException()
    {
        var sut = Build(new FakeCurrentUser { IsAuthenticated = false });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.ExecuteAsync());
    }

    [Fact]
    public async Task ExecuteAsync_AuthenticatedUserWithNoRole_ThrowsUnauthorizedAccessException()
    {
        // Authenticated but holds neither Reader nor Admin — Supervisor alone is insufficient.
        var sut = Build(new FakeCurrentUser(DomainRole.Supervisor));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.ExecuteAsync());
    }

    [Fact]
    public async Task ExecuteAsync_ReaderRole_ReturnsOnlyOwnOrders()
    {
        // user-1 has 2 orders in FakeOrderRepository; user-2 has 1.
        var sut = Build(new FakeCurrentUser(DomainRole.Reader) { UserId = "user-1" });

        var result = await sut.ExecuteAsync();

        Assert.Equal(2, result.Count);
        Assert.All(result, o => Assert.Equal("user-1", o.CreatedBy));
    }

    [Fact]
    public async Task ExecuteAsync_AdminRole_ReturnsAllOrders()
    {
        var sut = Build(new FakeCurrentUser(DomainRole.Admin) { UserId = "user-1" });

        var result = await sut.ExecuteAsync();

        // Admin sees all 3 orders regardless of CreatedBy.
        Assert.Equal(3, result.Count);
    }
}
