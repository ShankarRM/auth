using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace KeycloakDemo.Infrastructure.Auth;

/// <summary>
/// Builds <see cref="TokenValidationParameters"/> for a multi-client Keycloak topology
/// where a single API must accept tokens issued to more than one OAuth2 client.
/// </summary>
/// <remarks>
/// WHY THIS EXISTS:
/// A user authenticated via web-frontend receives a JWT whose "aud" claim is "web-frontend"
/// (plus "account" added by Keycloak). When that user calls the API, the API validates
/// the token — but the default single-audience check rejects it because "dotnet-api" ≠
/// "web-frontend". This class solves that without compromising security.
///
/// APPROACH A — ValidAudiences list (implemented):
///   Set ValidAudiences = ["dotnet-api", "web-frontend"].
///   The middleware accepts a token if its "aud" claim contains ANY of the listed values.
///   Pro: simple, explicit, validated by the JWT middleware.
///   Con: the API must know the names of all its clients upfront.
///
/// APPROACH B — Disable audience validation, check "azp" claim (advanced, NOT implemented):
///   Set ValidateAudience = false and add a custom validator that checks:
///     ctx.User.Claims.Any(c => c.Type == "azp" && ALLOWED_AZP.Contains(c.Value))
///   "azp" (Authorized Party) identifies the CLIENT that requested the token, while
///   "aud" identifies the resource the token is intended for.
///   Pro: works even when the audience claim is omitted or dynamic.
///   Con: audience validation is a security control — disabling it weakens the token check.
///        Only use this if your Keycloak configuration does not include a stable audience.
/// </remarks>
internal static class TokenValidationConfig
{
    internal static TokenValidationParameters Build(KeycloakOptions options) =>
        new()
        {
            ValidateIssuer   = true,
            ValidateAudience = true,
            ValidateLifetime = true,

            // Accept tokens whose "aud" matches the primary audience OR any additional
            // audience (e.g. "web-frontend"). Order does not matter — any match passes.
            ValidAudiences = [options.Audience, ..options.AdditionalAudiences],

            // Preserve raw OIDC claim names — MapInboundClaims = false is set on
            // JwtBearerOptions so "sub", "preferred_username", "azp" arrive as-is.
            NameClaimType = "preferred_username",

            // KeycloakRoleClaimsTransformation writes roles as ClaimTypes.Role.
            // RequireRole() and IsInRole() use this type for their lookups.
            RoleClaimType = ClaimTypes.Role,

            // APPROACH B reference (keep commented for documentation):
            // ValidateAudience = false,
            // AudienceValidator = (audiences, token, parameters) =>
            // {
            //     var azp = (token as System.IdentityModel.Tokens.Jwt.JwtSecurityToken)
            //         ?.Claims.FirstOrDefault(c => c.Type == "azp")?.Value;
            //     return azp is "dotnet-api" or "web-frontend" or "background-worker";
            // },
        };
}
