using System.Text.Json.Serialization;

namespace GUML.Analyzer;

/// <summary>
/// Root document for serialized Godot API metadata extracted from XML documentation.
/// </summary>
internal sealed class GodotApiDocument
{
    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("godot_version")]
    public string GodotVersion { get; set; } = "";

    [JsonPropertyName("generated_at")]
    public DateTime GeneratedAt { get; set; }

    [JsonPropertyName("classes")]
    public Dictionary<string, GodotClassInfo> Classes { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Describes a single Godot class extracted from XML documentation.
/// </summary>
internal sealed class GodotClassInfo
{
    [JsonPropertyName("inherits")]
    public string? Inherits { get; set; }

    [JsonPropertyName("brief_description")]
    public string? BriefDescription { get; set; }

    [JsonPropertyName("properties")]
    public List<GodotPropertyInfo> Properties { get; set; } = [];

    [JsonPropertyName("signals")]
    public List<GodotSignalInfo> Signals { get; set; } = [];

    [JsonPropertyName("theme_overrides")]
    public List<GodotThemeOverrideInfo> ThemeOverrides { get; set; } = [];

    [JsonPropertyName("enums")]
    public List<GodotEnumInfo> Enums { get; set; } = [];

    [JsonPropertyName("methods")]
    public List<GodotMethodInfo> Methods { get; set; } = [];
}

/// <summary>
/// Describes a property on a Godot class.
/// </summary>
internal sealed class GodotPropertyInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("setter")]
    public string? Setter { get; set; }

    [JsonPropertyName("getter")]
    public string? Getter { get; set; }

    [JsonPropertyName("default")]
    public string? Default { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("enum")]
    public string? Enum { get; set; }

    [JsonPropertyName("is_bitfield")]
    public bool IsBitfield { get; set; }
}

/// <summary>
/// Describes a signal on a Godot class.
/// </summary>
internal sealed class GodotSignalInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("parameters")]
    public List<GodotParameterInfo> Parameters { get; set; } = [];
}

/// <summary>
/// Describes a theme override entry on a Godot class.
/// </summary>
internal sealed class GodotThemeOverrideInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    /// The GUML theme override category: Color, Constant, Font, FontSize, Icon, Style.
    /// </summary>
    [JsonPropertyName("data_type")]
    public string DataType { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("default")]
    public string? Default { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// Describes a parameter of a signal or method.
/// </summary>
internal sealed class GodotParameterInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
}

/// <summary>
/// Describes an enum declared within a Godot class.
/// </summary>
internal sealed class GodotEnumInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("values")]
    public List<GodotEnumValueInfo> Values { get; set; } = [];
}

/// <summary>
/// Describes a single enum value.
/// </summary>
internal sealed class GodotEnumValueInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// Describes a method on a Godot class.
/// </summary>
internal sealed class GodotMethodInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("return_type")]
    public string ReturnType { get; set; } = "";

    [JsonPropertyName("return_enum")]
    public string? ReturnEnum { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("qualifiers")]
    public string? Qualifiers { get; set; }

    [JsonPropertyName("parameters")]
    public List<GodotParameterInfo> Parameters { get; set; } = [];
}
