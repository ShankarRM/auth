using KeycloakDemo.Application.Abstractions;
using KeycloakDemo.Infrastructure.Auth;
using KeycloakDemo.Infrastructure.Http;
using KeycloakDemo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KeycloakDemo.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers all infrastructure services for the Web API host.
    /// Call this from the API's Program.cs.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Validates [Required] fields at host startup — throws before first request.
        services.AddOptions<KeycloakOptions>()
            .BindConfiguration("Keycloak")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // ── Authentication ─────────────────────────────────────────────────────
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Wires KeycloakOptions → JwtBearerOptions after startup validation passes.
        // Uses TokenValidationConfig.Build() which supports multi-client audiences.
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();

        // ── Authorization ──────────────────────────────────────────────────────
        services.AddAuthorization(AuthorizationPolicies.AddPolicies);

        // ── Auth services ──────────────────────────────────────────────────────

        // Scoped: reads HttpContext.User per-request. Singleton would capture
        // the first request's identity for the process lifetime.
        services.AddScoped<IClaimsTransformation, KeycloakRoleClaimsTransformation>();

        // IHttpContextAccessor is required by CurrentUserService.
        // AddHttpContextAccessor is idempotent — safe to call multiple times.
        services.AddHttpContextAccessor();

        // Scoped: CurrentUserService snapshots ClaimsPrincipal from HttpContext.
        services.AddScoped<ICurrentUser, CurrentUserService>();

        // ── Persistence ────────────────────────────────────────────────────────
        services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();

        return services;
    }

    /// <summary>
    /// Registers infrastructure services for the background Worker host.
    /// The worker authenticates as itself (Client Credentials) — there is no
    /// per-request HTTP context, no JWT validation, and no authorization middleware.
    /// </summary>
    /// <remarks>
    /// ICurrentUser is registered as Singleton here (not Scoped) because there is
    /// no request scope in a hosted service. The identity is fixed for the process
    /// lifetime — the worker always acts as the same service account.
    ///
    /// Switching between web and worker ICurrentUser is purely a DI wiring decision:
    /// - Web API  → AddInfrastructure()       → ICurrentUser = CurrentUserService (Scoped)
    /// - Worker   → AddWorkerInfrastructure() → ICurrentUser = ServiceAccountCurrentUser (Singleton)
    /// Application-layer use cases are identical — they see only ICurrentUser.
    /// </remarks>
    public static IServiceCollection AddWorkerInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Validate worker-specific Keycloak config at startup.
        services.AddOptions<KeycloakWorkerOptions>()
            .BindConfiguration("Keycloak")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // ── ICurrentUser ───────────────────────────────────────────────────────
        // Singleton: no request scope exists in a hosted service.
        services.AddSingleton<ICurrentUser, ServiceAccountCurrentUser>();

        // ── Token acquisition ──────────────────────────────────────────────────

        // Singleton: caches the Bearer token across the process lifetime.
        // SemaphoreSlim inside KeycloakTokenService guards concurrent refresh.
        services.AddSingleton<KeycloakTokenService>();

        // Transient: DelegatingHandlers MUST be Transient. The HttpClientFactory
        // manages handler pipelines internally — Singleton handlers cause shared-state bugs.
        services.AddTransient<AuthenticatedHttpClientHandler>();

        // ── Named HttpClient ───────────────────────────────────────────────────

        // "api-client" is used by the Worker to call GET /orders on the API.
        // AuthenticatedHttpClientHandler injects the Bearer token automatically.
        services.AddHttpClient("api-client", client =>
            {
                var baseUrl = configuration["Api:BaseUrl"]
                    ?? throw new InvalidOperationException("Api:BaseUrl is required in appsettings.");
                client.BaseAddress = new Uri(baseUrl);
            })
            .AddHttpMessageHandler<AuthenticatedHttpClientHandler>();

        // ── Persistence ────────────────────────────────────────────────────────
        // Worker uses the same in-memory store. In production, both host types
        // would share a real database instead.
        services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();

        return services;
    }
}
