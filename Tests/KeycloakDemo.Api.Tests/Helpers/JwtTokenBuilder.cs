using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace KeycloakDemo.Api.Tests.Helpers;

/// <summary>
/// Builds self-signed JWTs for integration tests without a real Keycloak instance.
/// </summary>
/// <remarks>
/// HOW TEST TOKENS ARE ACCEPTED:
/// The real API validates JWTs against Keycloak's public keys (fetched via OIDC discovery).
/// In tests, we replace that with a known RSA key via PostConfigure&lt;JwtBearerOptions&gt;
/// in <see cref="TestApiFactory"/>:
///   options.TokenValidationParameters.IssuerSigningKey = JwtTokenBuilder.SigningKey;
///   options.Authority = null;   // disables OIDC discovery
///   options.TokenValidationParameters.ValidateIssuer = false;
///
/// This lets us issue valid-looking JWTs with any claims we need to exercise
/// role-based and azp-based authorization paths — no network call required.
///
/// THREAD SAFETY:
/// SigningKey is a static readonly field initialized once. RSA key generation is
/// inherently thread-safe after creation.
/// </remarks>
public sealed class JwtTokenBuilder
{
    // Generated once per test session — shared across all JwtTokenBuilder instances.
    // Tests configure the API to accept signatures from this key via PostConfigure.
    private static readonly RsaSecurityKey _signingKey = CreateSigningKey();

    public static RsaSecurityKey SigningKey => _signingKey;

    // Defaults match the API's primary configuration (appsettings.json in tests).
    private string _audience = "dotnet-api";
    private string _issuer   = "http://localhost:9093/realms/demo";
    private string _subject  = "test-user-id";
    private string _azp      = "dotnet-api";
    private readonly List<string> _roles = [];

    public JwtTokenBuilder WithAudience(string audience) { _audience = audience; return this; }
    public JwtTokenBuilder WithIssuer(string issuer)     { _issuer   = issuer;   return this; }
    public JwtTokenBuilder WithSubject(string subject)   { _subject  = subject;  return this; }

    // "azp" identifies the client that requested the token.
    // For human users via web-frontend: azp = "web-frontend"
    // For the background worker:        azp = "background-worker"
    public JwtTokenBuilder WithAzp(string azp) { _azp = azp; return this; }

    public JwtTokenBuilder WithRole(string role) { _roles.Add(role); return this; }

    public string Build()
    {
        var claims = new List<Claim>
        {
            new("sub",                _subject),
            new("azp",                _azp),
            new("preferred_username", _azp == "background-worker"
                ? $"service-account-{_azp}"
                : "testuser"),
        };

        // ClaimTypes.Role is what KeycloakRoleClaimsTransformation writes and
        // what RequireRole() / IsInRole() check. Use the same claim type here
        // so that authorization policies evaluate correctly in tests.
        foreach (var role in _roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer:             _issuer,
            audience:           _audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow.AddMinutes(-1),
            expires:            DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static RsaSecurityKey CreateSigningKey()
    {
        // RSA 2048-bit key — sufficient for test purposes. The private key is
        // embedded in the key object and used for signing; the public key is used
        // for verification when the test host validates the token.
        var rsa = RSA.Create(2048);
        return new RsaSecurityKey(rsa);
    }
}
