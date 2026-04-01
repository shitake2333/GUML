using GUML.Shared.Api.FrameworkPlugin;

namespace GUML.SourceGenerator.FrameworkPlugin;

/// <summary>
/// No-op framework plugin used when no framework is configured.
/// All methods return empty or null results so the emitter falls back
/// to its generic (non-framework) code paths.
/// </summary>
internal sealed class NullFrameworkPlugin
    : IFrameworkTypeProvider, IFrameworkEventProvider, IFrameworkPseudoPropProvider, IFrameworkCastProvider
{
    /// <summary>Shared singleton instance.</summary>
    public static readonly NullFrameworkPlugin Instance = new();

    private NullFrameworkPlugin() { }

    // ── IFrameworkTypeProvider ──

    /// <inheritdoc/>
    public string? ResolveTypeShorthand(string gumlName) => null;

    /// <inheritdoc/>
    public IReadOnlyList<string> GetRequiredUsings() => Array.Empty<string>();

    /// <inheritdoc/>
    public string? EmitShorthandConstruction(string typeName, IReadOnlyList<string>? args) => null;

    /// <inheritdoc/>
    public string? EmitShorthandNamedConstruction(string typeName,
        IReadOnlyList<(string Key, string Value)> namedArgs) => null;

    // ── IFrameworkEventProvider ──

    /// <inheritdoc/>
    public bool IsNativeEvent(string componentTypeName, string signalName) => false;

    /// <inheritdoc/>
    public string EmitEventSubscription(string varName, string signalName, string handlerExpr)
        => $"{varName}.{signalName} += {handlerExpr};";

    // ── IFrameworkPseudoPropProvider ──

    /// <inheritdoc/>
    public ISet<string> PseudoPropertyNames { get; } = new HashSet<string>();

    /// <inheritdoc/>
    public IReadOnlyList<string> EmitPseudoProperty(string varName, string componentTypeName,
        string propertyName, object valueNode,
        Func<object, string> emitExpression, string indent)
        => Array.Empty<string>();

    // ── IFrameworkCastProvider ──

    /// <inheritdoc/>
    public string? GetCastExpression(string fullyQualifiedTypeName, bool isEnum, bool isValueType,
        IReadOnlyList<string> baseTypeFullNames)
        => null;
}
