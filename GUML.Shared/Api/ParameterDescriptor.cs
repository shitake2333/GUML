namespace GUML.Shared.Api;

/// <summary>
/// Describes a parameter of an event or method (§11.1.2).
/// </summary>
public sealed class ParameterDescriptor
{
    /// <summary>
    /// The parameter name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// The canonical CLR type name of the parameter.
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    /// The XML documentation summary of the parameter, if any.
    /// </summary>
    public string? Description { get; set; }
}
