using Godot;

namespace GUML.Binding;

/// <summary>
/// Provides a chained scope for each-block loop variables in generated GUML views.
/// Each scope holds the index/value variables for one each-loop iteration,
/// and links to a <see cref="Parent"/> scope for outer (enclosing) each-loops.
/// Variable lookup walks up the parent chain, similar to JavaScript scope chains.
/// Returns <c>dynamic</c> so that values can be used directly in arithmetic,
/// string concatenation, and typed method arguments without explicit casts.
/// </summary>
public sealed partial class EachScope : RefCounted
{
    private readonly Dictionary<string, object?> _variables = new();

    /// <summary>
    /// The parent scope (from an enclosing each-block), or <c>null</c> for the outermost scope.
    /// </summary>
    public EachScope? Parent { get; }

    /// <summary>
    /// Creates a new each scope with an optional parent scope.
    /// </summary>
    /// <param name="parent">The enclosing each scope, or <c>null</c> for top-level.</param>
    public EachScope(EachScope? parent)
    {
        Parent = parent;
    }

    /// <summary>
    /// Gets or sets a variable in the current scope (does not search parent).
    /// </summary>
    /// <param name="key">The variable name.</param>
    public object? this[string key]
    {
        get => _variables.GetValueOrDefault(key);
        set => _variables[key] = value;
    }

    /// <summary>
    /// Looks up a variable by name, searching the current scope first,
    /// then walking up the <see cref="Parent"/> chain.
    /// Returns <c>null</c> if the variable is not found in any scope.
    /// </summary>
    /// <param name="key">The variable name to look up.</param>
    /// <returns>The variable value, or <c>null</c> if not found.</returns>
    public object? Lookup(string key)
    {
        if (_variables.TryGetValue(key, out object? value))
            return value;
        return Parent?.Lookup(key);
    }

}
