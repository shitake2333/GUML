using System;
using System.Collections.Generic;

namespace GUML.Shared;

/// <summary>
/// Root model for the GUML API cache. Contains metadata about the scanned SDK version
/// and a dictionary of all discovered Control-derived class definitions.
/// </summary>
public class GumlApiCache
{
    /// <summary>
    /// The version identifier of the scanned SDK (e.g. "4.6.1") or "compilation" for
    /// API caches generated from a Roslyn Compilation.
    /// </summary>
    public string Version { get; set; } = "";

    /// <summary>
    /// Timestamp when this cache was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// Dictionary of class name → class info for all discovered Control subclasses.
    /// </summary>
    public Dictionary<string, GumlClassInfo> Classes { get; set; } = new();
}

/// <summary>
/// Describes a single Godot Control-derived class, including its name,
/// base type, documentation, and public properties.
/// </summary>
public class GumlClassInfo
{
    /// <summary>
    /// The simple class name (e.g. "Label", "Button").
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// The simple name of the base type (e.g. "Control", "BaseButton").
    /// </summary>
    public string? BaseType { get; set; }

    /// <summary>
    /// XML documentation summary for this class.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Dictionary of property name (snake_case) → property info for all supported public properties.
    /// </summary>
    public Dictionary<string, GumlPropertyInfo> Properties { get; set; } = new();
}

/// <summary>
/// Describes a single property on a Godot Control-derived class.
/// </summary>
public class GumlPropertyInfo
{
    /// <summary>
    /// The property name in snake_case (e.g. "text", "clip_text").
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// The CLR type name of the property (e.g. "String", "Boolean", "Vector2").
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    /// XML documentation summary for this property.
    /// </summary>
    public string Description { get; set; } = "";
}
