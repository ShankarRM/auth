using KeycloakDemo.Application.Abstractions;
using KeycloakDemo.Domain;
using KeycloakDemo.Domain.Entities;

namespace KeycloakDemo.Application.UseCases;

/// <summary>
/// Returns orders visible to the current user.
/// </summary>
/// <remarks>
/// Authorization logic lives here, not on endpoints. The use case is the
/// authoritative source of "who can see what" — endpoints are just transport.
///
/// Testing without the auth stack:
/// <code>
///   var sut = new GetOrdersUseCase(
///       new FakeCurrentUser { IsAuthenticated = true, Role = DomainRole.Reader },
///       new FakeOrderRepository());
///   var orders = await sut.ExecuteAsync();
/// </code>
/// No JwtBearer, no HttpContext, no mocking framework required.
/// </remarks>
public sealed class GetOrdersUseCase(ICurrentUser currentUser, IOrderRepository repository)
{
    public async Task<IReadOnlyList<Order>> ExecuteAsync(CancellationToken ct = default)
    {
        // First line of defense inside the domain boundary — even if HTTP middleware
        // is misconfigured, an anonymous caller cannot reach data.
        if (!currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("Request must carry a verified identity.");

        bool canRead = currentUser.IsInRole(DomainRole.Reader)
                    || currentUser.IsInRole(DomainRole.Admin);

        if (!canRead)
            throw new UnauthorizedAccessException("Reader or Admin role is required to list orders.");

        // Admins see the full order book; Readers see only their own.
        if (currentUser.IsInRole(DomainRole.Admin))
            return await repository.GetAllAsync(ct);

        return await repository.GetByUserAsync(currentUser.UserId, ct);
    }
}
