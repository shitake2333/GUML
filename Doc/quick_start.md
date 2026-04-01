# Quick Start

GUML (Godot UI Markup Language) is a declarative markup language for building user interfaces in Godot .NET applications. This guide walks you through installation, project setup, and common usage patterns.

For complete syntax details, see [guml_syntax.md](guml_syntax.md).

---

## Installation

Add the NuGet packages to your Godot .NET project:

```
dotnet add package GUML
dotnet add package GUML.SourceGenerator
```

Then register your `.guml` files as additional files so the source generator can process them. Add the following to your `.csproj`:

```xml
<ItemGroup>
  <!-- Register .guml files for source generation -->
  <AdditionalFiles Include="gui\**\*.guml" />
</ItemGroup>
```

If you use custom GUI component types in your own namespaces, also configure:

```xml
<PropertyGroup>
  <!-- Semicolon-separated namespaces containing custom GUI components -->
  <GumlNamespaces>MyGame.GUI;MyGame.Widgets</GumlNamespaces>
</PropertyGroup>

<ItemGroup>
  <CompilerVisibleProperty Include="GumlNamespaces" />
</ItemGroup>
```

---

## Initialization

Initialize GUML in your root Node's `_Ready()` method:

```c#
using Godot;
using GUML;

public partial class Main : Node
{
    [Export]
    public Node Root;

    private GuiController _controller;

    public override void _Ready()
    {
        Guml.Init();
        Guml.ResourceProvider = new DefaultResourceProvider();
        _controller = Guml.LoadGuml(Root, "gui/main.guml");
    }

    public override void _Process(double delta)
    {
        _controller?.Update();
    }

    public override void _ExitTree()
    {
        _controller?.Dispose();
    }
}
```

- `Guml.Init()`  initializes the GUML runtime.
- `Guml.ResourceProvider`  sets the resource loader. `DefaultResourceProvider` provides built-in caching and reference-counted lifecycle management. Implement `IResourceProvider` for custom loading logic.
- `Guml.LoadGuml(rootNode, path)`  loads a `.guml` file, attaches the generated UI to `rootNode`, and returns the controller instance.
- `Guml.DefaultTheme`  an optional static property to set a default theme applied to all UI components.

---

## Controller and GUML File

Every `.guml` file has a paired C# controller class. The naming convention is: convert the file name to PascalCase and append `Controller`. For example, `main.guml`  `MainController`.

Annotate your controller with `[GumlController]` and declare it as `partial`:

```c#
using GUML;

[GumlController("../../gui/main.guml")]
public partial class MainController : GuiController
{
    public override void Created()
    {
        // Called after the UI tree is built and all bindings are established.
    }
}
```

The `[GumlController]` attribute path is relative to the controller source file.

### Controller Lifecycle

Every controller inherits from `GuiController` and implements `INotifyPropertyChanged`.

| Method | When called | Notes |
|--------|-------------|-------|
| `Created()` | After the UI tree is built and all bindings are set up | Called automatically by `LoadGuml` |
| `Update()` | Every frame  equivalent to `Node._Process` | Call manually in `_Process` |
| `Dispose()` | When the controller is destroyed | Call manually in `_ExitTree`. Automatically removes the bound GUI node from the scene tree. |

---

## Source Generator

GUML uses a Roslyn source generator (`GUML.SourceGenerator`) that compiles `.guml` files into strongly-typed C# classes at build time, providing compile-time error checking and zero-reflection runtime loading.

### How It Works

For each `[GumlController]`-annotated class the generator produces:

1. **View class** (`XxxGumlView.g.cs`): A `Build()` method that constructs the UI tree. A `[ModuleInitializer] Register()` method that registers the view factory into `Guml.ViewRegistry` automatically.
2. **Controller partial** (`XxxController.NamedNodes.g.cs`): Strongly-typed properties for all `@alias` named nodes and imported controller instances. Only generated when the controller is `partial` and the `.guml` file has aliases or imports.

`Guml.LoadGuml()` looks up the registered factory in `Guml.ViewRegistry`. If found, it instantiates the controller and calls `Build()`  no reflection. If not found, a `GumlRuntimeException` is thrown with guidance to add `[GumlController]`.

