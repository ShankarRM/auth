using System.ComponentModel.DataAnnotations;

namespace KeycloakDemo.Infrastructure.Auth;

public sealed class KeycloakOptions
{
    [Required(AllowEmptyStrings = false)]
    public string Authority { get; init; } = null!;

    // The primary audience — typically the API's own client_id (e.g. "dotnet-api").
    // Tokens issued directly to this API have "dotnet-api" in their "aud" claim.
    [Required(AllowEmptyStrings = false)]
    public string Audience { get; init; } = null!;

    // Additional audiences the API should accept — e.g. "web-frontend".
    // A token issued to web-frontend has "web-frontend" in its "aud" claim; the API
    // must recognise it as valid. See TokenValidationConfig for the full explanation.
    // In appsettings.json: "AdditionalAudiences": ["web-frontend"]
    public string[] AdditionalAudiences { get; init; } = [];

    public bool RequireHttpsMetadata { get; init; } = true;
}
