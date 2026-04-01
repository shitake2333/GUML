namespace GUML.Shared.Api.FrameworkPlugin;

/// <summary>
/// Provides framework-specific type shorthand information to the source generator.
/// Maps GUML shorthand names (e.g. "vec2") to framework types (e.g. "Vector2")
/// and generates corresponding constructor expressions in emitted code.
/// </summary>
public interface IFrameworkTypeProvider
{
    /// <summary>
    /// Maps a GUML shorthand function name to its framework type name.
    /// E.g. "vec2" -&gt; "Vector2", "color" -&gt; "Color".
    /// Returns <c>null</c> if the name is not a known shorthand.
    /// </summary>
    string? ResolveTypeShorthand(string gumlName);

    /// <summary>
    /// Returns the list of namespace <c>using</c> directives to inject into
    /// every generated file for this framework.
    /// E.g. <c>["Godot"]</c> for the Godot adapter.
    /// </summary>
    IReadOnlyList<string> GetRequiredUsings();

    /// <summary>
    /// Given a shorthand type name and positional arguments (as already-emitted expression strings),
    /// returns the framework-specific constructor expression.
    /// E.g. "vec2", ["10", "20"] -&gt; "new Vector2(10, 20)".
    /// </summary>
    /// <param name="typeName">The GUML shorthand name (e.g. "vec2", "color").</param>
    /// <param name="args">
    /// Already-emitted positional argument strings.
    /// Pass <c>null</c> to request the type's canonical zero-value expression
    /// (e.g. "Vector2.Zero" for "vec2"). Pass an empty list to emit a default
    /// constructor call (e.g. "new Vector2()").
    /// </param>
    /// <returns>
    /// The emitted expression, or <c>null</c> to fall back to generic
    /// <c>new TypeName(args)</c> generation.
    /// </returns>
    string? EmitShorthandConstruction(string typeName, IReadOnlyList<string>? args);

    /// <summary>
    /// Given a shorthand type name and named arguments (as key-value pairs of emitted expression strings),
    /// returns the framework-specific constructor/initializer expression.
    /// E.g. "vec2", [("X","10"),("Y","20")] -&gt; "new Vector2() { X = 10, Y = 20 }".
    /// Returns <c>null</c> to fall back to generic object initializer generation.
    /// </summary>
    string? EmitShorthandNamedConstruction(string typeName, IReadOnlyList<(string Key, string Value)> namedArgs);
}
