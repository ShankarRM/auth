using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace KeycloakDemo.Infrastructure.Auth;

internal sealed class ConfigureJwtBearerOptions(IOptions<KeycloakOptions> kc)
    : IConfigureOptions<JwtBearerOptions>
{
    public void Configure(JwtBearerOptions options)
    {
        var o = kc.Value;

        options.Authority            = o.Authority;
        options.RequireHttpsMetadata = o.RequireHttpsMetadata;

        // Do NOT set options.Audience here — setting it would override
        // TokenValidationParameters.ValidAudiences with a single value,
        // breaking multi-client audience validation.
        options.MapInboundClaims = false;

        // Multi-audience validation: TokenValidationConfig explains the
        // two approaches and why ValidAudiences (Approach A) is used here.
        options.TokenValidationParameters = TokenValidationConfig.Build(o);
    }
}
