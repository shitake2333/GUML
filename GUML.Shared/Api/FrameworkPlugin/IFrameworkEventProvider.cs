namespace GUML.Shared.Api.FrameworkPlugin;

/// <summary>
/// Provides framework-specific event/signal subscription code generation.
/// Determines whether an event identifier maps to a native framework event
/// (e.g. a Godot signal) versus a GUML custom event, and emits the subscription statement.
/// </summary>
public interface IFrameworkEventProvider
{
    /// <summary>
    /// Determines whether the given event identifier (sans the leading <c>#</c>,
    /// already converted to PascalCase, e.g. <c>"Pressed"</c>) maps to a native
    /// framework event rather than a GUML custom event.
    /// </summary>
    /// <param name="componentTypeName">Simple type name of the component (e.g. <c>"Button"</c>).</param>
    /// <param name="signalName">PascalCase signal/event name (e.g. <c>"Pressed"</c>, <c>"TextChanged"</c>).</param>
    bool IsNativeEvent(string componentTypeName, string signalName);

    /// <summary>
    /// Emits the event subscription statement for a native framework event.
    /// Returns the full statement string (without trailing newline).
    /// E.g. <c>"btn.Pressed += () =&gt; Foo();"</c>.
    /// </summary>
    /// <param name="varName">The local variable name of the target node.</param>
    /// <param name="signalName">PascalCase signal/event name.</param>
    /// <param name="handlerExpr">Already-emitted handler expression string.</param>
    string EmitEventSubscription(string varName, string signalName, string handlerExpr);
}
