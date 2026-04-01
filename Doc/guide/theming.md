# Theming & Styling

GUML provides first-class support for Godot's theme system. You can override theme properties directly in the markup using either the explicit `theme_override_*` Godot property names or the shorthand `theme_overrides` object syntax.

---

## Two Theming Syntaxes

### 1. Explicit Property Syntax

Use Godot's full `theme_override_*` property names as regular component properties:

```guml
Button {
    text: "Click",
    theme_override_colors_font_color: color(1.0, 1.0, 1.0, 1.0),
    theme_override_constants_outline_size: 1,
    theme_override_styles_normal: style_box_flat({
        bg_color: color(0.2, 0.4, 0.8, 1.0)
    })
}
```

This maps directly to Godot's `AddThemeColorOverride("font_color", ...)`, `AddThemeConstantOverride("outline_size", ...)`, etc.

### 2. Shorthand `theme_overrides` Syntax

The `theme_overrides` property accepts an untyped object literal. Keys are the override names; values use the same expressions as regular properties:

```guml
PanelContainer {
    theme_overrides: {
        panel: style_box_flat({
            bg_color: color(0.1, 0.1, 0.15, 1.0)
        })
    }
}

VBoxContainer {
    theme_overrides: {
        separation: 8
    }
}

Button {
    theme_overrides: {
        font_color: color(1.0, 1.0, 1.0, 1.0),
        font_hover_color: color(0.8, 0.9, 1.0, 1.0),
        normal: style_box_flat({ bg_color: color(0.2, 0.4, 0.8, 1.0) }),
        hover:  style_box_flat({ bg_color: color(0.3, 0.5, 0.9, 1.0) })
    }
}
```

The GUML runtime resolves the type of each key from the node's theme entry type and calls the appropriate `AddTheme*Override` method.

---

## Theme Override Types

Godot classifies theme entries into six types. The table below shows the GUML value to use for each:

| Theme Entry Type | GUML Value | Example |
|-----------------|-----------|---------|
| `Constant` | `int` literal | `separation: 8` |
| `Color` | `color(r, g, b, a)` | `font_color: color(1, 1, 1, 1)` |
| `Font` | `font("res://...")` | `font: font("res://fonts/ui.ttf")` |
| `FontSize` | `int` literal | `font_size: 16` |
| `Icon` | `image("res://...")` | `checked: image("res://icons/check.png")` |
| `Style` | `style_box_*()` | `panel: style_box_flat({ bg_color: ... })` |

---

## StyleBox Types

GUML exposes all four Godot StyleBox variants via struct constructor syntax:

### `style_box_empty()`

An invisible style with no background. Useful for removing inherited panel backgrounds:

```guml
PanelContainer {
    theme_overrides: {
        panel: style_box_empty()
    }
}
```

### `style_box_flat({ ... })`

A solid color background with optional border and corner radii:

```guml
PanelContainer {
    theme_overrides: {
        panel: style_box_flat({
            bg_color: color(0.12, 0.12, 0.18, 1.0),
            border_color: color(0.3, 0.3, 0.5, 1.0),
            border_width_left: 1,
            border_width_right: 1,
            border_width_top: 1,
            border_width_bottom: 1,
            corner_radius_top_left: 6,
            corner_radius_top_right: 6,
            corner_radius_bottom_left: 6,
            corner_radius_bottom_right: 6,
            content_margin_left: 12,
            content_margin_right: 12,
            content_margin_top: 8,
            content_margin_bottom: 8
        })
    }
}
```

**Common `style_box_flat` properties:**

| Property | Type | Description |
|----------|------|-------------|
| `bg_color` | `color` | Background fill color |
| `border_color` | `color` | Border color |
| `border_width_left/right/top/bottom` | `int` | Border thickness (px) |
| `corner_radius_top_left/top_right/bottom_left/bottom_right` | `int` | Corner radius (px) |
| `corner_detail` | `int` | Curve smoothness (default 8) |
| `content_margin_left/right/top/bottom` | `int` / `float` | Inner padding |
| `anti_aliased` | `bool` | Enable anti-aliasing on corners |
| `skew` | `vec2` | Horizontal and vertical skew |
| `shadow_offset` | `vec2` | Shadow position offset |
| `shadow_color` | `color` | Shadow color |
| `shadow_size` | `int` | Shadow blur radius |

### `style_box_line({ ... })`

A line border. Useful for separators or outline effects:

```guml
PanelContainer {
    theme_overrides: {
        panel: style_box_line({
            color: color(0.5, 0.5, 0.5, 1.0),
            thickness: 1,
            grow_begin: 0,
            grow_end: 0
        })
    }
}
```

| Property | Type | Description |
|----------|------|-------------|
| `color` | `color` | Line color |
| `thickness` | `int` | Line width (px) |
| `grow_begin` | `float` | Extend the line at the start |
| `grow_end` | `float` | Extend the line at the end |
| `vertical` | `bool` | Draw a vertical line instead |

### `style_box_texture({ ... })`

A nine-patch texture-based style:

