using System.Security.Claims;
using System.Text.Json;
using KeycloakDemo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KeycloakDemo.Tests;

public class KeycloakRoleClaimsTransformationTests
{
    private const string ClientId = "demo-api";

    private readonly KeycloakRoleClaimsTransformation _sut;

    public KeycloakRoleClaimsTransformationTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Keycloak:Audience"] = ClientId,
            })
            .Build();

        _sut = new KeycloakRoleClaimsTransformation(config, NullLogger<KeycloakRoleClaimsTransformation>.Instance);
    }

    [Fact]
    public async Task TransformAsync_RealmRole_AddsRoleClaim()
    {
        var principal = BuildPrincipal(realmRoles: ["api-admin"]);

        var result = await _sut.TransformAsync(principal);

        Assert.Contains(result.Claims,
            c => c.Type == ClaimTypes.Role && c.Value == "api-admin");
    }

    [Fact]
    public async Task TransformAsync_ClientRole_AddsRoleClaim()
    {
        var principal = BuildPrincipal(clientRoles: ["api-reader"]);

        var result = await _sut.TransformAsync(principal);

        Assert.Contains(result.Claims,
            c => c.Type == ClaimTypes.Role && c.Value == "api-reader");
    }

    [Fact]
    public async Task TransformAsync_CalledTwice_DoesNotDuplicateClaims()
    {
        var principal = BuildPrincipal(realmRoles: ["api-admin"]);

        // First call — adds the role claim.
        var firstResult = await _sut.TransformAsync(principal);

        // Second call with the already-transformed principal — idempotency check.
        // realm_access is still present on firstResult (it was cloned); the
        // transformer must detect the existing role claim and not add a duplicate.
        var secondResult = await _sut.TransformAsync(firstResult);

        var adminClaims = secondResult.Claims
            .Where(c => c.Type == ClaimTypes.Role && c.Value == "api-admin")
            .ToList();

        Assert.Single(adminClaims);
    }

    [Fact]
    public async Task TransformAsync_NoRealmAccessClaim_DoesNotThrow()
    {
        // Principal with no Keycloak role claims at all.
        var identity  = new ClaimsIdentity([new Claim("sub", "anonymous")], "test");
        var principal = new ClaimsPrincipal(identity);

        var result = await _sut.TransformAsync(principal);

        // No exception, and no spurious role claims added.
        Assert.DoesNotContain(result.Claims, c => c.Type == ClaimTypes.Role);
    }

    [Fact]
    public async Task TransformAsync_InvalidRealmAccessJson_ThrowsMalformedClaimException()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim("sub", "user-1"),
                new Claim("realm_access", "NOT-VALID-JSON"),
            ],
            "test");
        var principal = new ClaimsPrincipal(identity);

        var ex = await Assert.ThrowsAsync<MalformedClaimException>(
            () => _sut.TransformAsync(principal));

        Assert.Equal("realm_access", ex.ClaimName);
    }

    [Fact]
    public async Task TransformAsync_InvalidResourceAccessJson_ThrowsMalformedClaimException()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim("sub", "user-1"),
                new Claim("resource_access", "NOT-VALID-JSON"),
            ],
            "test");
        var principal = new ClaimsPrincipal(identity);

        var ex = await Assert.ThrowsAsync<MalformedClaimException>(
            () => _sut.TransformAsync(principal));

        Assert.Equal("resource_access", ex.ClaimName);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static ClaimsPrincipal BuildPrincipal(
        string[]? realmRoles  = null,
        string[]? clientRoles = null)
    {
        var claims = new List<Claim> { new("sub", "test-user") };

        if (realmRoles is { Length: > 0 })
        {
            // Mirror the exact JSON shape Keycloak puts in the JWT.
            var json = JsonSerializer.Serialize(new { roles = realmRoles });
            claims.Add(new Claim("realm_access", json));
        }

        if (clientRoles is { Length: > 0 })
        {
            // resource_access is a dictionary keyed by client ID.
            var json = JsonSerializer.Serialize(
                new Dictionary<string, object>
                {
                    [ClientId] = new { roles = clientRoles },
                });
            claims.Add(new Claim("resource_access", json));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
    }
}
