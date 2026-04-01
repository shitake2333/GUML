using GUML.Shared.Api;
using Microsoft.CodeAnalysis;

namespace GUML.Analyzer.FrameworkPlugin;

/// <summary>
/// Abstraction for framework-specific Analyzer behaviors.
/// Implementations provide Godot-specific (or other framework) type scanning logic.
/// </summary>
public interface IGumlApiPlugin
{
    /// <summary>
    /// Fully-qualified type name of the root base type for component scanning.
    /// E.g. "Godot.Control" for Godot.
    /// </summary>
    string RootBaseTypeName { get; }

    /// <summary>
    /// Name of the framework assembly (used to identify the SDK in the compilation).
    /// E.g. "GodotSharp" for Godot.
    /// </summary>
    string FrameworkAssemblyName { get; }

    /// <summary>
    /// Returns true if the given type is a supported property type for framework components.
    /// Used to filter which properties appear in the API document.
    /// </summary>
    bool IsTypeSupported(ITypeSymbol type);

    /// <summary>
    /// Scans a type symbol for framework-native signal/event declarations and
    /// populates <paramref name="typeDesc"/> with discovered events.
    /// </summary>
    void ScanSignalsOnType(INamedTypeSymbol type, TypeDescriptor typeDesc);

    /// <summary>
    /// Returns true if the named property on the named type has a known framework
    /// change observable (e.g. a Godot signal that fires on value change).
    /// </summary>
    bool IsObservableProperty(string typeName, string propertyName);

    /// <summary>
    /// Returns the set of pseudo-property names (in snake_case) that are valid
    /// GUML syntactic sugar. These expand into framework API method calls during
    /// code generation rather than simple property assignments.
    /// E.g. <c>{"theme_overrides"}</c> for Godot.
    /// </summary>
    IReadOnlySet<string> PseudoPropertyNames { get; }

    /// <summary>
    /// Returns descriptions for pseudo-properties, keyed by name.
    /// Used when constructing the initial <see cref="PropertyDescriptor"/> for each
    /// pseudo-property during type building.
    /// </summary>
    IReadOnlyDictionary<string, string> PseudoPropertyDescriptions { get; }

    /// <summary>
    /// Enriches the <see cref="ApiDocument"/> with framework-specific synthetic types and
    /// additional property metadata. Called after all types have been scanned and
    /// descriptions merged. Implementations may create synthetic types (e.g. per-component
    /// theme override types) and inject additional properties into existing types.
    /// </summary>
    /// <param name="doc">The API document to enrich.</param>
    void EnrichApiDocument(ApiDocument doc);
}
