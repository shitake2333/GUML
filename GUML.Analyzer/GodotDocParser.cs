using System.Text.RegularExpressions;
using System.Xml.Linq;
using Serilog;

namespace GUML.Analyzer;

/// <summary>
/// Parses Godot XML class documentation files (doc/classes/*.xml) and extracts
/// API metadata including properties, signals, theme overrides, enums, and methods.
/// </summary>
internal static class GodotDocParser
{
    /// <summary>
    /// Maps XML <c>theme_item.data_type</c> values to GUML ThemeOverrides category names.
    /// </summary>
    private static readonly Dictionary<string, string> s_dataTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["color"] = "Color",
        ["constant"] = "Constant",
        ["font"] = "Font",
        ["font_size"] = "FontSize",
        ["icon"] = "Icon",
        ["style"] = "Style",
    };

    /// <summary>
    /// Parses all XML documentation files in the given directory and returns a <see cref="GodotApiDocument"/>.
    /// </summary>
    /// <param name="classesDir">Path to the <c>doc/classes</c> directory.</param>
    /// <param name="godotVersion">The Godot version string to embed in the document.</param>
    public static GodotApiDocument ParseDirectory(string classesDir, string godotVersion)
    {
        var doc = new GodotApiDocument
        {
            GodotVersion = godotVersion,
            GeneratedAt = DateTime.UtcNow,
        };

        string[] xmlFiles;
        try
        {
            xmlFiles = Directory.GetFiles(classesDir, "*.xml");
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Failed to enumerate XML files in {Dir}", classesDir);
            return doc;
        }

        Log.Logger.Information("Parsing {Count} XML files from {Dir}", xmlFiles.Length, classesDir);

        foreach (string xmlPath in xmlFiles)
        {
            try
            {
                var classInfo = ParseClassFile(xmlPath);
                if (classInfo != null)
                {
                    string className = Path.GetFileNameWithoutExtension(xmlPath);
                    doc.Classes[className] = classInfo;
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Failed to parse {File}, skipping", xmlPath);
            }
        }

        Log.Logger.Information("Parsed {Count} classes", doc.Classes.Count);
        return doc;
    }

    /// <summary>
    /// Parses a single XML class documentation file.
    /// </summary>
    internal static GodotClassInfo? ParseClassFile(string xmlPath)
    {
        var xdoc = XDocument.Load(xmlPath);
        var classElem = xdoc.Root;
        if (classElem == null || classElem.Name.LocalName != "class")
            return null;

        var info = new GodotClassInfo
        {
            Inherits = classElem.Attribute("inherits")?.Value,
            BriefDescription = CleanDescription(
                classElem.Element("brief_description")?.Value),
        };

        // Parse members (properties)
        var membersElem = classElem.Element("members");
        if (membersElem != null)
        {
            foreach (var member in membersElem.Elements("member"))
            {
                info.Properties.Add(ParseProperty(member));
            }
        }

        // Parse signals
        var signalsElem = classElem.Element("signals");
        if (signalsElem != null)
        {
            foreach (var signal in signalsElem.Elements("signal"))
            {
                info.Signals.Add(ParseSignal(signal));
            }
        }

        // Parse theme items
        var themeItemsElem = classElem.Element("theme_items");
        if (themeItemsElem != null)
        {
            foreach (var themeItem in themeItemsElem.Elements("theme_item"))
            {
                var ov = ParseThemeOverride(themeItem);
                if (ov != null) info.ThemeOverrides.Add(ov);
            }
        }

        // Parse constants → group into enums
        var constantsElem = classElem.Element("constants");
        if (constantsElem != null)
        {
            var enumGroups = new Dictionary<string, GodotEnumInfo>(StringComparer.Ordinal);

            foreach (var constant in constantsElem.Elements("constant"))
            {
                string? enumName = constant.Attribute("enum")?.Value;
                if (string.IsNullOrEmpty(enumName)) continue;

                if (!enumGroups.TryGetValue(enumName, out var enumInfo))
                {
                    enumInfo = new GodotEnumInfo { Name = enumName };
                    enumGroups[enumName] = enumInfo;
                }

                enumInfo.Values.Add(new GodotEnumValueInfo
                {
                    Name = constant.Attribute("name")?.Value ?? "",
                    Value = constant.Attribute("value")?.Value ?? "0",
                    Description = CleanDescription(constant.Value),
                });
            }

            info.Enums.AddRange(enumGroups.Values);
        }

        // Parse methods
        var methodsElem = classElem.Element("methods");
        if (methodsElem != null)
        {
            foreach (var method in methodsElem.Elements("method"))
            {
                info.Methods.Add(ParseMethod(method));
            }
        }

        return info;
    }

    private static GodotPropertyInfo ParseProperty(XElement member)
    {
        return new GodotPropertyInfo
        {
            Name = member.Attribute("name")?.Value ?? "",
            Type = member.Attribute("type")?.Value ?? "",
            Setter = NullIfEmpty(member.Attribute("setter")?.Value),
            Getter = NullIfEmpty(member.Attribute("getter")?.Value),
            Default = NullIfEmpty(member.Attribute("default")?.Value),
            Description = CleanDescription(member.Value),
            Enum = NullIfEmpty(member.Attribute("enum")?.Value),
            IsBitfield = string.Equals(
                member.Attribute("is_bitfield")?.Value, "true",
                StringComparison.OrdinalIgnoreCase),
        };
    }

    private static GodotSignalInfo ParseSignal(XElement signal)
    {
        var info = new GodotSignalInfo
        {
            Name = signal.Attribute("name")?.Value ?? "",
            Description = CleanDescription(signal.Element("description")?.Value),
        };

        foreach (var param in signal.Elements("param"))
        {
            info.Parameters.Add(new GodotParameterInfo
            {
                Name = param.Attribute("name")?.Value ?? "",
                Type = param.Attribute("type")?.Value ?? "",
            });
        }

        return info;
    }

    private static GodotThemeOverrideInfo? ParseThemeOverride(XElement themeItem)
    {
        string? xmlDataType = themeItem.Attribute("data_type")?.Value;
        if (string.IsNullOrEmpty(xmlDataType)) return null;

        if (!s_dataTypeMap.TryGetValue(xmlDataType, out string? mappedDataType))
        {
            Log.Logger.Debug("Unknown theme_item data_type: {DataType}", xmlDataType);
            mappedDataType = xmlDataType;
        }

        return new GodotThemeOverrideInfo
        {
            Name = themeItem.Attribute("name")?.Value ?? "",
            DataType = mappedDataType,
            Type = themeItem.Attribute("type")?.Value ?? "",
            Default = NullIfEmpty(themeItem.Attribute("default")?.Value),
            Description = CleanDescription(themeItem.Value),
        };
    }

    private static GodotMethodInfo ParseMethod(XElement method)
    {
        var returnElem = method.Element("return");
        string returnType = returnElem?.Attribute("type")?.Value ?? "void";
        string? returnEnum = NullIfEmpty(returnElem?.Attribute("enum")?.Value);

        var info = new GodotMethodInfo
        {
            Name = method.Attribute("name")?.Value ?? "",
            ReturnType = returnType,
            ReturnEnum = returnEnum,
            Description = CleanDescription(method.Element("description")?.Value),
            Qualifiers = NullIfEmpty(method.Attribute("qualifiers")?.Value),
        };

        foreach (var param in method.Elements("param"))
        {
            info.Parameters.Add(new GodotParameterInfo
            {
                Name = param.Attribute("name")?.Value ?? "",
                Type = param.Attribute("type")?.Value ?? "",
            });
        }

        return info;
    }

    /// <summary>
    /// Strips BBCode-like tags and normalizes whitespace in Godot doc descriptions.
    /// </summary>
    private static string? CleanDescription(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        // Remove Godot BBCode tags like [code], [b], [param], [constant], etc.
        string cleaned = Regex.Replace(text, @"\[/?[a-z_]+(?:\s[^\]]*?)?\]", "");
        // Collapse whitespace
        cleaned = Regex.Replace(cleaned.Trim(), @"\s+", " ");
        return string.IsNullOrEmpty(cleaned) ? null : cleaned;
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrEmpty(value) ? null : value;
}
