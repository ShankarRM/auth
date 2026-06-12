using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────────
var kc = builder.Configuration.GetSection("Keycloak");

// Fail fast: missing config is a deployment error, not a runtime edge case.
var authority = kc["Authority"]
    ?? throw new InvalidOperationException("Keycloak:Authority is required in appsettings.");
var audience = kc["Audience"]
    ?? throw new InvalidOperationException("Keycloak:Audience is required in appsettings.");
var requireHttps = kc.GetValue<bool>("RequireHttpsMetadata", defaultValue: true);

// ── Authentication ────────────────────────────────────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience  = audience;

        // The middleware fetches Keycloak's OIDC discovery document from
        // {Authority}/.well-known/openid-configuration. Setting RequireHttpsMetadata
        // to false is safe only in local dev; production should always be true.
        options.RequireHttpsMetadata = requireHttps;

        // MapInboundClaims: false — without this, JwtBearer silently renames
        // standard OIDC claims to verbose WS-Federation URIs. "sub" becomes
        // "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
        // "email" becomes a different URI, etc. Reading them as raw OIDC names
        // (sub, preferred_username, email) makes the code portable across IdPs
        // and matches what's actually in the JWT.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer   = true,  // Reject tokens not issued by this Keycloak realm
            ValidateAudience = true,  // Reject tokens not scoped to this API's client ID
            ValidateLifetime = true,  // Reject expired tokens

            // Tell the framework which raw claim represents the user's name.
            // Relevant for User.Identity.Name and logging — no effect on RequireAuthorization().
            NameClaimType = "preferred_username",

            // RoleClaimType intentionally left at default here. Role-based
            // authorization requires IClaimsTransformation to flatten Keycloak's
            // nested role structure — covered in Part 2.
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// UseAuthentication must precede UseAuthorization. The authentication middleware
// decodes the Bearer token and populates HttpContext.User. The authorization
// middleware then reads HttpContext.User to evaluate policies. If the order is
// reversed, every request is evaluated as an anonymous user regardless of the token.
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ─────────────────────────────────────────────────────────────────

// Anonymous liveness probe — load balancers and k8s call this without a token.
app.MapGet("/health", () => Results.Ok("OK"))
   .AllowAnonymous();

// Returns identity claims from the validated JWT. RequireAuthorization() uses
// the default policy: the request must carry a valid, non-expired Bearer token.
app.MapGet("/me", (HttpContext ctx) =>
{
    var sub      = ctx.User.FindFirst("sub")?.Value;
    var username = ctx.User.FindFirst("preferred_username")?.Value;
    var email    = ctx.User.FindFirst("email")?.Value;

    return Results.Ok(new { sub, username, email });
})
.RequireAuthorization();

// Hardcoded orders list — no persistence layer in Part 1.
app.MapGet("/orders", () =>
{
    var orders = new[]
    {
        new { Id = 1, Item = "Widget A", Quantity = 3, Status = "Shipped"  },
        new { Id = 2, Item = "Widget B", Quantity = 1, Status = "Pending"  },
        new { Id = 3, Item = "Widget C", Quantity = 7, Status = "Delivered" },
    };

    return Results.Ok(orders);
})
.RequireAuthorization();

app.Run();
