namespace GUML.Shared.Api;

/// <summary>
/// Describes an enum member value (§11.1.2).
/// </summary>
public sealed class EnumValueDescriptor
{
    /// <summary>
    /// The enum member name in PascalCase (e.g. "Center", "Fill").
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// The underlying numeric value as a string (e.g. "4").
    /// </summary>
    public string Value { get; set; } = "";

    /// <summary>
    /// Documentation summary for this enum member.
    /// </summary>
    public string? Description { get; set; }
}
