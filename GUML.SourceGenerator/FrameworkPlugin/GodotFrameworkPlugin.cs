using GUML.Shared;
using GUML.Shared.Api.FrameworkPlugin;
using GUML.Shared.Converter;
using GUML.Shared.Syntax.Nodes.Expressions;

namespace GUML.SourceGenerator.FrameworkPlugin;

/// <summary>
/// Godot framework plugin for the GUML source generator.
/// Provides all Godot-specific code generation behaviors: type shorthands,
/// event subscription, theme override pseudo-properties, and property cast expressions.
/// </summary>
internal sealed class GodotFrameworkPlugin
    : IFrameworkTypeProvider, IFrameworkEventProvider, IFrameworkPseudoPropProvider, IFrameworkCastProvider
{
    /// <summary>Shared singleton instance.</summary>
    public static readonly GodotFrameworkPlugin Instance = new();

    private GodotFrameworkPlugin() { }

    // ── Type shorthand maps ──

    private static readonly Dictionary<string, string> s_shorthands = new()
    {
        { "vec2",              "Vector2"       },
        { "vec2i",             "Vector2I"      },
        { "vec3",              "Vector3"       },
        { "vec3i",             "Vector3I"      },
        { "vec4",              "Vector4"       },
        { "vec4i",             "Vector4I"      },
        { "rect2",             "Rect2"         },
        { "rect2i",            "Rect2I"        },
        { "color",             "Color"         },
        { "style_box_empty",   "StyleBoxEmpty" },
        { "style_box_flat",    "StyleBoxFlat"  },
        { "style_box_line",    "StyleBoxLine"  },
        { "style_box_texture", "StyleBoxTexture" },
    };

    // ──────────────────────────────────────────────────────
    // IFrameworkTypeProvider
    // ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public string? ResolveTypeShorthand(string gumlName)
        => s_shorthands.TryGetValue(gumlName, out string? t) ? t : null;

    /// <inheritdoc/>
    public IReadOnlyList<string> GetRequiredUsings() => ["Godot"];

    /// <inheritdoc/>
    public string? EmitShorthandConstruction(string typeName, IReadOnlyList<string>? args)
    {
        if (!s_shorthands.TryGetValue(typeName, out string? csharpType))
            return null;

        if (args == null)
        {
            // Zero-value expression (no parens in GUML source).
            return typeName switch
            {
                "vec2"   => "Vector2.Zero",
                "vec2i"  => "Vector2I.Zero",
                "vec3"   => "Vector3.Zero",
                "vec3i"  => "Vector3I.Zero",
                "vec4"   => "Vector4.Zero",
                "vec4i"  => "Vector4I.Zero",
                "rect2"  => "new Rect2()",
                "rect2i" => "new Rect2I()",
                "color"  => "new Color()",
                _        => $"new {csharpType}()"
            };
        }

        // Explicit constructor call, including empty-args case.
        return $"new {csharpType}({string.Join(", ", args)})";
    }

    /// <inheritdoc/>
    public string? EmitShorthandNamedConstruction(string typeName, IReadOnlyList<(string Key, string Value)> namedArgs)
    {
        if (!s_shorthands.TryGetValue(typeName, out string? csharpType))
            return null;

        if (namedArgs.Count == 0)
            return $"new {csharpType}()";

        // StyleBox types get special shorthand expansion.
        if (csharpType.StartsWith("StyleBox", StringComparison.Ordinal))
            return EmitStyleBoxFromNamedArgs(csharpType, namedArgs);

        // All other shorthands: generic object initializer with PascalCase keys.
        string props = string.Join(", ", namedArgs.Select(p => $"{KeyConverter.ToPascalCase(p.Key)} = {p.Value}"));
        return $"new {csharpType}() {{ {props} }}";
    }

    private static string EmitStyleBoxFromNamedArgs(
        string csharpType, IReadOnlyList<(string Key, string Value)> namedArgs)
    {
        var props = new List<string>(namedArgs.Count * 2);
        foreach (var (key, val) in namedArgs)
        {
            if (csharpType == "StyleBoxFlat")
            {
                string keyLower = key.Replace("_", "").ToLowerInvariant();

                if (keyLower == "borderwidth")
                {
                    props.Add($"BorderWidthLeft = {val}");
                    props.Add($"BorderWidthTop = {val}");
                    props.Add($"BorderWidthRight = {val}");
                    props.Add($"BorderWidthBottom = {val}");
                    continue;
                }
                if (keyLower == "contentmargin")
                {
                    props.Add($"ContentMarginLeft = {val}");
                    props.Add($"ContentMarginTop = {val}");
                    props.Add($"ContentMarginRight = {val}");
                    props.Add($"ContentMarginBottom = {val}");
                    continue;
                }
                if (keyLower == "cornerradius")
                {
                    props.Add($"CornerRadiusTopLeft = {val}");
                    props.Add($"CornerRadiusTopRight = {val}");
                    props.Add($"CornerRadiusBottomRight = {val}");
                    props.Add($"CornerRadiusBottomLeft = {val}");
                    continue;
                }
            }

            props.Add($"{KeyConverter.ToPascalCase(key)} = {val}");
        }

        return $"new {csharpType}() {{ {string.Join(", ", props)} }}";
    }

    // ──────────────────────────────────────────────────────
    // IFrameworkEventProvider
    // ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>
    /// For the Godot framework, all signals that do not appear in a GUML
    /// <c>&lt;events&gt;</c> block are treated as native Godot signals and thus
    /// subscribed directly on the node variable.
    /// </remarks>
    public bool IsNativeEvent(string componentTypeName, string signalName) => true;

    /// <inheritdoc/>
    public string EmitEventSubscription(string varName, string signalName, string handlerExpr)
        => $"{varName}.{signalName} += {handlerExpr};";

    // ──────────────────────────────────────────────────────
    // IFrameworkPseudoPropProvider
    // ──────────────────────────────────────────────────────

    private static readonly ISet<string> s_pseudoPropertyNames =
        new HashSet<string>(StringComparer.Ordinal) { "theme_overrides" };

    /// <inheritdoc/>
    public ISet<string> PseudoPropertyNames => s_pseudoPropertyNames;

    /// <inheritdoc/>
    public IReadOnlyList<string> EmitPseudoProperty(
        string varName,
        string componentTypeName,
        string propertyName,
        object valueNode,
        Func<object, string> emitExpression,
        string indent)
    {
        if (propertyName != "theme_overrides")
            return Array.Empty<string>();

        var lines = new List<string>();
        EmitThemeOverridesInternal(lines, varName, valueNode, emitExpression, indent);
        return lines;
    }

    private static void EmitThemeOverridesInternal(
        List<string> lines, string varName, object valueNode,
        Func<object, string> emitExpression, string indent)
    {
        if (valueNode is not ObjectLiteralExpressionSyntax objLit)
            return;

        foreach (var prop in objLit.Properties)
        {
            // Recursively unpack nested category objects.
            if (prop.Value is ObjectLiteralExpressionSyntax nestedObj)
            {
                EmitThemeOverridesInternal(lines, varName, nestedObj, emitExpression, indent);
                continue;
            }

            string overrideName = KeyConverter.FromCamelCase(prop.Name.Text);
            string valueExpr = emitExpression(prop.Value);
            lines.Add($"{indent}// ThemeOverride: {overrideName}");

            switch (prop.Value)
            {
                case StructExpressionSyntax { TypeName.Text: "color" }:
                    lines.Add($"{indent}{varName}.AddThemeColorOverride(\"{overrideName}\", {valueExpr});");
                    break;

                case ResourceExpressionSyntax { Keyword.Kind: Shared.Syntax.SyntaxKind.FontKeyword }:
                    lines.Add($"{indent}{varName}.AddThemeFontOverride(\"{overrideName}\", {valueExpr});");
                    break;

                case ResourceExpressionSyntax { Keyword.Kind: Shared.Syntax.SyntaxKind.ImageKeyword }:
                    lines.Add($"{indent}{varName}.AddThemeIconOverride(\"{overrideName}\", {valueExpr});");
                    break;

                case ObjectCreationExpressionSyntax objCreate when
                    objCreate.TypeName.Text.StartsWith("StyleBox", StringComparison.Ordinal):
                    lines.Add($"{indent}{varName}.AddThemeStyleboxOverride(\"{overrideName}\", {valueExpr});");
                    break;

                case LiteralExpressionSyntax { Token.Kind: Shared.Syntax.SyntaxKind.IntegerLiteralToken or Shared.Syntax.SyntaxKind.FloatLiteralToken }:
                    if (overrideName.IndexOf("font_size", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (overrideName.IndexOf("size", StringComparison.OrdinalIgnoreCase) >= 0 &&
                         !overrideName.Contains("separation")))
                        lines.Add($"{indent}{varName}.AddThemeFontSizeOverride(\"{overrideName}\", Convert.ToInt32({valueExpr}));");
                    else
                        lines.Add($"{indent}{varName}.AddThemeConstantOverride(\"{overrideName}\", Convert.ToInt32({valueExpr}));");
                    break;

                default:
                    lines.Add($"{indent}// Fallback: emit constant override");
                    lines.Add($"{indent}{varName}.AddThemeConstantOverride(\"{overrideName}\", Convert.ToInt32({valueExpr}));");
                    break;
            }
        }
    }

    // ──────────────────────────────────────────────────────
    // IFrameworkCastProvider
    // ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public string? GetCastExpression(
        string fullyQualifiedTypeName, bool isEnum, bool isValueType,
        IReadOnlyList<string> baseTypeFullNames)
    {
        // Normalise: strip "global::" prefix for lookup comparisons.
        string normalized = fullyQualifiedTypeName.StartsWith("global::", StringComparison.Ordinal)
            ? fullyQualifiedTypeName.Substring(8)
            : fullyQualifiedTypeName;

        // CLR primitive types — match both "System.X" and keyword alias form ("string", "int", …).
        switch (normalized)
        {
            case "System.String":
            case "string":
                return "Convert.ToString";
            case "System.Boolean":
            case "bool":
                return "Convert.ToBoolean";
            case "System.Int32":
            case "int":
                return "Convert.ToInt32";
            case "System.Int64":
            case "long":
                return "Convert.ToInt64";
            // Use Convert.ToSingle instead of (float) cast: boxed double cannot be
            // directly unboxed to float and would throw InvalidCastException.
            case "System.Single":
            case "float":
                return "Convert.ToSingle";
            case "System.Double":
            case "double":
                return "Convert.ToDouble";
        }

        // Enum: emit a fully-qualified C-style cast.
        if (isEnum)
            return $"({fullyQualifiedTypeName})";

        // Extract short name (last segment after '.').
        int lastDot = normalized.LastIndexOf('.');
        string shortName = lastDot >= 0 ? normalized.Substring(lastDot + 1) : normalized;

        // Godot value-type (struct) shorthand.
        if (isValueType && GodotTypeConstants.StructTypeNames.Contains(shortName))
            return $"({shortName})";

        // Godot reference types that don't derive from Node/Resource.
        if (GodotTypeConstants.ReferenceTypeFullNames.Contains(normalized))
            return $"({shortName})";

        // Godot node/resource hierarchy: check if any ancestor is a supported base.
        foreach (string supportedBase in GodotTypeConstants.SupportedBaseTypes)
        {
            foreach (string baseName in baseTypeFullNames)
            {
                if (string.Equals(baseName, supportedBase, StringComparison.Ordinal))
                    return $"({fullyQualifiedTypeName})";
            }
        }

        return null;
    }
}
