using Microsoft.AspNetCore.Authorization;

namespace KeycloakDemo;

public static class AuthorizationPolicies
{
    // Constants eliminate magic strings at call sites. A typo in a policy name
    // becomes a compile error rather than a silent 403 at runtime.
    public const string ReadAccess  = "ReadAccess";
    public const string AdminAccess = "AdminAccess";
    public const string AuditAccess = "AuditAccess";

    /// <summary>
    /// Registers all application authorization policies.
    /// Pass as the delegate to <c>builder.Services.AddAuthorization(...)</c>.
    /// </summary>
    public static void AddPolicies(AuthorizationOptions options)
    {
        // ReadAccess: Reader or Admin. Admin subsumes Reader — no separate Reader
        // role required in Keycloak for users who are already Admin.
        options.AddPolicy(ReadAccess, policy =>
            policy.RequireRole(
                DomainRole.Reader.ToKeycloakRole(),
                DomainRole.Admin.ToKeycloakRole()
            ));

        // AdminAccess: Admin only. Supervisor is explicitly excluded — give it
        // AdminAccess only when the business rules say Supervisors can administer.
        options.AddPolicy(AdminAccess, policy =>
            policy.RequireRole(DomainRole.Admin.ToKeycloakRole()));

        // AuditAccess: Admin role AND the "audit.read" OAuth scope.
        //
        // RequireAssertion is preferable to chaining RequireRole + RequireClaim here
        // because it expresses the compound rule as a single readable predicate.
        // It also avoids building a custom IAuthorizationRequirement + Handler pair
        // for every combination of role-plus-claim rule the API needs — those types
        // are worth creating only when the rule carries state or needs async evaluation.
        options.AddPolicy(AuditAccess, policy =>
            policy.RequireAssertion(ctx =>
                ctx.User.IsInRole(DomainRole.Admin.ToKeycloakRole()) &&
                // Keycloak encodes scope as one space-separated string: "profile email audit.read".
                // HasClaim(type, value) does exact match — useless here. Split first.
                ctx.User.Claims
                    .Where(c => c.Type == "scope")
                    .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    .Contains("audit.read")
            ));
    }
}
