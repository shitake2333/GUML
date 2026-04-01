# API Reference

Complete reference for the GUML runtime and shared API.

---

## `Guml` — Static Runtime Class

Namespace: `GUML`

The entry point for loading and configuring the GUML runtime.

### Properties

#### `ResourceProvider`

```c#
public static IResourceProvider ResourceProvider { get; set; }
```

The resource provider used to load assets referenced in GUML files (`image(...)`, `font(...)`, `audio(...)`, `video(...)`). Must be set before calling `Load<T>()` on any GUML file that uses resource functions.

```c#
Guml.ResourceProvider = new DefaultResourceProvider();
```

---

#### `ControllerRegistry`

```c#
public static readonly Dictionary<Type, Func<Node, GuiController>> ControllerRegistry
```

Registry of source-generated view factories, keyed by controller type. Populated automatically by `[ModuleInitializer]` in generated `*GumlView.g.cs` files. You do not typically interact with this directly.

---

### Methods

#### `Load<T>(Node root)`

```c#
public static T Load<T>(Node root) where T : GuiController
```

Loads a GUML view by controller type. Calls the registered view factory, builds the component tree, populates all named node properties, and calls `controller.Created()`.

**Parameters:**
- `root` — The parent node to attach the generated UI tree to.

**Returns:** The created `T` controller instance, with `GumlRootNode` set.

**Throws:** `GumlRuntimeException` if no registered view factory is found for `T`. Ensure the controller has a `[GumlController]` attribute and the project was compiled with the source generator enabled.

```c#
public override void _Ready()
{
    Guml.ResourceProvider = new DefaultResourceProvider();
    var controller = Guml.Load<MainController>(this);
    AddChild(controller.GumlRootNode);
}
```

---

## `GuiController` — Base Controller Class

Namespace: `GUML`  
Assembly: `GUML`

```c#
public abstract class GuiController : INotifyPropertyChanged, IDisposable
```

Base class for every GUML view controller. Implements `INotifyPropertyChanged` for reactive data binding.

### Properties

#### `GumlRootNode`

```c#
public Control GumlRootNode { get; set; }
```

The root `Control` node rendered from the associated `.guml` file. Populated by `Guml.Load<T>()` before `Created()` is called. Add this to your Godot scene tree.

---

#### `NamedNode`

```c#
public readonly Dictionary<string, Control> NamedNode
```

Dictionary of all named nodes registered via `@alias:` syntax in the GUML file. Keyed by the `snake_case` alias name. Prefer the strongly-typed generated properties over this dictionary.

---

#### `RootBindingScope`

```c#
public BindingScope? RootBindingScope { get; set; }
```

The root binding scope managing the lifecycle of all bindings created during rendering. Disposed automatically when the root node exits the scene tree.

---

### Events

#### `PropertyChanged`

```c#
public event PropertyChangedEventHandler? PropertyChanged
```

Raised by `OnPropertyChanged()`. The GUML runtime subscribes to this event to update `:=` bindings.

---

### Methods

#### `Created()`

```c#
public virtual void Created()
```

Called after the GUML document has been fully rendered and all bindings are active. Override to perform post-load initialization (e.g., load initial data, start animations):

```c#
public override void Created()
{
    UserName = "Player1";
    Score = 0;
    StartAnimations();
}
```

---

#### `Update()`

```c#
public virtual void Update()
```

Called each frame for per-frame update logic. Override for continuous updates:

```c#
public override void Update()
{
    Timer += GetProcessDeltaTime(); // Godot API available from the tree
}
```

---

#### `Dispose()`

```c#
public virtual void Dispose()
```

Disposes the controller, cleaning up all bindings and removing the root node from the scene tree. Call this to tear down the GUML view.

Default implementation:
1. Calls `RootBindingScope?.Dispose()`.
2. Removes `GumlRootNode` from its parent.

```c#
void CloseDialog()
{
    _dialogController.Dispose();
    _dialogController = null;
}
```

---

#### `OnPropertyChanged([CallerMemberName] string? propertyName = null)`

```c#
protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
```

Raises `PropertyChanged` for the calling property. Uses `[CallerMemberName]` — no need to pass the name explicitly:

```c#
private int _score;
public int Score
{
    get => _score;
    set
    {
        _score = value;
        OnPropertyChanged();   // automatically passes "Score"
    }
}
```

To manually trigger for a specific property (e.g., a computed property):

```c#
public void AddItem()
{
    Items.Add(new Item());
    OnPropertyChanged(nameof(ItemCount));   // explicitly named
}

public int ItemCount => Items.Count;
```

---

## `GumlControllerAttribute` — Controller Annotation

Namespace: `GUML.Shared`  
Assembly: `GUML.Shared`

```c#
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class GumlControllerAttribute : Attribute
```

Associates a controller class with a `.guml` file path. Used by the Source Generator to find and compile the GUML file into a view class.

### Constructor

```c#
public GumlControllerAttribute(string gumlPath)
```

