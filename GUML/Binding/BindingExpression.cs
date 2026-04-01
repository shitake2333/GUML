using System.ComponentModel;
using Godot;

namespace GUML.Binding;

/// <summary>
/// Represents a single data binding between a source expression and a target property.
/// Manages the full lifecycle of the binding including dependency tracking,
/// value evaluation, and subscription cleanup.
/// </summary>
public sealed class BindingExpression : IDisposable
{
    private readonly Action<object?> _setter;
    private readonly Func<object?> _valueFactory;
    private readonly DependencyTracker _tracker;
    private readonly INotifyPropertyChanged? _notifySource;
    private readonly IReadOnlySet<string> _dependencies;
    private bool _disposed;

    /// <summary>
    /// Creates a new binding expression with a direct setter delegate.
    /// Used by source-generated code where the target property type is known at compile time.
    /// </summary>
    /// <param name="targetObject">
    /// The UI control to bind to. Passed for call-site compatibility with generated code;
    /// the binding applies values via <paramref name="setter"/> instead.
    /// </param>
    /// <param name="setter">
    /// A delegate that directly assigns a value to the target property with the correct type cast.
    /// For example: <c>(val) =&gt; label.Text = (string)val</c>.
    /// </param>
    /// <param name="valueFactory">
    /// A delegate that produces the current value of the binding source.
    /// </param>
    /// <param name="notifySource">
    /// The object implementing <see cref="INotifyPropertyChanged"/> to subscribe to
    /// (typically the controller). May be null if no change tracking is needed.
    /// </param>
    /// <param name="dependencies">
    /// The set of property names to filter <see cref="INotifyPropertyChanged.PropertyChanged"/>
    /// notifications on. Only changes to these properties will trigger re-evaluation.
    /// </param>
    public BindingExpression(
        object targetObject,
        Action<object?> setter,
        Func<object?> valueFactory,
        INotifyPropertyChanged? notifySource,
        IReadOnlySet<string> dependencies)
    {
        _ = targetObject;
        _setter = setter;
        _valueFactory = valueFactory;
        _notifySource = notifySource;
        _dependencies = dependencies;
        _tracker = new DependencyTracker();
    }

    /// <summary>
    /// Activates the binding: evaluates the expression, assigns the initial value,
    /// and subscribes to change notifications for automatic updates.
    /// </summary>
    public void Activate()
    {
#if !NETSTANDARD2_0
        ObjectDisposedException.ThrowIf(_disposed, this);
#else
        if (_disposed) throw new ObjectDisposedException(nameof(BindingExpression));
#endif

        // Step 1: Evaluate and assign the initial value
        Apply();

        // Step 2: Subscribe to the source's PropertyChanged with filtered dependencies
        if (_notifySource != null && _dependencies.Count > 0)
        {
            _tracker.Subscribe(_notifySource, _dependencies, Apply);
        }
    }

    /// <summary>
    /// Evaluates the source expression and assigns the result to the target property
    /// via the direct setter delegate.
    /// </summary>
    public void Apply()
    {
        if (_disposed) return;
        try
        {
            object? value = _valueFactory();
            _setter(value);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[GUML] Binding expression evaluation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Disposes the binding, unsubscribing all change notification handlers.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _tracker.Dispose();
    }
}
