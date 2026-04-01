namespace GUML.Shared.Api.FrameworkPlugin;

/// <summary>
/// Provides the framework-specific mapping from UI component property names
/// to their change-notification signals or events.
/// Used by the analyzer to determine which properties support reactive binding
/// directions (<c>=:</c> and <c>&lt;=&gt;</c>).
/// </summary>
public interface IFrameworkObservabilityProvider
{
    /// <summary>
    /// Returns the signal or event name that fires when the given property changes
    /// on the given component type, or <c>null</c> if the property is not observable
    /// via a framework-native notification mechanism.
    /// E.g. <c>GetSignalForProperty("LineEdit", "text")</c> → <c>"text_changed"</c>.
    /// </summary>
    /// <param name="typeName">Simple component type name (e.g. <c>"LineEdit"</c>).</param>
    /// <param name="propertyName">Snake-case property name (e.g. <c>"text"</c>).</param>
    string? GetSignalForProperty(string typeName, string propertyName);

    /// <summary>
    /// Returns all pseudo-property (e.g. theme override) names valid for the given
    /// component type, mapped to their override kind identifier (e.g. <c>"Color"</c>,
    /// <c>"Constant"</c>, <c>"FontSize"</c>).
    /// Returns <c>null</c> or an empty dictionary if the type has no known overrides.
    /// Used by LSP completion to suggest valid override keys.
    /// </summary>
    /// <param name="typeName">Simple component type name (e.g. <c>"Button"</c>).</param>
    IReadOnlyDictionary<string, string>? GetThemeOverridesForType(string typeName);
}
