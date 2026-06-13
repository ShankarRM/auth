using System.Net.Http.Headers;

namespace KeycloakDemo.Infrastructure.Http;

/// <summary>
/// A <see cref="DelegatingHandler"/> that injects a Bearer token into every outbound
/// request made by the HttpClient it is attached to.
/// </summary>
/// <remarks>
/// Register this handler on any HttpClient that calls a protected API from a background
/// service. The token is fetched (or served from cache) by <see cref="KeycloakTokenService"/>
/// — callers never deal with token lifecycle.
///
/// WHY TRANSIENT:
/// DelegatingHandlers must be registered as Transient in the DI container. The
/// HttpClientFactory manages their lifetime internally — it creates a new handler
/// instance per HttpClient pipeline and pools the pipelines. If you register a
/// DelegatingHandler as Singleton, all HttpClient instances share state which causes
/// subtle bugs (stale cookies, captured HttpContext, etc.). Transient here is correct
/// even though it feels counter-intuitive given the Singleton KeycloakTokenService.
///
/// WHY NOT new HttpClient():
/// Never instantiate HttpClient directly — it does not participate in socket pooling
/// and causes socket exhaustion under load. Always use IHttpClientFactory.
/// </remarks>
public sealed class AuthenticatedHttpClientHandler(KeycloakTokenService tokenService)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await tokenService.GetAccessTokenAsync(cancellationToken);

        // Set — not Add — so a pre-existing header is replaced, not duplicated.
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