| Parameter | Description |
|-----------|-------------|
| `gumlPath` | Path to the `.guml` file. Relative to the controller source file's directory, or an absolute `res://` path. |

**Examples:**

```c#
[GumlController("../gui/main.guml")]         // relative to controller source file
[GumlController("res://gui/main.guml")]      // absolute resource path
```

---

## `IResourceProvider` Interface

Namespace: `GUML`  
Assembly: `GUML`

```c#
public interface IResourceProvider
```

Provides typed asset loading for GUML resource functions. Each method corresponds to one resource function in GUML syntax.

### Methods

| Method | GUML Function | Returns |
|--------|---------------|---------|
| `LoadImage(string path, Node? consumer)` | `image("path")` | `Texture2D` (boxed as `object`) |
| `LoadFont(string path, Node? consumer)` | `font("path")` | `Font` (boxed as `object`) |
| `LoadAudio(string path, Node? consumer)` | `audio("path")` | `AudioStream` (boxed as `object`) |
| `LoadVideo(string path, Node? consumer)` | `video("path")` | `VideoStream` (boxed as `object`) |

The `consumer` node is optional and used for reference-counted lifecycle management in `DefaultResourceProvider`.

### Custom Implementation

```c#
public class MyResourceProvider : IResourceProvider
{
    public object LoadImage(string path, Node? consumer = null)
        => ResourceLoader.Load<Texture2D>(path);

    public object LoadFont(string path, Node? consumer = null)
        => ResourceLoader.Load<FontFile>(path);

    public object LoadAudio(string path, Node? consumer = null)
        => ResourceLoader.Load<AudioStream>(path);

    public object LoadVideo(string path, Node? consumer = null)
        => ResourceLoader.Load<VideoStream>(path);
}
```

---

## `DefaultResourceProvider` Class

Namespace: `GUML`  
Assembly: `GUML`

```c#
public class DefaultResourceProvider : IResourceProvider
```

Built-in resource provider with caching and reference-counted lifecycle management. Resources are cached by `(category, path)` and automatically released when all consumer nodes exit the scene tree.

### Usage

```c#
Guml.ResourceProvider = new DefaultResourceProvider();
```

### Customization

`DefaultResourceProvider` is open for inheritance. Override the `*Core` methods for custom loading behavior:

```c#
public class MyProvider : DefaultResourceProvider
{
    protected override object LoadImageCore(string path)
    {
        // Custom image loading — e.g., with format conversion
        return ResourceLoader.Load<CompressedTexture2D>(path);
    }
}
```

---

## Collections for `each` Blocks

The `each` block's data source must implement `System.Collections.Specialized.INotifyCollectionChanged`. The standard .NET `ObservableCollection<T>` satisfies this:

```c#
using System.Collections.ObjectModel;

public partial class ListController : GuiController
{
    public ObservableCollection<string> Items { get; } = new();

    public override void Created()
    {
        Items.Add("First item");
        Items.Add("Second item");
    }

    public void AddItem(string text) => Items.Add(text);
    public void RemoveAt(int index) => Items.RemoveAt(index);
}
```

### `INotifyCollectionChanged` Change Events

| Change Action | GUML Reconciler Response |
|---------------|-------------------------|
| `Add` | Appends a new row node tree |
| `Insert` | Inserts a row at the specified index |
| `Remove` | Frees the row node at the specified index |
| `Replace` (indexer `[i] = x`) | Re-evaluates `:=` bindings on that row only; no node create/destroy |
| `Reset` / `Clear` | Frees all row nodes and rebuilds from scratch |

---

## Exceptions

### `GumlRuntimeException`

Namespace: `GUML`

```c#
public class GumlRuntimeException(string msg) : Exception(msg)
```

Thrown when a GUML runtime operation fails. Common causes:
- `Guml.Load<T>()` called for a controller with no registered view factory.
- Source generator output not compiled (e.g., missing `<AdditionalFiles>` in `.csproj`).

### `TypeErrorException`

Namespace: `GUML`

```c#
public class TypeErrorException(string msg) : Exception(msg)
```

Thrown when a type conversion fails during GUML property assignment (e.g., assigning a value of an incompatible type to a Godot property).

---

## `ThemeValueType` Enum

Namespace: `GUML`

```c#
public enum ThemeValueType
{
    Constant,
    Color,
    Font,
    FontSize,
    Icon,
    Style
}
```

Used internally to determine which `AddTheme*Override` method to call when applying `theme_overrides` entries. Matches Godot's theme entry classification.

---

## Related

- [Source Generator Guide](../guide/source_generator.md) — generated code structure and diagnostics.
- [Controller Guide](../guide/controller.md) — controller lifecycle and patterns.
- [Data Binding Guide](../guide/data_binding.md) — `INotifyPropertyChanged` patterns.
- [List Rendering Guide](../guide/list_rendering.md) — `ObservableCollection<T>` with `each` blocks.
