namespace GUML.Shared.Api;

/// <summary>
/// Describes a single component or struct type discovered from the Godot SDK or user code (§11.1.2).
/// </summary>
public sealed class TypeDescriptor
{
    /// <summary>
    /// The simple class name (e.g. "Label", "Button").
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// The fully qualified type name (e.g. "Godot.Label"). Must uniquely identify the type.
    /// </summary>
    public string QualifiedName { get; set; } = "";

    /// <summary>
    /// Whether this type is a class or a struct.
    /// </summary>
    public GumlTypeKind Kind { get; set; }

    /// <summary>
    /// The simple name of the base type, or null for root types.
    /// </summary>
    public string? BaseType { get; set; }

    /// <summary>
    /// Documentation summary for this type.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Dictionary of property name → property descriptor for all supported properties.
    /// </summary>
    public Dictionary<string, PropertyDescriptor> Properties { get; set; } = new();

    /// <summary>
    /// Dictionary of event/signal name → event descriptor for all declared events.
    /// </summary>
    public Dictionary<string, EventDescriptor> Events { get; set; } = new();
}

/// <summary>
/// Classifies a type as a class or a struct.
/// </summary>
public enum GumlTypeKind
{
    /// <summary>A reference type (class).</summary>
    Class,

    /// <summary>A value type (struct).</summary>
    Struct,

    /// <summary>An interface type.</summary>
    Interface,

    /// <summary>An enumeration type.</summary>
    Enum
}
