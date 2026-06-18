using System.Security.Claims;
using KeycloakDemo;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────────

// Bind appsettings "Keycloak" section → KeycloakOptions.
// ValidateDataAnnotations checks [Required] fields.
// ValidateOnStart throws before the host accepts any request — missing config is
// a deployment error caught at startup, not a 500 at runtime.
builder.Services.AddOptions<KeycloakOptions>()
    .BindConfiguration("Keycloak")       // reads appsettings.json → Keycloak:{Authority,Audience,...}
    .ValidateDataAnnotations()           // enforces [Required] on KeycloakOptions properties
    .ValidateOnStart();                  // fails immediately at host start, not on first request

// ── Authentication ─────────────────────────────────────────────────────────────

var kc = builder.Configuration.GetSection("Keycloak");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority            = kc["Authority"];
        options.Audience             = kc["Audience"];
        options.RequireHttpsMetadata = kc.GetValue<bool>("RequireHttpsMetadata", true);
        options.MapInboundClaims     = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer   = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            NameClaimType    = "preferred_username",
            RoleClaimType    = ClaimTypes.Role,
        };
    });

// ── OpenAPI ────────────────────────────────────────────────────────────────────

// Registers the built-in .NET 9 OpenAPI spec generator (produces /openapi/v1.json).
// The document transformer runs once at spec generation time — not per request.
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((doc, _, _) =>
    {
        // Set human-readable metadata shown in the Scalar UI header.
        doc.Info.Title   = "Keycloak Demo API";
        doc.Info.Version = "v1";
        doc.Info.Description =
            "Part 2: IClaimsTransformation & Domain Roles. " +
            "Obtain a Bearer token via Keycloak and paste it using the Authorize button.";

        // Declare the "Bearer" security scheme so Scalar renders the Authorize button.
        // Without this entry under Components.SecuritySchemes, the UI has no way to
        // collect or send an Authorization header.
        doc.Components ??= new();
        doc.Components.SecuritySchemes ??= new Dictionary<string, Microsoft.OpenApi.Models.OpenApiSecurityScheme>();
        doc.Components.SecuritySchemes["Bearer"] = new()
        {
            Type         = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme       = "bearer",   // lowercase required by OpenAPI spec
            BearerFormat = "JWT",
            Description  = "Paste the access_token from Keycloak (without the 'Bearer ' prefix).",
        };

        // Apply the Bearer scheme to every endpoint globally.
        // Endpoints decorated with [AllowAnonymous] still appear but are exempt at runtime.
        doc.SecurityRequirements ??= [];
        doc.SecurityRequirements.Add(new()
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    // Reference by Id so this points to the scheme declared above.
                    Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                [] // empty scopes list — JWT Bearer doesn't use OAuth scopes at this level
            }
        });

        return Task.CompletedTask;
    });
});

// ── Authorization ──────────────────────────────────────────────────────────────

// Register named policies (ReadAccess, AdminAccess, AuditAccess).
// AuthorizationPolicies.AddPolicies is an Action<AuthorizationOptions> defined
// in AuthorizationPolicies.cs.
builder.Services.AddAuthorization(AuthorizationPolicies.AddPolicies);

// IClaimsTransformation runs once per authentication event (not per request).
// KeycloakRoleClaimsTransformation reads realm/client roles from the Keycloak
// JWT and maps them to ClaimTypes.Role so IsInRole() and policy checks work.
// Scoped (not Singleton) because a future version will need IHttpContextAccessor,
// which is scoped — avoiding a captive-dependency bug before it arises.
builder.Services.AddScoped<IClaimsTransformation, KeycloakRoleClaimsTransformation>();

// ── Build ──────────────────────────────────────────────────────────────────────

// Finalises the DI container and creates the WebApplication.
// ValidateOnStart fires here — bad config throws before any middleware runs.
var app = builder.Build();

// ── Middleware pipeline ────────────────────────────────────────────────────────

// Only expose OpenAPI spec and Scalar UI in Development.
// The spec reveals endpoint shapes and security schemes — not safe on production
// without explicit opt-in.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                   // → GET /openapi/v1.json  (raw spec)

    app.MapScalarApiReference(options =>
    {
        options.Title             = "Keycloak Demo API";
        options.Theme             = ScalarTheme.Purple;
        options.DefaultHttpClient = new(ScalarTarget.Http, ScalarClient.Http11);
    });                                 // → GET /scalar/v1  (interactive UI)
}

// ORDER MATTERS: Authentication must run before Authorization.
// UseAuthentication decodes the Bearer token, validates it, populates
// HttpContext.User, and runs IClaimsTransformation (adds domain roles).
// UseAuthorization then evaluates policies against that populated identity.
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ─────────────────────────────────────────────────────────────────
app.MapHealthEndpoints();    // GET /health            — anonymous liveness probe
app.MapIdentityEndpoints();  // GET /me                — returns claims from JWT
app.MapOrderEndpoints();     // GET /orders            — requires api-reader or api-admin
app.MapAdminEndpoints();     // GET /admin/users       — requires api-admin
                             // GET /audit/logs        — requires api-admin + audit.read scope

app.Run();
