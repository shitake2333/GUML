namespace GUML.Shared;

/// <summary>
/// Centralised catalogue of Godot type names used to determine whether a given
/// property type is supported for GUML binding / code generation.
/// Both <c>GUML.Analyzer</c> and <c>GUML.SourceGenerator</c> reference these
/// sets so that the definition lives in exactly one place.
/// </summary>
public static class GodotTypeConstants
{
    /// <summary>
    /// Short names of Godot value-type (struct) types that are valid property types.
    /// </summary>
    public static readonly HashSet<string> StructTypeNames = new(StringComparer.Ordinal)
    {
        "Vector2",
        "Vector2I",
        "Vector3",
        "Vector3I",
        "Vector4",
        "Vector4I",
        "Rect2",
        "Rect2I",
        "Transform2D",
        "Transform3D",
        "Color",
        "Plane",
        "Quaternion",
        "Basis",
        "Rid",
        "Aabb",
        "Projection",
        "Margins"
    };

    /// <summary>
    /// Fully-qualified names of Godot reference types that are valid property types
    /// but are not derived from <c>Godot.Node</c> or <c>Godot.Resource</c>.
    /// </summary>
    public static readonly HashSet<string> ReferenceTypeFullNames = new(StringComparer.Ordinal)
    {
        "Godot.NodePath",
        "Godot.StringName",
        "Godot.Collections.Array",
        "Godot.Collections.Dictionary"
    };

    /// <summary>
    /// Fully-qualified base type names whose descendants are considered
    /// valid component / property types in GUML.
    /// </summary>
    public static readonly string[] SupportedBaseTypes =
    [
        "Godot.Node",
        "Godot.Resource",
        "Godot.StyleBox",
        "Godot.Theme",
        "Godot.Font",
        "Godot.FontFile",
        "Godot.Texture",
        "Godot.Texture2D"
    ];
}
