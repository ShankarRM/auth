using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;

namespace KeycloakDemo.Infrastructure.Auth;

/// <summary>
/// Infrastructure concern — Application layer never references this class directly.
/// Maps Keycloak's nested role structure into standard <see cref="ClaimTypes.Role"/>
/// claims so that policy checks and <see cref="ClaimsPrincipal.IsInRole"/> work correctly.
/// </summary>
/// <remarks>
/// Keycloak encodes roles in two nested structures:
/// <list type="bullet">
///   <item><c>realm_access.roles</c> — realm-level roles, apply to all clients</item>
///   <item><c>resource_access.{clientId}.roles</c> — roles scoped to one client</item>
/// </list>
/// </remarks>
internal sealed class KeycloakRoleClaimsTransformation(IConfiguration configuration)
    : IClaimsTransformation
{
    private readonly string _clientId = configuration["Keycloak:Audience"]
        ?? throw new InvalidOperationException("Keycloak:Audience is required.");

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var cloned   = principal.Clone();
        var identity = (ClaimsIdentity)cloned.Identity!;

        AddRealmRoles(identity, principal);
        AddClientRoles(identity, principal);

        return Task.FromResult(cloned);
    }

    private static void AddRealmRoles(ClaimsIdentity identity, ClaimsPrincipal source)
    {
        var claim = source.FindFirst("realm_access");
        if (claim is null) return;

        JsonElement root;
        try   { root = JsonDocument.Parse(claim.Value).RootElement; }
        catch (JsonException) { return; }

        if (!root.TryGetProperty("roles", out var roles)) return;

        foreach (var role in roles.EnumerateArray())
        {
            var name = role.GetString();
            if (!string.IsNullOrWhiteSpace(name) && !identity.HasClaim(ClaimTypes.Role, name))
                identity.AddClaim(new Claim(ClaimTypes.Role, name));
        }
    }

    private void AddClientRoles(ClaimsIdentity identity, ClaimsPrincipal source)
    {
        var claim = source.FindFirst("resource_access");
        if (claim is null) return;

        JsonElement root;
        try   { root = JsonDocument.Parse(claim.Value).RootElement; }
        catch (JsonException) { return; }

        if (!root.TryGetProperty(_clientId, out var client)) return;
        if (!client.TryGetProperty("roles", out var roles)) return;

        foreach (var role in roles.EnumerateArray())
        {
            var name = role.GetString();
            if (!string.IsNullOrWhiteSpace(name) && !identity.HasClaim(ClaimTypes.Role, name))
                identity.AddClaim(new Claim(ClaimTypes.Role, name));
        }
    }
}
