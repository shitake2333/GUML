using System.Collections.Specialized;

namespace GUML.Binding;

/// <summary>
/// Manages the subscription to <see cref="INotifyCollectionChanged.CollectionChanged"/> for
/// each-block rendering, with proper lifecycle management.
/// Structural changes (Add/Remove/Insert/Reset) invoke the <c>structureChangedHandler</c>;
/// item replacement (Replace action / indexer assignment) invokes the <c>valueChangedHandler</c>.
/// Implements <see cref="IDisposable"/> to ensure the event handler is unsubscribed
/// when the associated UI node is destroyed.
/// </summary>
public sealed class ListBinding : IDisposable
{
    private readonly Func<INotifyCollectionChanged> _sourceFactory;
    private INotifyCollectionChanged? _source;
    private readonly NotifyCollectionChangedEventHandler _handler;
    private readonly Action _structureChangedHandler;
    private bool _disposed;

    /// <summary>
    /// Creates a new list binding and immediately subscribes to the source's
    /// <see cref="INotifyCollectionChanged.CollectionChanged"/> event.
    /// </summary>
    /// <param name="source">The observable collection to monitor.</param>
    /// <param name="structureChangedHandler">Invoked when the collection structure changes (add/remove/insert/reset).</param>
    /// <param name="valueChangedHandler">Invoked when a collection item is replaced, with the item index and new value.</param>
    public ListBinding(INotifyCollectionChanged source, Action structureChangedHandler,
        Action<int, object?> valueChangedHandler)
        : this(() => source, structureChangedHandler, valueChangedHandler)
    {
    }

    /// <summary>
    /// Creates a new list binding using a factory function to retrieve the source collection.
    /// This allows the binding to switch sources when the component is reused/bound to a new scope.
    /// </summary>
    /// <param name="sourceFactory">A function that returns the current observable collection.</param>
    /// <param name="structureChangedHandler">Invoked when the collection structure changes (add/remove/insert/reset).</param>
    /// <param name="valueChangedHandler">Invoked when a collection item is replaced, with the item index and new value.</param>
    public ListBinding(Func<INotifyCollectionChanged> sourceFactory, Action structureChangedHandler,
        Action<int, object?> valueChangedHandler)
    {
        _sourceFactory = sourceFactory;
        _structureChangedHandler = structureChangedHandler;
        _handler = (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Replace)
            {
                valueChangedHandler(e.NewStartingIndex, e.NewItems?[0]);
            }
            else
            {
                structureChangedHandler();
            }
        };
        _source = _sourceFactory();
        if (_source != null)
        {
            _source.CollectionChanged += _handler;
        }
    }

    /// <summary>
    /// Updates the binding by re-evaluating the source factory.
    /// If the source collection has changed, updates the subscription and triggers a full reconcile.
    /// </summary>
    public void Update()
    {
        if (_disposed) return;

        var newSource = _sourceFactory();
        if (!ReferenceEquals(newSource, _source))
        {
            if (_source != null)
            {
                _source.CollectionChanged -= _handler;
            }

            _source = newSource;

            if (_source != null)
            {
                _source.CollectionChanged += _handler;
            }

            // Notify that the source has changed completely — trigger full reconcile
            if (_source != null)
            {
                _structureChangedHandler();
            }
        }
    }

    /// <summary>
    /// Unsubscribes from the collection changed event.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_source != null)
        {
            _source.CollectionChanged -= _handler;
        }
        _source = null;
    }
}
