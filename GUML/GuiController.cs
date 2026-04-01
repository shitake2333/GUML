using System.ComponentModel;
using System.Runtime.CompilerServices;
using Godot;
using GUML.Binding;

namespace GUML;

/// <summary>
/// Base class for all GUML view controllers.
/// Provides property change notification, named node access, and lifecycle management.
/// </summary>
public abstract class GuiController : INotifyPropertyChanged, IDisposable
{
    /// <summary>
    /// Initializes the controller and subscribes to locale change notifications
    /// from <see cref="Guml.StringProvider"/> so that i18n bindings are automatically
    /// refreshed when the active locale changes.
    /// </summary>
    protected GuiController()
    {
        if (Guml.StringProvider is INotifyPropertyChanged sp)
            sp.PropertyChanged += OnStringProviderPropertyChanged;
    }

    private void OnStringProviderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IStringProvider.CurrentLocale))
            // ReSharper disable once ExplicitCallerInfoArgument
            OnPropertyChanged("_locale");
    }

    /// <summary>Named nodes registered via local alias syntax (@name) in GUML.</summary>
    public readonly Dictionary<string, Control> NamedNode = new();

    /// <summary>The root <see cref="Control"/> node rendered from the associated .guml file.</summary>
    public Control GumlRootNode { get; set; } = null!;

    /// <summary>
    /// The root <see cref="BindingScope"/> that manages the lifecycle of all bindings
    /// created during rendering. Disposed automatically when the root node exits the tree.
    /// </summary>
    public BindingScope? RootBindingScope { get; set; }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Called after the GUML document has been rendered and all bindings are active.</summary>
    public virtual void Created()
    {
    }

    /// <summary>Called each frame for per-frame update logic.</summary>
    public virtual void Update()
    {
    }

    /// <summary>
    /// Disposes the controller, cleaning up bindings and removing the root node from the scene tree.
    /// </summary>
    private bool _disposed;
    public virtual void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (Guml.StringProvider is INotifyPropertyChanged sp)
            sp.PropertyChanged -= OnStringProviderPropertyChanged;
        RootBindingScope?.Dispose();

        var parent = GumlRootNode.GetParent();
        if (parent != null)
        {
            parent.RemoveChild(GumlRootNode);
        }
        GumlRootNode.QueueFree();

        GC.SuppressFinalize(this);
    }

    /// <summary>Raises the <see cref="PropertyChanged"/> event.</summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        // When a property that backs an each-block data source is replaced with a new
        // list instance, notify all ListBinding instances so they can resubscribe.
        RootBindingScope?.UpdateListBindings();
    }
}
