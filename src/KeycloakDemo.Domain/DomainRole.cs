namespace KeycloakDemo.Domain;

/// <summary>
/// The roles a user can hold within the system, expressed in business vocabulary.
/// These names are domain concepts — they carry no knowledge of Keycloak,
/// JWT claim structures, or any other identity provider.
/// </summary>
public enum DomainRole
{
    /// <summary>
    /// Can read orders and other non-sensitive resources.
    /// Typical end-user with read-only access to their own data.
    /// </summary>
    Reader,

    /// <summary>
    /// Full administrative access: user management, all orders, audit logs.
    /// Implies Reader rights — admins do not need a separate Reader assignment.
    /// </summary>
    Admin,

    /// <summary>
    /// Elevated observability over operational data without full admin rights.
    /// Can view aggregated reports but cannot modify users or access audit trails.
    /// </summary>
    Supervisor,
}
