using Microsoft.CodeAnalysis;

namespace GUML.SourceGenerator;

/// <summary>
/// Provides Roslyn-based type resolution for the source generator.
/// Used to resolve property types on Godot Control-derived components,
/// enabling zero-reflection binding code generation.
/// </summary>
internal sealed class CompilationApiScanner
{
    private readonly Compilation _compilation;
    private readonly INamedTypeSymbol? _controlBaseType;

    // Cache: componentName -> INamedTypeSymbol
    private readonly Dictionary<string, INamedTypeSymbol?> _typeCache = new();

    // ── Supported type sets (mirrors GUML.ApiGenerator.ApiModelBuilder) ──

    private static readonly HashSet<string> PrimitiveClrTypeNames = new(StringComparer.Ordinal)
    {
        nameof(Boolean),
        nameof(Int32),
        nameof(Int64),
        nameof(Single),
        nameof(Double),
        nameof(String)
    };

    private static readonly HashSet<string> GodotStructTypeNames = new(StringComparer.Ordinal)
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

    private static readonly HashSet<string> GodotReferenceTypeFullNames = new(StringComparer.Ordinal)
    {
        "Godot.NodePath",
        "Godot.StringName",
        "Godot.Collections.Array",
        "Godot.Collections.Dictionary"
    };

    private static readonly string[] GodotSupportedBaseTypes =
    {
        "Godot.Node",
        "Godot.Resource",
        "Godot.StyleBox",
        "Godot.Theme",
        "Godot.Font",
        "Godot.FontFile",
        "Godot.Texture",
        "Godot.Texture2D"
    };

    /// <summary>
    /// Creates a new scanner backed by the given Roslyn <see cref="Compilation"/>.
    /// The scanner will resolve the <c>Godot.Control</c> base type from the compilation's references.
    /// </summary>
    /// <param name="compilation">
    /// The Roslyn Compilation containing all referenced assemblies (including GodotSharp).
    /// </param>
    public CompilationApiScanner(Compilation compilation)
    {
        _compilation = compilation;
        _controlBaseType = compilation.GetTypeByMetadataName("Godot.Control");
    }

    /// <summary>
    /// Whether the scanner has a valid Control base type to scan against.
    /// Returns <c>false</c> if GodotSharp is not referenced in the compilation.
    /// </summary>
    public bool IsAvailable => _controlBaseType != null;

    // ── For source generator: single property type lookup ──

    /// <summary>
    /// Resolves the CLR type of a specific property on a component.
    /// Walks the type hierarchy to find the property, matching by PascalCase name.
    /// </summary>
    /// <param name="componentName">
    /// The simple class name of the component (e.g. "Label", "Button").
    /// </param>
    /// <param name="propertyName">
    /// The PascalCase property name to look up (e.g. "Text", "ClipText").
    /// </param>
    /// <returns>
    /// The <see cref="ITypeSymbol"/> of the property, or <c>null</c> if the component or property
    /// could not be found.
    /// </returns>
    public ITypeSymbol? ResolvePropertyType(string componentName, string propertyName)
    {
        var typeSymbol = ResolveComponentType(componentName);
        if (typeSymbol == null) return null;

        var current = typeSymbol;
        while (current != null)
        {
            foreach (var member in current.GetMembers(propertyName))
            {
                if (member is IPropertySymbol prop && prop.DeclaredAccessibility == Accessibility.Public)
                    return prop.Type;
                if (member is IFieldSymbol field && field.DeclaredAccessibility == Accessibility.Public
                    && !field.IsStatic && !field.IsConst)
                    return field.Type;
            }
            current = current.BaseType;
        }
        return null;
    }

    /// <summary>
    /// Checks if a type has a specific public property or field.
    /// </summary>
    public bool HasProperty(string typeName, string propertyName)
    {
        var typeSymbol = ResolveComponentType(typeName);
        if (typeSymbol == null) return false;

        var current = typeSymbol;
        while (current != null)
        {
            foreach (var member in current.GetMembers(propertyName))
            {
                if (member is IPropertySymbol prop && prop.DeclaredAccessibility == Accessibility.Public)
                    return true;
                if (member is IFieldSymbol field && field.DeclaredAccessibility == Accessibility.Public &&
                    !field.IsStatic && !field.IsConst)
                    return true;
            }
            current = current.BaseType;
        }
        return false;
    }

