namespace GUML.Shared.Api.FrameworkPlugin;

/// <summary>
/// Provides framework-specific "pseudo-property" expansion.
/// Pseudo-properties are GUML syntactic sugar properties that expand into one or
/// more framework API method calls at code-generation time rather than simple
/// property assignments.
/// Example: Godot's <c>theme_overrides: { font_size: 14 }</c> expands to
/// <c>node.AddThemeFontSizeOverride("font_size", 14);</c>.
/// </summary>
public interface IFrameworkPseudoPropProvider
{
    /// <summary>
    /// Returns the set of property names (in snake_case, as they appear in GUML source)
    /// that are treated as pseudo-properties by this framework adapter.
    /// E.g. <c>{"theme_overrides"}</c> for Godot.
    /// </summary>
    ISet<string> PseudoPropertyNames { get; }

    /// <summary>
    /// Emits all generated C# statement lines for a pseudo-property assignment.
    /// </summary>
    /// <param name="varName">The local variable name of the target node (e.g. <c>"label_0"</c>).</param>
    /// <param name="componentTypeName">Simple type name of the component (e.g. <c>"Button"</c>).</param>
    /// <param name="propertyName">The snake_case pseudo-property name (e.g. <c>"theme_overrides"</c>).</param>
    /// <param name="valueNode">
    /// The raw AST expression node for the property value.
    /// Typed as <see cref="object"/> to avoid coupling the interface to specific AST types;
    /// the implementing adapter performs the necessary downcast.
    /// </param>
    /// <param name="emitExpression">
    /// Delegate that recursively emits a sub-expression node into its C# string form.
    /// </param>
    /// <param name="indent">The current indentation prefix string.</param>
    /// <returns>
    /// Zero or more generated statement lines (each a complete statement without trailing newline),
    /// or an empty list if this adapter does not handle the given pseudo-property.
    /// </returns>
    IReadOnlyList<string> EmitPseudoProperty(
        string varName,
        string componentTypeName,
        string propertyName,
        object valueNode,
        Func<object, string> emitExpression,
        string indent);
}
