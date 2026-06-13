using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace KeycloakDemo.Infrastructure.Auth;

internal sealed class ConfigureJwtBearerOptions(IOptions<KeycloakOptions> kc)
    : IConfigureOptions<JwtBearerOptions>
{
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
