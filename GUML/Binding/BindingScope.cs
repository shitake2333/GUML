using Godot;

namespace GUML.Binding;

/// <summary>
/// Manages the lifecycle of all <see cref="BindingExpression"/> instances
/// (and other <see cref="IDisposable"/> resources) attached to a single Godot <see cref="Node"/>.
/// Automatically disposes all bindings when the node exits the scene tree.
/// </summary>
public sealed partial class BindingScope : RefCounted
{
    /// <summary>Meta key used to attach a <see cref="BindingScope"/> to a Godot node.</summary>
    internal const string MetaKey = "GumlBindingScope";

    private readonly List<IDisposable> _bindings = new();
    private readonly Node _owner;
    private bool _disposed;
    private bool _autoDispose = true;

    /// <summary>
    /// Creates a new binding scope for the specified node.
    /// Automatically subscribes to the node's <c>TreeExiting</c> signal for cleanup.
    /// </summary>
    /// <param name="owner">The Godot node that owns this scope.</param>
    public BindingScope(Node owner)
    {
        _owner = owner;
        _owner.SetMeta(MetaKey, this);
        _owner.TreeExiting += OnOwnerExiting;
    }

    /// <summary>
    /// Disables automatic disposal when the owner node exits the tree.
    /// Useful when caching nodes (temporarily detaching them).
    /// </summary>
    public void DisableAutoDispose()
    {
        if (_autoDispose && IsInstanceValid(_owner))
        {
            _owner.TreeExiting -= OnOwnerExiting;
            _autoDispose = false;
        }
    }

    /// <summary>
    /// Enables automatic disposal when the owner node exits the tree.
    /// </summary>
    public void EnableAutoDispose()
    {
        if (!_autoDispose && IsInstanceValid(_owner))
        {
            _owner.TreeExiting += OnOwnerExiting;
            _autoDispose = true;
        }
    }

    /// <summary>
    /// Registers a binding (or any <see cref="IDisposable"/>) to be managed by this scope.
    /// </summary>
    /// <param name="binding">The disposable resource to register.</param>
    public void Add(IDisposable binding)
    {
#if !NETSTANDARD2_0
        ObjectDisposedException.ThrowIf(_disposed, this);
#else
        if (_disposed) throw new ObjectDisposedException(nameof(BindingScope));
#endif
        _bindings.Add(binding);
    }

    /// <summary>
    /// Updates all bindings in this scope.
    /// </summary>
    public void UpdateAll()
    {
        if (_disposed) return;
        foreach (var binding in _bindings)
        {
            if (binding is BindingExpression be)
            {
                be.Apply();
            }
            else if (binding is ListBinding lb)
            {
                lb.Update();
            }
        }
    }

    /// <summary>
    /// Updates only <see cref="ListBinding"/> instances in this scope and all child scopes.
    /// Called when a controller property reference may have been replaced so that
    /// each-block bindings can resubscribe to the new list instance.
    /// </summary>
    public void UpdateListBindings()
    {
        if (_disposed) return;
        foreach (var binding in _bindings)
        {
            if (binding is ListBinding lb)
                lb.Update();
            else if (binding is BindingScope child)
                child.UpdateListBindings();
        }
    }

    /// <summary>
    /// Recursively updates bindings for the given node and all its descendants.
    /// </summary>
    public static void UpdateRecursive(Node node)
    {
        if (node.HasMeta(MetaKey))
        {
            var scope = node.GetMeta(MetaKey).As<BindingScope>();
            scope.UpdateAll();
        }

        foreach (var child in node.GetChildren())
        {
            if (child is { } childNode)
            {
                UpdateRecursive(childNode);
            }
        }
    }

    /// <summary>
    /// Disposes all registered bindings and unsubscribes from the owner node's signal.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (IsInstanceValid(_owner))
        {
             _owner.TreeExiting -= OnOwnerExiting;
        }

        foreach (var binding in _bindings)
        {
            binding.Dispose();
        }
        _bindings.Clear();
        base.Dispose(disposing);
    }

    private void OnOwnerExiting()
    {
        Dispose();
    }
}
