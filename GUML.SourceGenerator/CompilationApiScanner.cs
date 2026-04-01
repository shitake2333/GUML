using Microsoft.CodeAnalysis;
using GUML.Shared.Api.FrameworkPlugin;
using GUML.SourceGenerator.FrameworkPlugin;

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
    private readonly IFrameworkCastProvider _castProvider;

    // Cache: componentName -> INamedTypeSymbol
    private readonly Dictionary<string, INamedTypeSymbol?> _typeCache = new();

    /// <summary>
    /// Creates a new scanner backed by the given Roslyn <see cref="Compilation"/>.
    /// The scanner will resolve the <c>Godot.Control</c> base type from the compilation's references.
    /// </summary>
    /// <param name="compilation">
    /// The Roslyn Compilation containing all referenced assemblies (including GodotSharp).
    /// </param>
    /// <param name="castProvider">
    /// The framework cast provider used to generate property cast expressions.
    /// Defaults to <see cref="GodotFrameworkPlugin.Instance"/> when not specified.
    /// </param>
    public CompilationApiScanner(Compilation compilation,
        IFrameworkCastProvider? castProvider = null)
    {
        _compilation = compilation;
        _controlBaseType = compilation.GetTypeByMetadataName("Godot.Control");
        _castProvider = castProvider ?? GodotFrameworkPlugin.Instance;
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
                if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public } prop)
                    return prop.Type;
                if (member is IFieldSymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: false, IsConst: false } field)
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
                if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public })
                    return true;
                if (member is IFieldSymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: false, IsConst: false })
                    return true;
            }
            current = current.BaseType;
        }
        return false;
    }

    /// <summary>
    /// Resolves the namespace of a component type by its simple name.
    /// Returns <c>null</c> if the type cannot be found or is in the global namespace.
    /// </summary>
    /// <param name="componentName">The simple class name (e.g. "NotebookViewer").</param>
    /// <returns>The fully-qualified namespace string, or <c>null</c>.</returns>
    public string? ResolveComponentNamespace(string componentName)
    {
        var typeSymbol = ResolveComponentType(componentName);
        if (typeSymbol?.ContainingNamespace is { IsGlobalNamespace: false } ns)
            return ns.ToDisplayString();
        return null;
    }

    /// <summary>
    /// Resolves the element type of a collection property on a given type.
    /// Supports <c>IList{T}</c>, <c>IEnumerable{T}</c>, <c>ObservableCollection{T}</c>, and array types.
    /// </summary>
    /// <param name="typeName">The class name containing the collection property.</param>
    /// <param name="propertyName">The PascalCase collection property name.</param>
    /// <returns>Fully qualified element type name, or <c>null</c> if unresolvable.</returns>
    public string? ResolveCollectionElementType(string typeName, string propertyName)
    {
        var propType = ResolvePropertyType(typeName, propertyName);
        if (propType == null) return null;

        // Array type: T[]
        if (propType is IArrayTypeSymbol arrayType)
            return arrayType.ElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // Generic type: IList<T>, ObservableCollection<T>, etc.
        if (propType is INamedTypeSymbol namedType)
        {
            // Direct generic arguments
            if (namedType.IsGenericType && namedType.TypeArguments.Length > 0)
                return namedType.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            // Walk interfaces for IEnumerable<T>
            foreach (var iface in namedType.AllInterfaces)
            {
                if (iface.IsGenericType &&
                    iface.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>" &&
                    iface.TypeArguments.Length > 0)
                {
                    return iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Maps an <see cref="ITypeSymbol"/> to a C# cast/conversion expression string
    /// for use in zero-reflection setter code generation.
    /// Delegates to the configured <see cref="IFrameworkCastProvider"/> after extracting
    /// the type metadata from the symbol.
    /// </summary>
    /// <param name="typeSymbol">The resolved property type symbol.</param>
    /// <returns>
    /// A cast expression string like <c>"(string)"</c>, <c>"Convert.ToSingle"</c>, etc.,
    /// or <c>null</c> when the type is unknown (triggers reflection fallback).
    /// </returns>
    internal string? GetCastExpression(ITypeSymbol typeSymbol)
    {
        string fullyQualifiedTypeName = typeSymbol.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat);

        bool isEnum = typeSymbol.TypeKind == TypeKind.Enum;
        bool isValueType = typeSymbol.IsValueType;

        // Collect base type fully-qualified names (without global:: prefix)
        var baseTypeFullNames = new List<string>();
        var current = typeSymbol.BaseType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            string ns = current.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            string baseFull = string.IsNullOrEmpty(ns)
                ? current.Name
                : ns + "." + current.Name;
            baseTypeFullNames.Add(baseFull);
            current = current.BaseType;
        }

        return _castProvider.GetCastExpression(fullyQualifiedTypeName, isEnum, isValueType,
            baseTypeFullNames);
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