```guml
PanelContainer {
    theme_overrides: {
        panel: style_box_texture({
            texture: image("res://ui/panel_bg.png"),
            margin_left: 8,
            margin_right: 8,
            margin_top: 8,
            margin_bottom: 8
        })
    }
}
```

---

## Common Node Theme Overrides

### `VBoxContainer` / `HBoxContainer`

| Key | Type | Description |
|-----|------|-------------|
| `separation` | Constant | Pixel gap between children |

### `PanelContainer` / `ScrollContainer`

| Key | Type | Description |
|-----|------|-------------|
| `panel` | Style | The background panel style |

### `Button`

| Key | Type | Description |
|-----|------|-------------|
| `normal` | Style | Default button background |
| `hover` | Style | Mouse-over background |
| `pressed` | Style | Click-down background |
| `disabled` | Style | Disabled state background |
| `focus` | Style | Focus ring |
| `font_color` | Color | Text color (normal) |
| `font_hover_color` | Color | Text color (hover) |
| `font_pressed_color` | Color | Text color (pressed) |
| `font_disabled_color` | Color | Text color (disabled) |
| `font_outline_color` | Color | Text outline color |
| `outline_size` | Constant | Text outline thickness |
| `h_separation` | Constant | Gap between icon and text |
| `font` | Font | Override font |
| `font_size` | FontSize | Override font size |

### `TabContainer`

| Key | Type | Description |
|-----|------|-------------|
| `tab_selected` | Style | Active tab background |
| `tab_unselected` | Style | Inactive tab background |
| `tab_hovered` | Style | Hovered tab background |
| `tab_disabled` | Style | Disabled tab background |
| `panel` | Style | Tab body background |
| `font_selected_color` | Color | Active tab text |
| `font_unselected_color` | Color | Inactive tab text |

---

## Global Default Theme

Set `Guml.DefaultTheme` to apply a Godot `Theme` resource to all loaded GUML views:

```c#
// In your main scene _Ready:
Guml.ResourceProvider = new DefaultResourceProvider();

var theme = ResourceLoader.Load<Theme>("res://assets/themes/main_theme.tres");
// If Guml.DefaultTheme is supported, set it here:
// Guml.DefaultTheme = theme;

var controller = Guml.Load<MainController>(this);
```

> Note: Check the current API documentation for whether `Guml.DefaultTheme` is available in your installed version. Theme resources can also be set directly on the `GumlRootNode` after loading.

---

## Complete Styled Panel Example

```guml
VBoxContainer {
    theme_overrides: { separation: 12 }

    @header: PanelContainer {
        theme_overrides: {
            panel: style_box_flat({
                bg_color: color(0.1, 0.1, 0.2, 1.0),
                corner_radius_top_left: 8,
                corner_radius_top_right: 8,
                content_margin_left: 16,
                content_margin_right: 16,
                content_margin_top: 12,
                content_margin_bottom: 12
            })
        }

        Label {
            text := $controller.title,
            label_settings: new LabelSettings {
                font_size: 20,
                font_color: color(0.9, 0.9, 1.0, 1.0)
            }
        }
    }

    @content: PanelContainer {
        theme_overrides: {
            panel: style_box_flat({
                bg_color: color(0.08, 0.08, 0.14, 1.0),
                corner_radius_top_left: 0,
                corner_radius_top_right: 0,
                corner_radius_bottom_left: 8,
                corner_radius_bottom_right: 8,
                content_margin_left: 16,
                content_margin_right: 16,
                content_margin_top: 16,
                content_margin_bottom: 16
            })
        }

        VBoxContainer {
            theme_overrides: { separation: 8 }

            Label { text := $controller.description }

            @action_btn: Button {
                text: "Confirm",
                theme_overrides: {
                    normal:   style_box_flat({ bg_color: color(0.2, 0.5, 0.9, 1.0), corner_radius_top_left: 4, corner_radius_top_right: 4, corner_radius_bottom_left: 4, corner_radius_bottom_right: 4 }),
                    hover:    style_box_flat({ bg_color: color(0.3, 0.6, 1.0, 1.0), corner_radius_top_left: 4, corner_radius_top_right: 4, corner_radius_bottom_left: 4, corner_radius_bottom_right: 4 }),
                    pressed:  style_box_flat({ bg_color: color(0.1, 0.4, 0.8, 1.0), corner_radius_top_left: 4, corner_radius_top_right: 4, corner_radius_bottom_left: 4, corner_radius_bottom_right: 4 }),
                    disabled: style_box_flat({ bg_color: color(0.2, 0.2, 0.3, 1.0), corner_radius_top_left: 4, corner_radius_top_right: 4, corner_radius_bottom_left: 4, corner_radius_bottom_right: 4 })
                },
                #pressed: $controller.on_confirm
            }
        }
    }
}
```

---

## Next Steps

- **[Components & Properties](components_and_properties.md)** — full value type reference including all struct constructors.
- **[Source Generator](source_generator.md)** — configure the generator to compile theming at build time.