### Named Nodes (`@alias`)

Use `@name:` before a component declaration to name it. The source generator injects a strongly-typed property for it on the controller.

`main.guml`:
```
Panel {
    @hello_label: Label {
        text: "hello"
    }
}
```

Auto-generated (`MainController.NamedNodes.g.cs`):
```c#
public partial class MainController
{
    /// <summary>Named node '@hello_label' (Label).</summary>
    public Label HelloLabel { get; internal set; }
}
```

Access it in the controller with full type safety:
```c#
public override void Created()
{
    HelloLabel.Text = "Updated!";   // compile-time type check 
    HelloLabel.Modulate = Colors.Red;
}
```

The alias name uses `snake_case` in GUML and is converted to `PascalCase` for the C# property (`hello_label`  `HelloLabel`).

### Imported Component Controllers

When a `.guml` file imports another component, the source generator automatically produces a typed property on the controller:

`main.guml`:
```
import "panel/setting"

Panel {
    Setting {}
}
```

Auto-generated:
```c#
public partial class MainController
{
    /// <summary>Import controller for 'panel/setting.guml'.</summary>
    public SettingController SettingController { get; set; }
}
```

If you manually define the property in your controller, the generator detects it and skips generation.

### Diagnostics

| ID | Severity | Description |
|----|----------|-------------|
| GUML005 | Error | `.guml` file not found in AdditionalFiles |
| GUML006 | Error | Multiple controllers reference the same `.guml` file |
| GUML007 | Warning | Controller not declared as `partial`  named node properties cannot be generated |

---

## Data Binding

GUML supports binding controller properties to UI component properties using `:=`. When the controller property changes (calls `OnPropertyChanged()`), the UI updates automatically.

### Making Properties Bindable

All properties involved in bindings must call `OnPropertyChanged()` in their setter:

```c#
public partial class MainController : GuiController
{
    private string _userName = "Player";

    public string UserName
    {
        get => _userName;
        set
        {
            _userName = value;
            OnPropertyChanged();  // required to trigger UI updates
        }
    }
}
```

### One-Way Binding (Data  UI)

Use `:=` to bind a controller property to a UI property:

`main.guml`:
```
Panel {
    Label {
        position: vec2(10, 10),
        size: vec2(200, 30),
        text := $controller.user_name
    }
}
```

**Expression binding**  the UI re-evaluates whenever any referenced property changes:

```
Label {
    text := $"Hello, {$controller.user_name}! Score: {$controller.score}"
}
```

### Two-Way Binding

Use `<=>` for symmetric binding between a component property and a controller property. The right-hand side must be a writable controller property. Requires the component to support property change notifications.

```
CustomToggle {
    checked <=> $controller.is_enabled
}
```

### List Rendering with `NotifyList<T>`

The data source for `each` blocks must implement `INotifyListChanged`. GUML provides `NotifyList<T>`:

```c#
public NotifyList<string> Names
{
    get => _names;
    set
    {
        _names = value;
        OnPropertyChanged();
    }
}
private NotifyList<string> _names = new() { "Alice", "Bob" };
```

`main.guml`:
```
Panel {
    each $controller.names { |idx, name|
        Control {
            custom_minimum_size: vec2(220, 40),
            Label {
                position: vec2(10, 10),
                text: name
            }
        }
    }
}
```

**Incremental updates:** Calling `Names.Add(...)` only creates nodes for the new item. Other list mutations (Remove, Insert, Clear) trigger a full re-render.

---

## Internationalization (i18n)

GUML provides built-in `tr()` and `ntr()` expressions for text translation, following the gettext convention.

### Setup

Implement the `IStringProvider` interface and assign it at startup:

```c#
public override void _Ready()
{
    Guml.Init();
    Guml.ResourceProvider = new DefaultResourceProvider();
    Guml.StringProvider = new MyStringProvider(); // your IStringProvider implementation
    _controller = Guml.LoadGuml(Root, "gui/main.guml");
}
```

