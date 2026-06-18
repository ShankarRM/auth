using System.Security.Claims;
using KeycloakDemo.Application.Abstractions;
using KeycloakDemo.Domain;
using Microsoft.AspNetCore.Http;

namespace KeycloakDemo.Infrastructure.Auth;

/// <summary>
/// This is the only place in the codebase that touches <see cref="ClaimsPrincipal"/>
/// for identity resolution. Everything above this layer (Application, Domain) works
/// with <see cref="ICurrentUser"/> and never imports <c>System.Security.Claims</c>.
/// </summary>
internal sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    // Snapshot the principal at construction time — the HttpContext is request-scoped
    // and this service is Scoped, so this is safe.
    private readonly ClaimsPrincipal _user =
        httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();

    // Uses preferred_username as UserId so it aligns with InMemoryOrderRepository's
    // CreatedBy field, which stores usernames ("alice", "bob"). In a production system
    // with a real database, UserId would be the stable sub UUID instead.
    public string UserId   => _user.FindFirst("preferred_username")?.Value ?? string.Empty;
    public string Username => _user.FindFirst("preferred_username")?.Value ?? string.Empty;
    public string Email    => _user.FindFirst("email")?.Value              ?? string.Empty;

    public bool IsAuthenticated => _user.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(DomainRole role)
    {
        // Translate domain role → Keycloak string → check ClaimTypes.Role claim.
        // KeycloakRoleClaimsTransformation has already flattened realm/client roles
        // into ClaimTypes.Role by the time this is called.
        var keycloakRole = KeycloakRoleMapper.ToKeycloakRole(role);
        return _user.IsInRole(keycloakRole);
    }
}
