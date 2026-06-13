namespace KeycloakDemo.Domain.Entities;

/// <summary>Pure domain entity — no auth attributes, no framework annotations.</summary>
public sealed class Order
{
    public int    Id          { get; init; }
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// The <c>sub</c> claim of the user who created this order.
    /// Stored as an opaque string — the domain does not parse or interpret JWT claims.
    /// </summary>
    public string CreatedBy   { get; init; } = string.Empty;
}
