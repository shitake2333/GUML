# The Controller

Every `.guml` file is paired with a C# **controller** class. The controller is the data model, event handler, and business logic layer for the UI defined in the GUML file. Understanding this relationship is the most important concept in GUML.

---

## The Controller–GUML Pair

GUML enforces a strict one-to-one pairing between a `.guml` file and a C# class:

| `.guml` file | Controller class |
|---|---|
| `gui/main.guml` | `MainController` |
| `gui/panel/settings.guml` | `SettingsController` |
| `gui/components/search_bar.guml` | `SearchBarController` |

The naming convention is: convert the file name to `PascalCase` and append `Controller`. The pairing is established at compile time via the `[GumlController]` attribute.

---

## `[GumlController]` Attribute

Every controller class must:
1. Inherit from `GuiController`.
2. Be declared as `partial`.
3. Be annotated with `[GumlController("path/to/file.guml")]`.

```c#
using GUML;

[GumlController("main.guml")]          // relative path from this .cs file to the .guml
public partial class MainController : GuiController
{
    // ...
}
```

The `path` parameter is **relative to the controller source file's location** on disk.

You can also use an absolute resource path:

```c#
[GumlController("res://gui/main.guml")]
public partial class MainController : GuiController { }
```

### What the Attribute Does

At compile time, the Source Generator reads the `[GumlController]` attribute, locates the `.guml` file in the project's `AdditionalFiles`, and generates:

1. A **view class** (`MainGumlView.g.cs`) with a `Build()` method that constructs the UI tree.
2. A **controller partial** (`MainController.NamedNodes.g.cs`) with strongly-typed properties for `@alias` nodes and imported controllers.

---

## `GuiController` Base Class

All controllers inherit from `GuiController`:

```c#
public abstract class GuiController : INotifyPropertyChanged, IDisposable
```

### Members

| Member | Type | Description |
|--------|------|-------------|
| `GumlRootNode` | `Control` | The root Godot `Control` node rendered from the `.guml` file. Set after `Load`. |
| `RootBindingScope` | `BindingScope?` | Manages the lifecycle of all active bindings. Disposed automatically via `Dispose()`. |
| `NamedNode` | `Dictionary<string, Control>` | Named nodes registered via `@alias` syntax. Prefer the generated typed properties. |
| `PropertyChanged` | `event` | Standard `INotifyPropertyChanged` event. Fired by `OnPropertyChanged()`. |
| `Created()` | virtual method | Called once after the UI tree is built and all bindings are active. Override for initialization. |
| `Update()` | virtual method | Called by your `_Process()` method each frame. Override for per-frame logic. |
| `Dispose()` | virtual method | Cleans up bindings and removes `GumlRootNode` from the scene tree. Call in `_ExitTree()`. |
| `OnPropertyChanged()` | protected method | Raises `PropertyChanged`. Use `[CallerMemberName]` — call with no arguments inside a setter. |

---

## Controller Lifecycle

```
Guml.Load<T>(root)
    └─► Build UI tree (component nodes created, children attached)
    └─► Apply property assignments
    └─► Activate data bindings (:=, =:, <=>)
    └─► Connect event handlers (#signal: handler)
    └─► Set GumlRootNode, RootBindingScope on controller
    └─► Call controller.Created()          ← your initialization logic here

_Process(delta)
    └─► Call controller.Update()           ← your per-frame logic here

_ExitTree()
    └─► Call controller.Dispose()
           └─► Dispose bindings (all :=, =:, <=> connections released)
           └─► Remove GumlRootNode from parent
```

### `Created()`

Override `Created()` to run initialization code that needs access to the fully built UI:

```c#
[GumlController("main.guml")]
public partial class MainController : GuiController
{
    public override void Created()
    {
        // GumlRootNode is available here
        // Strongly-typed @alias properties are available here
        TitleLabel.Text = "Loaded!";    // 'TitleLabel' generated from @title_label alias
        GD.Print($"Root node: {GumlRootNode.Name}");
    }
}
```

### `Update()`

Call `controller.Update()` in your Godot `Node._Process()` for per-frame logic:

```c#
public override void _Process(double delta)
{
    _controller?.Update();
}
```

