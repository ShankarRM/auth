using System.ComponentModel.DataAnnotations;

namespace KeycloakDemo;

public sealed class KeycloakOptions
{
    [Required(AllowEmptyStrings = false)]
    public string Authority { get; init; } = null!;

    [Required(AllowEmptyStrings = false)]
    public string Audience { get; init; } = null!;

    public bool RequireHttpsMetadata { get; init; } = true;
}
