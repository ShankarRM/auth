using KeycloakDemo.Application.Abstractions;
using KeycloakDemo.Domain;
using Microsoft.Extensions.Options;

namespace KeycloakDemo.Infrastructure.Auth;

/// <summary>
/// <see cref="ICurrentUser"/> implementation for background workers that run without
/// an HTTP request context.
/// </summary>
/// <remarks>
/// Background workers have no user — they act under their own service identity.
/// Instead of reading claims from <c>HttpContext.User</c>, this reads identity from
/// <see cref="KeycloakWorkerOptions"/>, which is bound from appsettings.json and
/// reflects the service account Keycloak assigned to the worker's client.
///
/// Lifetime: Singleton — there is no per-request scope in a worker. The options are
/// resolved once at startup and remain stable for the process lifetime.
/// </remarks>
internal sealed class ServiceAccountCurrentUser(IOptions<KeycloakWorkerOptions> options)
    : ICurrentUser
{
    private readonly KeycloakWorkerOptions _opts = options.Value;

    // client_id serves as the service account's unique identifier.
    // Keycloak also creates a shadow user "service-account-{clientId}" internally,
    // but client_id is the stable, config-driven identity to use here.
    public string UserId => _opts.ClientId;

    // Keycloak's naming convention for service account usernames.
    public string Username => $"service-account-{_opts.ClientId}";

    // Service accounts have no email in the human-user sense.
    public string Email => string.Empty;

    // The worker is always authenticated as itself — if options are invalid,
    // ValidateOnStart would have thrown before this code runs.
    public bool IsAuthenticated => true;

    public bool IsInRole(DomainRole role)
    {
        // Map the domain role to its Keycloak string and check against the
        // configured roles list. This mirrors how CurrentUserService checks
        // ClaimTypes.Role claims — only the source differs (config vs JWT).
        var keycloakRole = KeycloakRoleMapper.ToKeycloakRole(role);
        return _opts.Roles.Contains(keycloakRole, StringComparer.OrdinalIgnoreCase);
    }
}
