# Getting Started

This guide walks you through setting up a Godot .NET project that uses GUML, from installation to a working application.

**Prerequisites:** Godot 4.x with .NET support, .NET 8 SDK or later.

---

## 1. Install NuGet Packages

In your Godot .NET project directory, add the GUML packages:

```bash
dotnet add package GUML
dotnet add package GUML.SourceGenerator
```

Open your `.csproj` file and verify both packages were added. Also add an `<AdditionalFiles>` entry that points to your `.guml` files so the source generator can process them:

```xml
<Project Sdk="Godot.NET.Sdk/4.x.x">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="GUML" Version="*" />
    <PackageReference Include="GUML.SourceGenerator" Version="*" />
  </ItemGroup>

  <ItemGroup>
    <!-- Tell the source generator where your .guml files live -->
    <AdditionalFiles Include="gui\**\*.guml" />
  </ItemGroup>
</Project>
```

> **Custom namespaces:** If your project defines custom Godot Control subclasses in your own namespaces, tell the generator where to find them:
> ```xml
> <PropertyGroup>
>   <GumlNamespaces>MyGame.UI;MyGame.Widgets</GumlNamespaces>
> </PropertyGroup>
> <ItemGroup>
>   <CompilerVisibleProperty Include="GumlNamespaces" />
> </ItemGroup>
> ```

---

## 2. Project Structure

A typical GUML project looks like this:

```
MyGame/
  MyGame.csproj
  scripts/
    Main.cs              ← entry point Node
    gui/
      main.guml          ← UI definition
      MainController.cs  ← paired controller
      panel/
        settings.guml
        SettingsController.cs
```

The naming convention is: for a file `gui/main.guml`, the controller class must be named `MainController`. The Source Generator enforces this pairing via the `[GumlController]` attribute.

---

## 3. Write Your First GUML File

Create `gui/main.guml`:

```guml
// gui/main.guml
VBoxContainer {
    Label {
        text: "Hello, GUML!",
        horizontal_alignment: .Center
    }

    Button {
        text: "Click me",
        #pressed: $controller.on_button_pressed
    }
}
```

A GUML document always has exactly one **root component** (here `VBoxContainer`). Child components are nested inside. Properties use `name: value` syntax. The `#pressed` line connects the Godot `pressed` signal to a method on the controller.

---

## 4. Write the Controller

Create `gui/MainController.cs`. Every `.guml` file must have a paired `partial` controller class annotated with `[GumlController]`:

```c#
// gui/MainController.cs
using Godot;
using GUML;

[GumlController("main.guml")]
public partial class MainController : GuiController
{
    public override void Created()
    {
        // Called once the UI tree is built and all bindings are active.
        GD.Print("UI is ready!");
    }

    private void OnButtonPressed()
    {
        GD.Print("Button was pressed!");
    }
}
```

The `[GumlController("main.guml")]` path is relative to the controller source file.

> **Naming:** The `[GumlController]` attribute path must point to an existing `.guml` file that is also registered in `<AdditionalFiles>`.

---

## 5. Initialize GUML in Your Root Node

Create the Godot entry-point Node that loads GUML:

```c#
// scripts/Main.cs
using Godot;
using GUML;

public partial class Main : Node
{
    [Export]
    public Node Root;  // Assign this in the Godot editor to a Control node

    private GuiController _controller;

    public override void _Ready()
    {
        Guml.ResourceProvider = new DefaultResourceProvider();
        _controller = Guml.Load<MainController>(Root);

        // Make the GUML root fill the window (optional)
        if (_controller.GumlRootNode is { } r)
            r.SetAnchorsPreset(Control.LayoutPreset.FullRect);
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

`Guml.Load<T>()` is the type-safe API — it uses the source-generated view factory and requires no reflection. `Guml.ResourceProvider` must be set before loading any GUML that references resources (images, fonts, audio).

---

## 6. Set Up the Scene in Godot

1. Create a Godot scene with a root `Node` (or `Node2D`, etc.).
2. Attach the `Main.cs` script.
3. Add a `Control` child node (e.g., a plain `Control`) and assign it to the exported `Root` property in the inspector.
4. Run the scene.

The GUML renderer will attach the generated UI tree as a child of `Root` and call `MainController.Created()`.

---

## 7. Next Steps

Now that your project is running, explore the core concepts:

- **[The Controller](controller.md)** — understand the controller lifecycle and how to pass data to the UI.
- **[Components & Properties](components_and_properties.md)** — learn all the property types and how component trees are built.
- **[Data Binding](data_binding.md)** — automatically keep the UI in sync with your data.
- **[Events](events.md)** — wire up signals and handle user input.
- **[List Rendering](list_rendering.md)** — render dynamic lists with `each`.

Or go straight to the **[Syntax Reference](../guml_syntax.md)** for the complete language specification.