    /// <summary>
    /// Maps an <see cref="ITypeSymbol"/> to a C# cast/conversion expression string
    /// for use in zero-reflection setter code generation.
    /// </summary>
    /// <param name="typeSymbol">The resolved property type symbol.</param>
    /// <returns>
    /// A cast expression string like <c>"(string)"</c>, <c>"Convert.ToSingle"</c>, etc.,
    /// or <c>null</c> when the type is unknown (triggers reflection fallback).
    /// </returns>
    internal static string? GetCastExpression(ITypeSymbol typeSymbol)
    {
        switch (typeSymbol.SpecialType)
        {
            case SpecialType.System_String:
                return "Convert.ToString";
            case SpecialType.System_Boolean:
                return "Convert.ToBoolean";
            case SpecialType.System_Int32:
                return "Convert.ToInt32";
            case SpecialType.System_Int64:
                return "Convert.ToInt64";
            case SpecialType.System_Single:
                // Use Convert.ToSingle instead of (float) cast because boxed double
                // cannot be directly unboxed to float — would throw InvalidCastException.
                return "Convert.ToSingle";
            case SpecialType.System_Double:
                return "Convert.ToDouble";
        }

        if (typeSymbol.TypeKind == TypeKind.Enum)
        {
            string fullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return $"({fullName})";
        }

        // Godot struct types (Vector2, Color, etc.)
        if (typeSymbol.IsValueType && GodotStructTypeNames.Contains(typeSymbol.Name))
        {
            return $"({typeSymbol.Name})";
        }

        // Godot reference types (NodePath, StringName, etc.)
        string typeFullName = typeSymbol.ContainingNamespace?.ToDisplayString() + "." + typeSymbol.Name;
        if (GodotReferenceTypeFullNames.Contains(typeFullName))
        {
            return $"({typeSymbol.Name})";
        }

        // Godot node/resource types (check inheritance)
        foreach (string supportedBase in GodotSupportedBaseTypes)
        {
            var baseType = typeSymbol;
            while (baseType != null)
            {
                string baseFull = baseType.ContainingNamespace?.ToDisplayString() + "." + baseType.Name;
                if (baseFull == supportedBase)
                {
                    return $"({typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})";
                }
                baseType = baseType.BaseType;
            }
        }

        return null;
    }

    // ── Private helpers ──

    /// <summary>
    /// Resolves a component name (e.g. "Label") to its INamedTypeSymbol.
    /// Searches in the Godot namespace first, then in current compilation assembly, then all referenced assemblies.
    /// Results are cached for performance.
    /// </summary>
    private INamedTypeSymbol? ResolveComponentType(string componentName)
    {
        if (_typeCache.TryGetValue(componentName, out var cached))
            return cached;

        // Try Godot namespace first
        var symbol = _compilation.GetTypeByMetadataName("Godot." + componentName);
        if (symbol != null)
        {
            _typeCache[componentName] = symbol;
            return symbol;
        }

        // Search in current assembly first
        foreach (var type in GetAllNamedTypes(_compilation.Assembly.GlobalNamespace))
        {
            if (type.Name == componentName && type.DeclaredAccessibility == Accessibility.Public)
            {
                _typeCache[componentName] = type;
                return type;
            }
        }

        // Search all referenced assemblies for the type
        foreach (var reference in _compilation.References)
        {
            var assemblySymbol = _compilation.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol;
            if (assemblySymbol == null) continue;

            foreach (var type in GetAllNamedTypes(assemblySymbol.GlobalNamespace))
            {
                if (type.Name == componentName && type.DeclaredAccessibility == Accessibility.Public)
                {
                    _typeCache[componentName] = type;
                    return type;
                }
            }
        }

        _typeCache[componentName] = null;
        return null;
    }

    /// <summary>
    /// Recursively enumerates all named types within a namespace and its children.
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            yield return type;
        }
        foreach (var child in ns.GetNamespaceMembers())
        {
            foreach (var type in GetAllNamedTypes(child))
            {
                yield return type;
            }
        }
    }
}
