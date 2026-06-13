using KeycloakDemo.Application.Abstractions;
using KeycloakDemo.Infrastructure.Auth;
using KeycloakDemo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KeycloakDemo.Infrastructure;

public static class DependencyInjection
{
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
        // Scoped lifetime matches the request scope of HttpContext.
        services.AddScoped<ICurrentUser, CurrentUserService>();

        // ── Persistence ────────────────────────────────────────────────────────

        // Singleton: in-memory data has no per-request state and is cheap to share.
        services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();

        return services;
    }
}
