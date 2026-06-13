using KeycloakDemo.Domain;
using Microsoft.AspNetCore.Authorization;

namespace KeycloakDemo.Infrastructure.Auth;

public static class AuthorizationPolicies
{
    public const string ReadAccess  = "ReadAccess";
    public const string AdminAccess = "AdminAccess";
    public const string AuditAccess = "AuditAccess";

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
        // RequireAssertion lets us express this as a single predicate without a
        // custom IAuthorizationRequirement type.
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
    }
}
