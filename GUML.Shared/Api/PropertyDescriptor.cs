namespace GUML.Shared.Api;

/// <summary>
/// Describes a single property on a component type (§11.1.2).
/// </summary>
public sealed class PropertyDescriptor
{
    /// <summary>
    /// The property name (e.g. "text", "clip_text").
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// The canonical CLR type name (e.g. "System.String", "System.Boolean").
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    /// Documentation summary for this property.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Whether the property can be read.
    /// </summary>
    public bool IsReadable { get; set; }

    /// <summary>
    /// Whether the property can be written.
    /// </summary>
    public bool IsWritable { get; set; }

    /// <summary>
    /// Enum member info, present only when the property type is an enum.
    /// </summary>
    public List<EnumValueDescriptor>? EnumValues { get; set; }

    /// <summary>
    /// Mapping constraint metadata describing which binding operators are supported.
    /// </summary>
    public MappingConstraintDescriptor Mapping { get; set; } = new();
}
