namespace KeycloakDemo;

/// <summary>
/// Thrown when a JWT claim exists but contains data that cannot be parsed
/// (e.g. <c>realm_access</c> or <c>resource_access</c> is not valid JSON).
/// </summary>
/// <remarks>
/// Caught by the global exception handler and converted to 401 Unauthorized.
/// Throwing rather than returning lets the caller distinguish "claim absent"
/// (safe, skip) from "claim present but corrupt" (suspicious, reject).
/// </remarks>
public sealed class MalformedClaimException(string claimName, Exception innerException)
    : Exception($"Claim '{claimName}' contains invalid JSON.", innerException)
{
    public string ClaimName { get; } = claimName;
}
