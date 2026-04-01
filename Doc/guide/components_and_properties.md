# Components & Properties

A GUML document is a tree of **components** — Godot nodes arranged hierarchically. This guide covers how to declare components, set properties, name nodes with aliases, and use all available value types.

---

## Component Declarations

A component declaration has three parts: an optional `@alias:` prefix, a type name, and a body enclosed in `{ }`.

```
[@alias:] ComponentType {
    property_name: value,
    // child components
}
```

- **ComponentType** must start with an uppercase letter (e.g., `Label`, `VBoxContainer`, `Button`). It corresponds to a Godot node class.
- **`@alias:`** is optional. It gives the node a name so it can be accessed from the controller.
- **Properties** use `snake_case` names matching Godot's own property names.

### Minimal Example

```guml
Label {
    text: "Hello, World!",
    horizontal_alignment: .Center
}
```

### Component Tree

Nested declarations create a parent-child hierarchy in the Godot scene tree. Children are added in the order they appear:

```guml
VBoxContainer {
    Label { text: "First" }
    Label { text: "Second" }
    HBoxContainer {
        Button { text: "A" }
        Button { text: "B" }
    }
}
```

This creates:
```
VBoxContainer
├── Label ("First")
├── Label ("Second")
└── HBoxContainer
    ├── Button ("A")
    └── Button ("B")
```

---

## Property Assignments

Properties are set with `name: value`. The name is always `snake_case` and the value is a GUML expression.

```guml
Button {
    text: "Submit",
    visible: true,
    disabled: false,
    custom_minimum_size: vec2(120, 40)
}
```

### Comma Rules

- A comma is **required** between two adjacent property assignments.
- A **trailing comma** after the last property (before a child component or `}`) is optional.
- Commas are **not required** before or after child component declarations or `each` blocks.

```guml
Panel {
    visible: true,
    modulate: color(1, 1, 1, 0.9),   // trailing comma is fine

    Label { text: "no comma before child" }
    Label { text: "no comma between children either" }
}
```

---

## Naming Nodes with `@alias`

Prefix a component with `@name:` to give it a name. This creates a strongly-typed property on the controller:

```guml
VBoxContainer {
    @title_label: Label {
        text: "Hello"
    }
    @submit_btn: Button {
        text: "Submit",
        #pressed: $controller.on_submit
    }
}
```

The Source Generator produces on the controller:

```c#
public partial class MainController
{
    public Label TitleLabel { get; internal set; }
    public Button SubmitBtn { get; internal set; }
}
```

Use them in `Created()`:

```c#
public override void Created()
{
    TitleLabel.Text = "Ready!";
    SubmitBtn.Disabled = false;
}
```

**Naming convention:** alias names use `snake_case` in GUML; generated C# properties are `PascalCase`.

---

## Property Value Types

### Primitives

| Type | Example | C# Type |
|------|---------|---------|
| `string` | `"Hello"` | `System.String` |
| `int` | `42`, `-10` | `System.Int32` |
| `float` | `3.14`, `0.5` | `System.Single` |
| `boolean` | `true`, `false` | `System.Boolean` |
| `null` | `null` | `null` |

```guml
Label {
    text: "Hello",
    visible: true,
    z_index: 5,
    modulate: color(1.0, 1.0, 1.0, 0.9)
}
```

> **Note:** There are no negative number literals. Use the unary `-` operator: `-10`, `-$controller.offset`.

### Enum Values

Enum values start with `.` followed by a PascalCase member name. The concrete C# enum type is inferred from the target property:

```guml
Label {
    horizontal_alignment: .Center,
    vertical_alignment: .Fill,
    size_flags_horizontal: .ExpandFill
}
```

Resolution: `.Center` → `HorizontalAlignment.Center`, `.Fill` → `VerticalAlignment.Fill`, etc.

> **Note:** A `.` followed by a lowercase letter is **member access**, not an enum value (e.g., `$controller.name`).

### Vectors

All Godot vector types use a positional constructor syntax. Named (dictionary-style) form is also available:

```guml
Control {
    position: vec2(100, 200),        // Godot.Vector2
    size: vec2i(640, 480),           // Godot.Vector2I
    offset_left: -10,

    // Named form — useful for partial initialization
    custom_minimum_size: vec2({ x: 490 })
}
```

| Constructor | Godot Type |
|-------------|-----------|
| `vec2(x, y)` | `Vector2` |
| `vec2i(x, y)` | `Vector2I` |
| `vec3(x, y, z)` | `Vector3` |
| `vec3i(x, y, z)` | `Vector3I` |
| `vec4(x, y, z, w)` | `Vector4` |
| `vec4i(x, y, z, w)` | `Vector4I` |
| `rect2(x, y, w, h)` | `Rect2` |
| `rect2i(x, y, w, h)` | `Rect2I` |

### Color

```guml
Label {
    modulate: color(1.0, 0.5, 0.0, 1.0)   // r, g, b, a — all in range [0.0, 1.0]
}

ColorRect {
    color: color({ r: 0.2, g: 0.4, b: 0.8, a: 1.0 })  // named form
}
```

### Object Creation (`new`)

Creates an instance with property initializers. The type name must start with an uppercase letter:

```guml
Label {
    label_settings: new LabelSettings {
        font_size: 24,
        font_color: color(1.0, 0.0, 0.0, 1.0),
        outline_size: 1
    }
}
```

