using KeycloakDemo.Domain;

namespace KeycloakDemo.Application.Abstractions;

/// <summary>
/// Represents the identity of the user making the current request,
/// as understood by the Application layer.
/// </summary>
/// <remarks>
/// The Application layer depends on this interface, never on
/// <c>ClaimsPrincipal</c>, <c>IHttpContextAccessor</c>, or any auth stack type.
/// The infrastructure implementation (<c>CurrentUserService</c>) lives in
/// <c>KeycloakDemo.Infrastructure</c> and is invisible to this layer.
/// </remarks>
public interface ICurrentUser
{
    /// <summary>
    /// Opaque unique identifier for the user — corresponds to the <c>sub</c> JWT claim.
    /// Use this for ownership checks and audit trails, never for display.
    /// </summary>
    string UserId { get; }

    /// <summary>Human-readable login name, suitable for display and logging.</summary>
    string Username { get; }

    /// <summary>The user's email address as verified by the identity provider.</summary>
    string Email { get; }

    /// <summary>
    /// <c>true</c> if the request carries a verified identity.
    /// Use cases must check this before performing any work — never assume
    /// the HTTP middleware has already enforced authentication.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Returns <c>true</c> if the user holds the specified domain role.
    /// The Application layer uses <see cref="DomainRole"/> vocabulary exclusively —
    /// it has no concept of Keycloak role strings, <c>ClaimTypes.Role</c>, or JWT claims.
    /// </summary>
    bool IsInRole(DomainRole role);
}
