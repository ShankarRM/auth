using System.ComponentModel.DataAnnotations;

namespace KeycloakDemo.Infrastructure.Auth;

/// <summary>
/// Configuration for a background worker that authenticates as itself (not as a user)
/// using the OAuth2 Client Credentials flow.
/// </summary>
/// <remarks>
/// Bound from the "Keycloak" configuration section in the Worker project's appsettings.json.
/// ClientSecret must never be stored in source control — supply it via an environment
/// variable (KC_WORKER_SECRET) or a secrets manager (Azure Key Vault, AWS Secrets Manager).
/// </remarks>
public sealed class KeycloakWorkerOptions
{
    [Required(AllowEmptyStrings = false)]
    public string Authority { get; init; } = null!;

    [Required(AllowEmptyStrings = false)]
    public string ClientId { get; init; } = null!;

    [Required(AllowEmptyStrings = false)]
    public string ClientSecret { get; init; } = null!;

    // Roles this service account holds — must match the Keycloak realm roles assigned
    // to the service account user in keycloak-setup.sh.
    // Used by ServiceAccountCurrentUser so Application-layer use cases work without
    // an HttpContext. If the service account's Keycloak roles change, update this list.
    public string[] Roles { get; init; } = [];
}
