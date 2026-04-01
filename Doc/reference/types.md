# Types Reference

Complete reference for all GUML value types and their C# mappings.

---

## Primitive Types

| GUML Type | Literal Form | C# Type | Notes |
|-----------|-------------|---------|-------|
| `string` | `"Hello"` | `System.String` | UTF-16. Escape `\\`, `\"`, `\n`, `\t`. |
| `int` | `42`, `-10` | `System.Int32` | No negative literals; use unary `-`. |
| `float` | `3.14`, `0.5` | `System.Single` | Accepts integer literals where float expected. |
| `boolean` | `true`, `false` | `System.Boolean` | Keywords, lowercase only. |
| `null` | `null` | `null` | Keyword. |

---

## Enum Values

Enum values are prefixed with `.` followed by a PascalCase member name:

```
enum_value : '.' UPPERCASE_LETTER IDENTIFIER_PART*
```

The concrete C# enum type is inferred from the target property. The `.` prefix is only interpreted as an enum when it is NOT immediately preceded by an identifier character (to distinguish it from member access like `$controller.center`).

**Examples:**

```guml
Label {
    horizontal_alignment: .Center,   // HorizontalAlignment.Center
    vertical_alignment: .Fill        // VerticalAlignment.Fill
}
TextureRect {
    expand_mode: .FitWidth           // TextureRect.ExpandModeEnum.FitWidth
}
Control {
    size_flags_horizontal: .ExpandFill
}
```

---

## Object Literal

An untyped key-value map enclosed in `{ }`. Keys are identifiers:

```
object_literal : '{' (key ':' expr (',' key ':' expr)*)? '}'
```

**Uses:**
- Named-form struct initializer: `type({ key: value })`
- `theme_overrides` property
- Passing configuration values

```guml
Panel {
    theme_overrides: { separation: 8, bg: color(0, 0, 0, 1) }
}
```

---

## Array Literal

A typed ordered collection. Element type is required:

```
array_literal : type_name '[' (expr (',' expr)*)? ']'
```

| GUML Syntax | C# Type |
|-------------|---------|
| `int[1, 2, 3]` | `int[]` or `Array<int>` |
| `string["a", "b"]` | `string[]` or `Array<string>` |
| `float[]` | Empty `float[]` |

The concrete collection type (`T[]` vs `Godot.Collections.Array<T>`) is selected based on the target property's declared type.

---

## Typed Dictionary

A strongly-typed key-value map:

```
typed_dictionary : component_name '[' type ',' type ']' '{' (expr ':' expr (',' expr ':' expr)*)? '}'
```

```guml
Panel {
    score_map: Dictionary[string, int]{ "Alice": 100, "Bob": 95 },
    config:    Dictionary[int, string]{ 1: "one", 2: "two" }
}
```

---

## Struct Constructor Types

All Godot struct/object types use a unified constructor syntax:

```
struct_expr : struct_type_name '(' init ')' 
init        : positional_args | named_init
named_init  : object_literal
```

### Vector Types

| Constructor | Godot Type | Example |
|-------------|-----------|---------|
| `vec2(x, y)` | `Vector2` | `vec2(100.0, 200.0)` |
| `vec2i(x, y)` | `Vector2I` | `vec2i(640, 480)` |
| `vec3(x, y, z)` | `Vector3` | `vec3(1.0, 0.0, 0.0)` |
| `vec3i(x, y, z)` | `Vector3I` | `vec3i(1, 2, 3)` |
| `vec4(x, y, z, w)` | `Vector4` | `vec4(0.0, 0.0, 1.0, 1.0)` |
| `vec4i(x, y, z, w)` | `Vector4I` | `vec4i(0, 0, 1, 1)` |
| `rect2(x, y, w, h)` | `Rect2` | `rect2(0, 0, 100, 50)` |
| `rect2i(x, y, w, h)` | `Rect2I` | `rect2i(0, 0, 640, 480)` |

Named-form also available for partial initialization:

```guml
Control {
    custom_minimum_size: vec2({ x: 200 })   // y defaults to 0
}
```

### Color

```
color(r, g, b, a)   // all floats in [0.0 .. 1.0]
color({ r, g, b, a })
```

```guml
Label { modulate: color(1.0, 0.5, 0.0, 1.0) }
ColorRect { color: color({ r: 0.2, g: 0.4, b: 0.8, a: 1.0 }) }
```

### StyleBox Types

| Constructor | Godot Type | Description |
|-------------|-----------|-------------|
| `style_box_empty()` | `StyleBoxEmpty` | No background |
| `style_box_flat({ ... })` | `StyleBoxFlat` | Solid color box |
| `style_box_line({ ... })` | `StyleBoxLine` | Line border |
| `style_box_texture({ ... })` | `StyleBoxTexture` | Nine-patch texture |

