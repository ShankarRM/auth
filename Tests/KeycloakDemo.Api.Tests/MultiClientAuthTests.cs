using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using KeycloakDemo.Api.Tests.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace KeycloakDemo.Api.Tests;

// ── Test factory ───────────────────────────────────────────────────────────────

/// <summary>
/// Boots the full API in-process and overrides JWT validation to accept
/// test-issued tokens signed with <see cref="JwtTokenBuilder.SigningKey"/>.
/// </summary>
public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Supply the Keycloak config that ValidateOnStart requires — no real
        // Keycloak needed, we just need to satisfy [Required] validation.
        builder.UseSetting("Keycloak:Authority", "http://localhost:9093/realms/demo");
        builder.UseSetting("Keycloak:Audience",  "dotnet-api");
        builder.UseSetting("Keycloak:AdditionalAudiences:0", "web-frontend");
        builder.UseSetting("Keycloak:RequireHttpsMetadata", "false");

        builder.ConfigureTestServices(services =>
        {
            // Replace the production JWT validation (Keycloak OIDC discovery + public keys)
            // with test-key-based validation. PostConfigure runs AFTER all other
            // IConfigureOptions, so it wins over ConfigureJwtBearerOptions.
            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    // Disable OIDC discovery — Authority lookup would fail without Keycloak.
                    options.Authority = null;
                    options.RequireHttpsMetadata = false;

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey         = JwtTokenBuilder.SigningKey,
                        ValidateIssuer           = false,
                        ValidateAudience         = true,

                        // Mirror the production audience list from appsettings.
                        ValidAudiences = ["dotnet-api", "web-frontend"],

                        ValidateLifetime = true,
                        RoleClaimType    = ClaimTypes.Role,
                        NameClaimType    = "preferred_username",
                    };
                });
        });
    }
}

// ── Tests ──────────────────────────────────────────────────────────────────────

public sealed class MultiClientAuthTests(TestApiFactory factory)
    : IClassFixture<TestApiFactory>
{
    // Helper: build an HttpClient with a Bearer token already attached.
    private HttpClient ClientWithToken(string jwt)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", jwt);
        return client;
    }

    [Fact]
    public async Task HumanUser_WithReaderRole_CanCallGetOrders()
    {
        // A user authenticated via the dotnet-api client with the api-reader role.
        var token = new JwtTokenBuilder()
            .WithAudience("dotnet-api")
            .WithAzp("dotnet-api")
            .WithRole("api-reader")
            .Build();

        var response = await ClientWithToken(token).GetAsync("/orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ServiceAccount_WithReaderRole_CanCallGetOrdersService()
    {
        // The background-worker authenticates via Client Credentials.
        // Its token has azp = "background-worker" and the api-reader role.
        var token = new JwtTokenBuilder()
            .WithAudience("dotnet-api")
            .WithAzp("background-worker")
            .WithRole("api-reader")
            .Build();

        var response = await ClientWithToken(token).GetAsync("/orders/service");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HumanUser_IsRejected_OnGetOrdersService()
    {
        // A human user token has azp = "web-frontend", not "background-worker".
        // The ServiceAccountOnly policy checks azp — this must be rejected with 403.
        var token = new JwtTokenBuilder()
            .WithAudience("dotnet-api")
            .WithAzp("web-frontend")
            .WithRole("api-reader")
            .Build();

        var response = await ClientWithToken(token).GetAsync("/orders/service");

        // 403 (not 401): the token is valid and authenticated, but authorization fails.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task WebFrontendAudienceToken_IsAccepted_OnGetOrders()
    {
        // A token issued to the web-frontend public client has aud = "web-frontend".
        // The API must accept it because "web-frontend" is in AdditionalAudiences.
        // This test validates Approach A (ValidAudiences list) from TokenValidationConfig.
        var token = new JwtTokenBuilder()
            .WithAudience("web-frontend")
            .WithAzp("web-frontend")
            .WithRole("api-reader")
            .Build();

        var response = await ClientWithToken(token).GetAsync("/orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NoToken_Returns401_OnGetOrders()
    {
        var response = await factory.CreateClient().GetAsync("/orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReaderRoleToken_IsRejected_OnGetOrdersService_WhenAzpIsNotWorker()
    {
        // Even with the correct role, a non-worker azp must not reach /orders/service.
        var token = new JwtTokenBuilder()
            .WithAudience("dotnet-api")
            .WithAzp("dotnet-api")   // human user going directly through dotnet-api client
            .WithRole("api-reader")
            .Build();

        var response = await ClientWithToken(token).GetAsync("/orders/service");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