### Arrays

Array literals require a type prefix. They map to `T[]` or `Godot.Collections.Array<T>` depending on the target property:

```guml
Panel {
    scores: int[100, 95, 87, 92],
    names: string["Alice", "Bob", "Charlie"],
    empty: float[]
}
```

### Typed Dictionaries

Strongly-typed key-value collections:

```guml
Panel {
    score_map: Dictionary[string, int]{ "Alice": 100, "Bob": 95 }
}
```

### Object Literal (Untyped Dictionary)

An untyped `{ key: value, ... }` literal. Used for named-form struct initialization and theme overrides:

```guml
PanelContainer {
    theme_overrides: {
        panel: style_box_flat({
            bg_color: color(0.1, 0.1, 0.1, 1.0)
        })
    }
}
```

### StyleBox Types

Used for Godot theme override style properties:

| Constructor | Description |
|-------------|-------------|
| `style_box_empty()` | No background style |
| `style_box_flat({ ... })` | Solid color background, optional border/radius |
| `style_box_line({ ... })` | Line-style border |
| `style_box_texture({ ... })` | Texture-based background |

```guml
Button {
    theme_override_styles_normal: style_box_flat({
        bg_color: color(0.2, 0.2, 0.8, 1.0),
        corner_radius_top_left: 4,
        corner_radius_top_right: 4,
        corner_radius_bottom_left: 4,
        corner_radius_bottom_right: 4,
        content_margin_left: 12,
        content_margin_right: 12
    })
}
```

### Resource Loaders

Load assets from the file system. The path is a resource path string:

| Function | Return Type | Example |
|----------|-------------|---------|
| `image("res://...")` | `Texture2D` | `image("res://icon.png")` |
| `font("res://...")` | `Font` | `font("res://fonts/main.ttf")` |
| `audio("res://...")` | `AudioStream` | `audio("res://sfx/click.ogg")` |
| `video("res://...")` | `VideoStream` | `video("res://video/intro.ogv")` |

```guml
TextureRect {
    texture: image("res://sprites/player.png"),
    expand_mode: .FitWidth
}

Label {
    label_settings: new LabelSettings {
        font: font("res://fonts/pixel.ttf"),
        font_size: 16
    }
}
```

> `Guml.ResourceProvider` must be set before loading GUML files that use resource loaders.

---

## Template Strings

Template strings (prefixed with `$"`) embed expressions using `{ }`:

```guml
Label {
    text: $"Hello, {$controller.user_name}!"
}
```

Use `{{` and `}}` for literal braces. In a binding context (`:=`), the string re-evaluates whenever any referenced property changes:

```guml
Label {
    text := $"Score: {$controller.score} / {$controller.max_score}"
}
```

See [Expressions Reference](../reference/expressions.md) for the full expression language.

---

## Global References

Two built-in global references are always available:

| Reference | Description |
|-----------|-------------|
| `$controller` | The controller instance. Access properties and methods. |
| `$root` | The root control node of the current component. Used inside imported reusable components (§ see [Reusable Components](reusable_components.md)). |

```guml
Panel {
    Label {
        text := $controller.title
    }
    Button {
        #pressed: $controller.on_click
    }
}
```

In GUML, `$controller.user_name` maps to C# `controller.UserName` — all identifiers after `$controller.` use `snake_case` which is automatically converted to `PascalCase` at code generation time.

---

## Documentation Comments

Documentation comments use `///` and attach to the following declaration. They are used by the LSP for hover tooltips and completions:

```guml
/// The main action button. Disabled when the form is invalid.
/// @name submit_btn_node
@submit_btn: Button {
    text: "Submit",
    #pressed: $controller.on_submit
}
```

- `@name` — specifies a node naming marker for code generation.
- Comments are preserved as syntax metadata; regular `//` comments are discarded.

---

## Complete Example

```guml
// A user profile card component
PanelContainer {
    @card_panel: PanelContainer {
        theme_override_styles_panel: style_box_flat({
            bg_color: color(0.15, 0.15, 0.2, 1.0),
            corner_radius_top_left: 8,
            corner_radius_top_right: 8,
            corner_radius_bottom_left: 8,
            corner_radius_bottom_right: 8,
            content_margin_left: 16,
            content_margin_right: 16,
            content_margin_top: 16,
            content_margin_bottom: 16
        }),

        VBoxContainer {
            @avatar: TextureRect {
                custom_minimum_size: vec2(64, 64),
                stretch_mode: .KeepAspectCentered
            }

            @name_label: Label {
                label_settings: new LabelSettings { font_size: 18 },
                horizontal_alignment: .Center,
                text := $controller.display_name
            }

            @status_label: Label {
                horizontal_alignment: .Center,
                modulate := $controller.is_online
                            ? color(0.4, 1.0, 0.5, 1.0)
                            : color(0.6, 0.6, 0.6, 1.0),
                text := $controller.is_online ? "Online" : "Offline"
            }

            @send_btn: Button {
                text: "Send Message",
                size_flags_horizontal: .ExpandFill,
                disabled := !$controller.is_online,
                #pressed: $controller.on_send_message
            }
        }
    }
}
```

---

## Next Steps

- **[Data Binding](data_binding.md)** — learn how `:=`, `=:`, `<=>` work.
- **[Events](events.md)** — wire up signals and custom events.
- **[Types Reference](../reference/types.md)** — complete type reference with grammar.