**`style_box_flat` properties:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `bg_color` | `color` | `color(0,0,0,0)` | Background color |
| `border_color` | `color` | `color(0,0,0,0)` | Border color |
| `border_width_left` | `int` | `0` | Left border px |
| `border_width_right` | `int` | `0` | Right border px |
| `border_width_top` | `int` | `0` | Top border px |
| `border_width_bottom` | `int` | `0` | Bottom border px |
| `corner_radius_top_left` | `int` | `0` | Top-left corner radius |
| `corner_radius_top_right` | `int` | `0` | Top-right corner radius |
| `corner_radius_bottom_left` | `int` | `0` | Bottom-left corner radius |
| `corner_radius_bottom_right` | `int` | `0` | Bottom-right corner radius |
| `corner_detail` | `int` | `8` | Corner curve smoothness |
| `anti_aliased` | `bool` | `true` | Anti-aliasing |
| `content_margin_left` | `float` | `0` | Left inner padding |
| `content_margin_right` | `float` | `0` | Right inner padding |
| `content_margin_top` | `float` | `0` | Top inner padding |
| `content_margin_bottom` | `float` | `0` | Bottom inner padding |
| `shadow_color` | `color` | transparent | Shadow color |
| `shadow_offset` | `vec2` | `vec2(0,0)` | Shadow offset |
| `shadow_size` | `int` | `0` | Shadow blur size |
| `skew` | `vec2` | `vec2(0,0)` | Horizontal/vertical skew |

**`style_box_line` properties:**

| Property | Type | Description |
|----------|------|-------------|
| `color` | `color` | Line color |
| `thickness` | `int` | Line width (px) |
| `grow_begin` | `float` | Extension at start |
| `grow_end` | `float` | Extension at end |
| `vertical` | `bool` | Vertical line |

**`style_box_texture` properties:**

| Property | Type | Description |
|----------|------|-------------|
| `texture` | `image(...)` | The nine-patch texture |
| `margin_left/right/top/bottom` | `int` | Nine-patch margins |
| `texture_margin_left/right/top/bottom` | `int` | Texture UV margins |

---

## Object Creation (`new`)

Instantiates an arbitrary class with property initializers:

```
new_expr : 'new' ComponentName '{' (property ':' expr (',' property ':' expr)*)? '}'
```

```guml
Label {
    label_settings: new LabelSettings {
        font_size: 24,
        font_color: color(1.0, 0.0, 0.0, 1.0),
        outline_size: 1,
        outline_color: color(0.0, 0.0, 0.0, 1.0)
    }
}
```

The type name must start with an uppercase letter (PascalCase). Properties use `snake_case` matching Godot's property names.

---

## Resource Loaders

Four built-in resource-loading functions. Paths must be Godot resource paths (`res://...`) or paths relative to the project root:

| Function | Return Type | Path Format |
|----------|-------------|-------------|
| `image("path")` | `Texture2D` | `"res://sprites/foo.png"` |
| `font("path")` | `Font` | `"res://fonts/ui.ttf"` |
| `audio("path")` | `AudioStream` | `"res://sfx/click.ogg"` |
| `video("path")` | `VideoStream` | `"res://video/intro.ogv"` |

```guml
TextureRect {
    texture: image("res://icon.png")
}
Label {
    label_settings: new LabelSettings {
        font: font("res://fonts/pixel.ttf"),
        font_size: 16
    }
}
```

`Guml.ResourceProvider` must be set before loading any GUML file that uses these functions.

---

## Parameter Types (Reusable Components)

Parameters declared with `param` support additional generic and interface types:

| GUML Syntax | C# Type | Notes |
|-------------|---------|-------|
| `param string foo` | `string` | Required parameter |
| `param int count: 0` | `int` | Optional with default |
| `param boolean flag: true` | `bool` | |
| `param float ratio: 1.0` | `float` | |
| `param IEnumerable items` | `IEnumerable` | Non-generic collection |
| `param NotifyList<T> list` | `NotifyList<T>` | Reactive list |
| `param Node template` | `Node` | Content projection slot |

See [Reusable Components](../guide/reusable_components.md) for usage details.

---

## Type Naming Conventions

| Context | Convention | Example |
|---------|-----------|---------|
| Component types | `PascalCase` | `Label`, `VBoxContainer` |
| Property names | `snake_case` | `font_size`, `horizontal_alignment` |
| Alias names | `snake_case` | `@title_label:` |
| Enum members | `.PascalCase` | `.Center`, `.ExpandFill` |
| Parameter names | `snake_case` | `param string user_name` |
| Event names | `snake_case` | `event on_confirmed` |
| Import aliases | `PascalCase` | `import "..." as MyCard` |

---

## Related

- [Expressions Reference](expressions.md) — operators, template strings, member access.
- [Types Guide: Components & Properties](../guide/components_and_properties.md) — worked examples.
- [Theming Guide](../guide/theming.md) — StyleBox and theme override details.