### `Dispose()`

Call `controller.Dispose()` in `_ExitTree()` to tear down bindings cleanly:

```c#
public override void _ExitTree()
{
    _controller?.Dispose();
}
```

`Dispose()` releases all binding subscriptions and removes the root Control node from the scene tree. After `Dispose()`, the controller instance should not be used.

---

## Bindable Properties

For the UI to update automatically when data changes, properties must call `OnPropertyChanged()` in their setter:

```c#
private string _playerName = "Player";

public string PlayerName
{
    get => _playerName;
    set
    {
        _playerName = value;
        OnPropertyChanged();   // <-- required; uses [CallerMemberName] automatically
    }
}
```

`OnPropertyChanged()` uses the `[CallerMemberName]` attribute — call it with no arguments and it automatically passes the current property name.

To fire the notification explicitly (e.g., after modifying a field):

```c#
public void SetScore(int score)
{
    _score = score;
    OnPropertyChanged(nameof(Score));
}
```

### Properties NOT Needing Notification

- Properties that are **never used in a `:=` binding** in the `.guml` file.
- Properties that are **read once** at build time (`:` static assignment).
- `readonly` fields and computed-only getters that depend on notifying properties automatically re-evaluated.

---

## Accessing Named Nodes

When the `.guml` file uses `@alias:` syntax, the Source Generator injects typed properties:

```guml
// main.guml
VBoxContainer {
    @title_label: Label {
        text: "Hello"
    }
    @start_btn: Button {
        text: "Start"
    }
}
```

Generated automatically in `MainController.NamedNodes.g.cs`:

```c#
public partial class MainController
{
    public Label TitleLabel { get; internal set; }
    public Button StartBtn { get; internal set; }
}
```

Use them in the controller:

```c#
public override void Created()
{
    GD.Print(TitleLabel.Text);   // "Hello"
    StartBtn.Disabled = true;
}
```

Alias names use `snake_case` in GUML; the generated property is `PascalCase` (`title_label` → `TitleLabel`).

See [Components & Properties](components_and_properties.md) for more on aliases.

---

## Multiple GUML Files

A larger UI is split into multiple GUML files, each with its own controller. The root controller loads the top-level file; sub-controllers are automatically instantiated for imported GUML components.

```
Main.cs
  └─► Guml.Load<MainController>
        └─► MainController  ← main.guml
              └─► (import "panel/settings") → SettingsController
              └─► (import "components/search_bar") → SearchBarController
```

When `main.guml` imports another GUML file, the Source Generator automatically generates a property for the imported component's controller on `MainController`:

```c#
// Auto-generated in MainController.NamedNodes.g.cs
public partial class MainController
{
    public SettingsController SettingsController { get; set; }
    public SearchBarController SearchBarController { get; set; }
}
```

See [Reusable Components](reusable_components.md) for the full import system.

---

## Example: Hello World

**`gui/hello.guml`**
```guml
VBoxContainer {
    Label {
        text := $"Hello, {$controller.name}!",
        horizontal_alignment: .Center
    }
    Button {
        text: "Change Name",
        #pressed: $controller.on_change_name
    }
}
```

**`gui/HelloController.cs`**
```c#
using GUML;

[GumlController("hello.guml")]
public partial class HelloController : GuiController
{
    private string _name = "World";

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    private void OnChangeName()
    {
        Name = Name == "World" ? "GUML" : "World";
    }
}
```

**`scripts/Main.cs`**
```c#
using Godot;
using GUML;

public partial class Main : Node
{
    [Export] public Node Root;
    private GuiController _ctrl;

    public override void _Ready()
    {
        Guml.ResourceProvider = new DefaultResourceProvider();
        _ctrl = Guml.Load<HelloController>(Root);
    }
    public override void _Process(double delta) => _ctrl?.Update();
    public override void _ExitTree() => _ctrl?.Dispose();
}
```

---

## Next Steps

- **[Components & Properties](components_and_properties.md)** — learn the UI declaration syntax.
- **[Data Binding](data_binding.md)** — in-depth guide to `:=`, `=:`, `<=>`.
- **[Source Generator](source_generator.md)** — understand what code is generated.
