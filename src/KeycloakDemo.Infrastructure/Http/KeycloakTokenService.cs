using System.Net.Http.Json;
using System.Text.Json.Serialization;
using KeycloakDemo.Infrastructure.Auth;
using Microsoft.Extensions.Options;

namespace KeycloakDemo.Infrastructure.Http;

/// <summary>
/// Fetches and caches OAuth2 access tokens for the background worker using the
/// Client Credentials flow.
/// </summary>
/// <remarks>
/// WHY CACHE:
/// Keycloak issues tokens with a finite lifetime (default 5 minutes for service accounts).
/// Fetching a new token on every HTTP call to the API adds latency and hammers the
/// Keycloak token endpoint unnecessarily. We cache the token and refresh it 30 seconds
/// before expiry to handle clock skew and brief network delays.
///
/// WHY SINGLETON + SemaphoreSlim:
/// KeycloakTokenService is registered as Singleton so the token cache lives for the
/// process lifetime. When the token expires, multiple concurrent callers (e.g. tasks
/// in a parallel background service) could all see a stale token simultaneously and
/// all attempt to refresh it. The SemaphoreSlim(1,1) ensures only one refresh happens
/// at a time — the "double-checked lock" pattern prevents redundant refreshes once the
/// first caller repopulates the cache.
/// </remarks>
public sealed class KeycloakTokenService : IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly KeycloakWorkerOptions _options;

    // Allow only one token refresh at a time across concurrent callers.
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    public KeycloakTokenService(
        IHttpClientFactory httpClientFactory,
        IOptions<KeycloakWorkerOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    /// <summary>Returns a valid Bearer token, fetching a new one only when needed.</summary>
    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        // Fast path: no lock needed if the cached token is still valid.
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
            return _cachedToken;

        await _lock.WaitAsync(ct);
        try
        {
            // Double-check: another caller may have refreshed the token while we waited.
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
                return _cachedToken;

            (_cachedToken, _tokenExpiry) = await FetchTokenAsync(ct);
            return _cachedToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<(string Token, DateTimeOffset Expiry)> FetchTokenAsync(CancellationToken ct)
    {
        // Use a plain, unnamed HttpClient — we are not calling the protected API here,
        // we are calling Keycloak's public token endpoint which needs no Bearer header.
        var client = _httpClientFactory.CreateClient();

        var tokenEndpoint = $"{_options.Authority.TrimEnd('/')}/protocol/openid-connect/token";

        using var response = await client.PostAsync(
            tokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"]    = "client_credentials",
                ["client_id"]     = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
            }),
            ct);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty token response from Keycloak.");

        // Subtract 30 seconds so the token is refreshed before Keycloak rejects it.
        var expiry = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn - 30);
        return (payload.AccessToken, expiry);
    }

    public void Dispose() => _lock.Dispose();

    // Private DTO — not part of public API surface.
    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}
