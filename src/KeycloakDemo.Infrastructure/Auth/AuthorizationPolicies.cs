using KeycloakDemo.Domain;
using Microsoft.AspNetCore.Authorization;

namespace KeycloakDemo.Infrastructure.Auth;

public static class AuthorizationPolicies
{
    public const string ReadAccess        = "ReadAccess";
    public const string AdminAccess       = "AdminAccess";
    public const string AuditAccess       = "AuditAccess";
    public const string ServiceAccountOnly = "ServiceAccountOnly";

    // The client_id Keycloak assigns to the background worker.
    // In production, read this from configuration rather than hard-coding it here.
    // It is kept as a constant for clarity in this blog series.
    private const string WorkerClientId = "background-worker";

    public static void AddPolicies(AuthorizationOptions options)
    {
        options.AddPolicy(ReadAccess, policy =>
            policy.RequireRole(
                KeycloakRoleMapper.ToKeycloakRole(DomainRole.Reader),
                KeycloakRoleMapper.ToKeycloakRole(DomainRole.Admin)
            ));

        options.AddPolicy(AdminAccess, policy =>
            policy.RequireRole(KeycloakRoleMapper.ToKeycloakRole(DomainRole.Admin)));

        // Compound rule: Admin role AND audit.read OAuth scope.
        // Keycloak sends scope as one space-separated string ("profile email audit.read"),
        // so HasClaim(type, value) exact-match doesn't work — split first.
        options.AddPolicy(AuditAccess, policy =>
            policy.RequireAssertion(ctx =>
                ctx.User.IsInRole(KeycloakRoleMapper.ToKeycloakRole(DomainRole.Admin)) &&
                ctx.User.Claims
                    .Where(c => c.Type == "scope")
                    .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    .Contains("audit.read")
            ));

        // Restricts an endpoint to machine-to-machine callers only.
        // "azp" (Authorized Party) identifies WHICH client requested the token —
        // human users going through web-frontend have azp = "web-frontend",
        // the background worker has azp = "background-worker".
        // Combining with ReadAccess (via chained RequireAuthorization) means the
        // caller must BOTH be the worker AND hold the api-reader role.
        options.AddPolicy(ServiceAccountOnly, policy =>
            policy.RequireAssertion(ctx =>
                ctx.User.Claims
                    .Any(c => c.Type == "azp" && c.Value == WorkerClientId)
            ));
    }
}
