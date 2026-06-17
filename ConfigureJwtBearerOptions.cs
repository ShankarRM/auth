using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace KeycloakDemo;

// IConfigureOptions<T> only runs for the default (unnamed) options instance.
// JwtBearerHandler resolves options by scheme name ("Bearer"), so IConfigureOptions
// is silently skipped. IConfigureNamedOptions is required to reach named schemes.
internal sealed class ConfigureJwtBearerOptions(IOptions<KeycloakOptions> kc)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(string? name, JwtBearerOptions options)
    {
        // Only configure the scheme we own — guard against cross-scheme pollution
        // if multiple JWT schemes are registered.
        if (name != JwtBearerDefaults.AuthenticationScheme) return;
        Configure(options);
    }

    public void Configure(JwtBearerOptions options)
    {
        var o = kc.Value;

        options.Authority            = o.Authority;
        options.Audience             = o.Audience;
        options.RequireHttpsMetadata = o.RequireHttpsMetadata;
        options.MapInboundClaims     = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer   = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            NameClaimType    = "preferred_username",
            RoleClaimType    = ClaimTypes.Role,
        };
    }
}