`IStringProvider` requires:
- `Tr(msgid, context?, args?)` — singular translation.
- `Ntr(singular, plural, count, context?, args?)` — plural translation.
- `INotifyPropertyChanged` — raise `PropertyChanged("CurrentLocale")` on locale switch.

When `Guml.StringProvider` is `null`, `tr()` returns the msgid unchanged.

### Usage in GUML

```
Label { text: tr("Start Game") }
Label { text := tr("Hello, {name}!", { name: $controller.user_name }) }
Label { text := ntr("One item", "{count} items", $controller.item_count,
                    { count: $controller.item_count }) }
```

When used with `:=`, translations automatically re-evaluate on locale changes.

For full syntax details, see the [Translation Expressions](guml_syntax.md#translation-expressions-i18n) section in the syntax reference.

---

## Complete Example

### Project Structure

```
MyGame/
  Main.cs
  gui/
    main.guml
    MainController.cs
    panel/
      setting.guml
      SettingController.cs
```

### `gui/main.guml`

```
import "panel/setting"

Panel {
    @title_label: Label {
        position: vec2(10, 10),
        size: vec2(400, 40),
        text := $"Welcome, {$controller.player_name}!"
    }

    @start_btn: Button {
        position: vec2(10, 60),
        size: vec2(120, 40),
        text: "Start Game",
        #pressed: $controller.on_start_pressed
    }

    Setting {
        position: vec2(10, 110)
    }

    each $controller.scores { |idx, score|
        Control {
            custom_minimum_size: vec2(400, 30),
            Label {
                text: $"{idx + 1}. {score.name}: {score.value}"
            }
        }
    }
}
```

### `gui/MainController.cs`

```c#
using Godot;
using GUML;

[GumlController("main.guml")]
public partial class MainController : GuiController
{
    private string _playerName = "Player";

    public string PlayerName
    {
        get => _playerName;
        set { _playerName = value; OnPropertyChanged(); }
    }

    public NotifyList<ScoreEntry> Scores { get; } = new();

    public override void Created()
    {
        // TitleLabel and StartBtn are auto-generated from @alias declarations
        GD.Print(TitleLabel.Text);
    }

    private void OnStartPressed()
    {
        GD.Print("Game started!");
    }
}

public record ScoreEntry(string Name, int Value);
```

### `Main.cs` (Root Node)

```c#
using Godot;
using GUML;

public partial class Main : Node
{
    [Export] public Node Root;

    private GuiController _controller;

    public override void _Ready()
    {
        Guml.Init();
        Guml.ResourceProvider = new DefaultResourceProvider();
        _controller = Guml.LoadGuml(Root, "gui/main.guml");
    }

    public override void _Process(double delta)
    {
        _controller?.Update();
    }

    public override void _ExitTree()
    {
        _controller?.Dispose();
    }
}
```

---

## Theme Overrides

GUML provides a `ThemeOverrides` property on components that mirrors the editor's ThemeOverrides panel. Its value is an object literal:

```
Button {
    theme_override_colors_font_color: color(1.0, 1.0, 1.0, 1.0),
    theme_override_font_sizes_font_size: 16,
    theme_override_styles_normal: style_box_flat({
        bg_color: color(0.2, 0.2, 0.8, 1.0),
        corner_radius_top_left: 4,
        corner_radius_top_right: 4,
        corner_radius_bottom_left: 4,
        corner_radius_bottom_right: 4
    })
}
```

`Guml.ThemeOverrides`  a static property providing all available theme override entries and their types.

`Guml.DefaultTheme`  a static property to set a default theme applied to all UI components globally.

---

## GUML Syntax Reference

See [guml_syntax.md](guml_syntax.md) for the complete syntax reference, including:
- All value types (strings, template strings, enums, arrays, typed dictionaries, vectors, colors, resource loaders, etc.)
- Expressions and operators (arithmetic, logical, ternary, member access, call expressions)
- Property mapping operators (`:`, `:=`, `=:`, `<=>`)
- List rendering (`each` block form and projection form)
- Parameter declarations (`param`)
- Event declarations (`event`)
- Comma separator rules
- Identifier naming conventions
