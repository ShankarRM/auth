using System.Security.Claims;
using KeycloakDemo;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────────
var kc = builder.Configuration.GetSection("Keycloak");

// Fail fast: missing config is a deployment error, not a runtime edge case.
var authority    = kc["Authority"]
    ?? throw new InvalidOperationException("Keycloak:Authority is required in appsettings.");
var audience     = kc["Audience"]
    ?? throw new InvalidOperationException("Keycloak:Audience is required in appsettings.");
var requireHttps = kc.GetValue<bool>("RequireHttpsMetadata", defaultValue: true);

// ── Authentication ─────────────────────────────────────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority            = authority;
        options.Audience             = audience;
        options.RequireHttpsMetadata = requireHttps;

        // MapInboundClaims: false — preserves raw OIDC claim names (sub, email,
        // preferred_username) instead of renaming them to verbose WS-Federation URIs.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer   = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            NameClaimType    = "preferred_username",

            // Tell the framework which claim type holds role values. Must match
            // what KeycloakRoleClaimsTransformation writes so that IsInRole() and
            // policy RequireRole() checks find the right claims.
            RoleClaimType = ClaimTypes.Role,
        };
    });

// ── OpenAPI ────────────────────────────────────────────────────────────────────
builder.Services.AddOpenApi(options =>
{
    // Inject a Bearer security scheme so Scalar shows the "Authorize" button.
    // Without this, the UI renders but every protected endpoint returns 401 with no way to auth.
    options.AddDocumentTransformer((doc, _, _) =>
    {
        doc.Info.Title   = "Keycloak Demo API";
        doc.Info.Version = "v1";
        doc.Info.Description =
            "Part 2: IClaimsTransformation & Domain Roles. " +
            "Obtain a Bearer token via Keycloak and paste it using the Authorize button.";

        doc.Components ??= new();
        doc.Components.SecuritySchemes ??= new Dictionary<string, Microsoft.OpenApi.Models.OpenApiSecurityScheme>();
        doc.Components.SecuritySchemes["Bearer"] = new()
        {
            Type        = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme      = "bearer",
            BearerFormat = "JWT",
            Description = "Paste the access_token from Keycloak (without the 'Bearer ' prefix).",
        };

        // Apply the scheme globally — every endpoint requires auth unless it has AllowAnonymous.
        doc.SecurityRequirements ??= [];
        doc.SecurityRequirements.Add(new()
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                []
            }
        });

        return Task.CompletedTask;
    });
});

// ── Authorization ──────────────────────────────────────────────────────────────
builder.Services.AddAuthorization(AuthorizationPolicies.AddPolicies);

// Scoped, not Singleton: IClaimsTransformation runs per-request inside the
// authentication middleware. A future extension (Part 3) will inject
// IHttpContextAccessor, which is inherently scoped — registering as Scoped now
// avoids a captive-dependency bug before it can happen.
builder.Services.AddScoped<IClaimsTransformation, KeycloakRoleClaimsTransformation>();

var app = builder.Build();

// Catches MalformedClaimException thrown by IClaimsTransformation and returns 401.
// Must be registered before UseAuthentication so it wraps the auth middleware pipeline.
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    var feature = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
    if (feature?.Error is MalformedClaimException)
    {
        ctx.Response.StatusCode  = StatusCodes.Status401Unauthorized;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(
            """{"error":"invalid_token","error_description":"A JWT claim contained malformed data."}""");
        return;
    }
    ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
}));

// Serves the raw OpenAPI JSON spec consumed by Scalar.
// Restrict to Development — the spec reveals endpoint names, parameter shapes, and
// security schemes that should not be exposed on production without explicit intent.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();  // → GET /openapi/v1.json

    app.MapScalarApiReference(options =>
    {
        options.Title            = "Keycloak Demo API";
        options.Theme            = ScalarTheme.Purple;
        options.DefaultHttpClient = new(ScalarTarget.Http, ScalarClient.Http11);
    });  // → GET /scalar/v1
}

// UseAuthentication must precede UseAuthorization. The authentication middleware
// decodes the Bearer token, populates HttpContext.User, and runs IClaimsTransformation.
// The authorization middleware then evaluates policies against that populated identity.
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ─────────────────────────────────────────────────────────────────

// Anonymous liveness probe — load balancers and k8s probes call this without a token.
app.MapGet("/health", () => Results.Ok("OK"))
   .AllowAnonymous()
   .WithName("Health")
   .WithTags("System")
   .WithSummary("Liveness probe — no token required.");

// Returns identity claims from the validated JWT. Any valid token is sufficient.
app.MapGet("/me", (HttpContext ctx) =>
{
    var sub      = ctx.User.FindFirst("sub")?.Value;
    var username = ctx.User.FindFirst("preferred_username")?.Value;
    var email    = ctx.User.FindFirst("email")?.Value;

    return Results.Ok(new { sub, username, email });
})
.RequireAuthorization()
.WithName("GetMe")
.WithTags("Identity")
.WithSummary("Returns sub, preferred_username, and email from the validated JWT.");

// Requires Reader or Admin role (ReadAccess policy). A valid JWT with no
// matching role still gets a 403 — Part 1's "any token" rule is now tightened.
app.MapGet("/orders", () =>
{
    var orders = new[]
    {
        new { Id = 1, Item = "Widget A", Quantity = 3, Status = "Shipped"   },
        new { Id = 2, Item = "Widget B", Quantity = 1, Status = "Pending"   },
        new { Id = 3, Item = "Widget C", Quantity = 7, Status = "Delivered" },
    };

    return Results.Ok(orders);
})
.RequireAuthorization(AuthorizationPolicies.ReadAccess)
.WithName("GetOrders")
.WithTags("Orders")
.WithSummary("List orders. Requires api-reader or api-admin role.");

// Admin-only: list all users. Reader and Supervisor tokens → 403.
app.MapGet("/admin/users", () =>
{
    var users = new[]
    {
        new { Id = "u1", Username = "alice", Roles = new[] { "api-admin"  } },
        new { Id = "u2", Username = "bob",   Roles = new[] { "api-reader" } },
    };

    return Results.Ok(users);
})
.RequireAuthorization(AuthorizationPolicies.AdminAccess)
.WithName("GetAdminUsers")
.WithTags("Admin")
.WithSummary("List all users. Requires api-admin role.");

// Admin + audit.read scope: demonstrates that role alone is not always enough.
// An Admin without the audit.read scope in their token still gets 403 here.
app.MapGet("/audit/logs", () =>
{
    var logs = new[]
    {
        new { Timestamp = "2024-01-15T10:00:00Z", Action = "USER_CREATED",  Actor = "alice" },
        new { Timestamp = "2024-01-15T11:23:00Z", Action = "ROLE_ASSIGNED", Actor = "alice" },
    };

    return Results.Ok(logs);
})
.RequireAuthorization(AuthorizationPolicies.AuditAccess)
.WithName("GetAuditLogs")
.WithTags("Audit")
.WithSummary("List audit logs. Requires api-admin role AND audit.read scope.");

app.Run();
