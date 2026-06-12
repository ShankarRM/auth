namespace KeycloakDemo;

/// <summary>
/// Domain vocabulary for user roles. These names belong to the application's
/// ubiquitous language — not to Keycloak, LDAP, or any other identity provider.
/// </summary>
/// <remarks>
/// Keycloak role strings ("api-admin", "api-reader") are infrastructure details.
/// Leaking those strings into application logic creates an invisible coupling:
/// if a Keycloak role is renamed, authorization breaks at runtime with no compiler
/// warning. Mapping IdP strings to domain values at the boundary (here) keeps the
/// rest of the code provider-agnostic and refactor-safe.
/// </remarks>
public enum DomainRole
{
    Reader,
    Admin,
    Supervisor,
}

public static class DomainRoleExtensions
{
    /// <summary>Returns the Keycloak role string that corresponds to this domain role.</summary>
    public static string ToKeycloakRole(this DomainRole role) => role switch
    {
        DomainRole.Reader     => "api-reader",
        DomainRole.Admin      => "api-admin",
        DomainRole.Supervisor => "api-supervisor",
        // Exhaustiveness: a new enum member with no case here is a bug, not a default.
        _                     => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    /// <summary>
    /// Maps a Keycloak role string back to a domain role.
    /// Returns <c>null</c> for unknown strings so callers decide how to handle gaps.
    /// </summary>
    public static DomainRole? ToDomainRole(this string keycloakRole) => keycloakRole switch
    {
        "api-reader"     => DomainRole.Reader,
        "api-admin"      => DomainRole.Admin,
        "api-supervisor" => DomainRole.Supervisor,
        _                => null,
    };
}
