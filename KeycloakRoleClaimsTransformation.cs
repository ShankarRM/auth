using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace KeycloakDemo;

/// <summary>
/// Transforms Keycloak JWT claims into standard .NET role claims so that
/// <see cref="ClaimsPrincipal.IsInRole"/> and policy-based authorization work
/// without special configuration on every endpoint.
/// </summary>
/// <remarks>
/// Keycloak encodes roles in two nested structures:
/// <list type="bullet">
///   <item><c>realm_access.roles</c> — realm-level roles; apply to every client in the realm</item>
///   <item><c>resource_access.{clientId}.roles</c> — roles scoped to one specific client application</item>
/// </list>
/// Neither maps automatically to <see cref="ClaimTypes.Role"/>; this transformer bridges the gap.
/// </remarks>
public sealed class KeycloakRoleClaimsTransformation : IClaimsTransformation
{
    private readonly string _clientId;

    public KeycloakRoleClaimsTransformation(IConfiguration configuration)
    {
        // The Keycloak audience is the same string used as the key under resource_access.
        _clientId = configuration["Keycloak:Audience"]
            ?? throw new InvalidOperationException("Keycloak:Audience is required.");
    }

    /// <summary>
    /// Reads realm and client roles from the principal and re-emits them as
    /// <see cref="ClaimTypes.Role"/> claims on a cloned identity.
    /// </summary>
    /// <remarks>
    /// ASP.NET Core may invoke this method more than once per request (once per
    /// registered authentication scheme). The implementation is idempotent:
    /// it checks for existing role claims before adding, so repeated calls
    /// produce the same result without growing the claim set.
    /// </remarks>
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // Always clone — mutating the incoming principal violates the contract
        // and can corrupt shared state when the same instance is reused.
        var cloned   = principal.Clone();
        var identity = (ClaimsIdentity)cloned.Identity!;

        AddRealmRoles(identity, principal);
        AddClientRoles(identity, principal);

        return Task.FromResult(cloned);
    }

    private static void AddRealmRoles(ClaimsIdentity identity, ClaimsPrincipal source)
    {
        // realm_access JSON shape: { "roles": ["offline_access", "api-admin", ...] }
        var realmAccessClaim = source.FindFirst("realm_access");
        if (realmAccessClaim is null) return;

        JsonElement realmAccess;
        try
        {
            realmAccess = JsonDocument.Parse(realmAccessClaim.Value).RootElement;
        }
        catch (JsonException)
        {
            // Malformed claim — skip rather than crash. Real Keycloak tokens are always
            // valid JSON; this path exists to guard synthetic or hand-crafted test tokens.
            return;
        }

        if (!realmAccess.TryGetProperty("roles", out var rolesEl)) return;

        foreach (var role in rolesEl.EnumerateArray())
        {
            var roleName = role.GetString();
            if (string.IsNullOrWhiteSpace(roleName)) continue;

            // Idempotency: skip if the claim already exists from a prior call.
            if (!identity.HasClaim(ClaimTypes.Role, roleName))
                identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
        }
    }

    private void AddClientRoles(ClaimsIdentity identity, ClaimsPrincipal source)
    {
        // resource_access JSON shape:
        // { "demo-api": { "roles": ["api-reader"] }, "account": { "roles": [...] } }
        var resourceAccessClaim = source.FindFirst("resource_access");
        if (resourceAccessClaim is null) return;

        JsonElement resourceAccess;
        try
        {
            resourceAccess = JsonDocument.Parse(resourceAccessClaim.Value).RootElement;
        }
        catch (JsonException)
        {
            return;
        }

        // Only project roles for our client — other clients' roles must not bleed
        // into this application's security context.
        if (!resourceAccess.TryGetProperty(_clientId, out var clientAccess)) return;
        if (!clientAccess.TryGetProperty("roles", out var rolesEl)) return;

        foreach (var role in rolesEl.EnumerateArray())
        {
            var roleName = role.GetString();
            if (string.IsNullOrWhiteSpace(roleName)) continue;

            if (!identity.HasClaim(ClaimTypes.Role, roleName))
                identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
        }
    }
}
