using KeycloakDemo.Domain;

namespace KeycloakDemo.Infrastructure.Auth;

/// <summary>
/// Bidirectional mapping between <see cref="DomainRole"/> and Keycloak role strings.
/// </summary>
/// <remarks>
/// Keycloak role strings ("api-admin", "api-reader") are infrastructure vocabulary.
/// Mapping happens here and never leaks into Domain or Application. If Keycloak roles
/// are renamed, one switch arm changes — no domain or application code is touched.
/// </remarks>
internal static class KeycloakRoleMapper
{
    internal static string ToKeycloakRole(DomainRole role) => role switch
    {
        DomainRole.Reader     => "api-reader",
        DomainRole.Admin      => "api-admin",
        DomainRole.Supervisor => "api-supervisor",
        // Exhaustive: an unhandled enum member is a bug, not a silent default.
        _                     => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    internal static DomainRole? ToDomainRole(string keycloakRole) => keycloakRole switch
    {
        "api-reader"     => DomainRole.Reader,
        "api-admin"      => DomainRole.Admin,
        "api-supervisor" => DomainRole.Supervisor,
        _                => null,
    };
}
