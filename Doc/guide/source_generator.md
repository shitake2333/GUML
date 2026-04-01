# Source Generator

The GUML Source Generator is a Roslyn incremental source generator that compiles `.guml` files into strongly-typed C# view classes at build time. This page explains how to configure it, what it generates, and how to interpret its diagnostics.

---

## Setup

### 1. Add NuGet Packages

```xml
<PackageReference Include="GodotSharp" Version="4.*" />
<PackageReference Include="GUML" Version="*" />
<PackageReference Include="GUML.SourceGenerator" Version="*" />
<PackageReference Include="GUML.Shared" Version="*" />
```

### 2. Declare `.guml` Files as AdditionalFiles

Add every `.guml` file (including imported components) to the `AdditionalFiles` item group so the generator can read them:

```xml
<ItemGroup>
  <!-- Include all .guml files in the project -->
  <AdditionalFiles Include="**/*.guml" />
</ItemGroup>
```

Or individually:

```xml
<ItemGroup>
  <AdditionalFiles Include="gui/main.guml" />
  <AdditionalFiles Include="gui/components/card.guml" />
</ItemGroup>
```

### 3. Configure the Namespace (Optional)

Set the `GumlNamespaces` build property to specify which namespaces the generator scans for Godot types when resolving component names:

```xml
<PropertyGroup>
  <GumlNamespaces>Godot;MyGame.UI</GumlNamespaces>
</PropertyGroup>
```

By default, `Godot` is always included.

### 4. Mark the Controller with `[GumlController]`

```c#
using GUML.Shared;

[GumlController("gui/main.guml")]   // relative to project root, or "res://..."
public partial class MainController : GuiController
{
    // ...
}
```

The controller class **must** be `partial` for named node and import controller injection.

---

## What the Generator Produces

For each `[GumlController("path/to/file.guml")]` annotation, the generator produces **two** files:

### View Class — `XxxGumlView.g.cs`

A class named `<ControllerSimpleName>GumlView` (e.g., `MainControllerGumlView`) containing:

- **`Build(MainController controller)`** — instantiates the component tree, sets all properties, and wires all signal handlers.
- **`[ModuleInitializer] Register()`** — registers the view class in `Guml.ControllerRegistry` so `Guml.Load<T>()` can find it.

You never call the view class directly — `Guml.Load<T>(root)` handles that.

### Controller Partial — `XxxController.NamedNodes.g.cs`

A partial class extension for the controller containing:

- **Typed properties for every `@alias`** — named `PascalCase` matching the `snake_case` alias in GUML.
- **Typed properties for every `import`** — a property of the imported component's controller type (if it can be resolved), e.g., `public SidebarController SidebarController { get; internal set; }`.

**Example:**

```guml
// gui/main.guml
import "gui/components/sidebar.guml" as Sidebar

VBoxContainer {
    @title_label: Label {
        text: "Hello"
    }
    Sidebar {
        #nav_changed: $controller.on_navigate
    }
}
```

Generated `MainController.NamedNodes.g.cs`:

```c#
public partial class MainController
{
    /// <summary>Named node 'title_label' from gui/main.guml</summary>
    public Label TitleLabel { get; internal set; } = null!;

    /// <summary>Imported controller for 'gui/components/sidebar.guml'</summary>
    public SidebarController SidebarController { get; internal set; } = null!;
}
```

---

## Loading a View

```c#
public override void _Ready()
{
    Guml.ResourceProvider = new DefaultResourceProvider();
    var controller = Guml.Load<MainController>(this);

    // controller.TitleLabel, controller.SidebarController are now populated
    AddChild(controller.GumlRootNode);
}
```

`Guml.Load<T>` calls `Build()` on the registered view class, populates `GumlRootNode`, sets all named node properties, and then calls `controller.Created()`.

---

## Build-Time Diagnostics

The generator emits diagnostics visible in the IDE and build output:

| Code | Severity | When Triggered |
|------|----------|----------------|
| `GUML001` | Error | `.guml` file has a syntax error |
| `GUML002` | Error | A component type is not a recognized Godot control |
| `GUML003` | Warning | A binding expression cannot be fully resolved at compile time |
| `GUML004` | Info | View class successfully generated |
| `GUML005` | Error | The `.guml` file path in `[GumlController]` was not found in `AdditionalFiles` |
| `GUML006` | Error | Two controllers reference the same `.guml` file |
| `GUML007` | Warning | Controller class is not `partial` — named node properties cannot be generated |

### Fixing Common Errors

**GUML005: File not found**

Ensure the path in `[GumlController(...)]` matches the entry in `<AdditionalFiles>`. Check:
- Relative vs. absolute path (`res://` prefix)
- File extension (`.guml`)
- Correct case on case-sensitive file systems

**GUML006: Duplicate path**

Only one controller can be linked to each `.guml` file. Rename one of the `.guml` files.

**GUML007: Not partial**

Add `partial` to the controller class declaration:

```c#
// Before (triggers GUML007)
public class MainController : GuiController { }

// After (correct)
public partial class MainController : GuiController { }
```

---

## Incremental Generation

The generator is **incremental** — it only re-generates when the `.guml` file content or the annotated controller declaration changes. Unrelated file edits do not trigger regeneration, keeping build times fast.

---

## Generated Code Location

Generated `.g.cs` files are written to the `obj/` directory and are included in the compilation automatically. You can view them in Visual Studio or VS Code via the "Generated Files" node under the project in Solution Explorer.

Typical paths:
```
obj/Debug/net8.0/generated/GUML.SourceGenerator.GumlSourceGenerator/
    MainControllerGumlView.g.cs
    MainController.NamedNodes.g.cs
```

---

## Custom Resource Provider

Before calling `Guml.Load<T>()`, set a resource provider if your `.guml` files use resource loaders (`image(...)`, `font(...)`, etc.):

```c#
// DefaultResourceProvider loads from Godot's res:// file system
Guml.ResourceProvider = new DefaultResourceProvider();

// Custom implementation
public class MyResourceProvider : IResourceProvider
{
    public Resource Load(string path) => ResourceLoader.Load(path);
}

Guml.ResourceProvider = new MyResourceProvider();
```

`IResourceProvider` has a single method:

```c#
public interface IResourceProvider
{
    Resource Load(string resourcePath);
}
```

---

## Next Steps

- **[Theming](theming.md)** — apply theme overrides and StyleBox styles.
- **[Controller Guide](controller.md)** — full `GuiController` API reference.
- **[API Reference](../reference/api.md)** — `Guml`, `GuiController`, `NotifyList<T>`.
