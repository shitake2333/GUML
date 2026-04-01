# Reusable Components

GUML supports building reusable UI components that can be composed across multiple documents. A reusable component is a regular `.guml` file that declares `param` and `event` directives to define its public interface.

---

## Creating a Reusable Component

Any `.guml` file can be treated as a reusable component. `param` and `event` declarations must appear **inside the root component's braces**, after any property assignments and before children:

```guml
// components/card.guml
PanelContainer {
    param string title: "Card"
    param string body: ""
    param boolean show_footer: true
    event on_close

    VBoxContainer {
        Label { text := $root.title }
        Label { text := $root.body }

        Button {
            text: "Close",
            visible: $root.show_footer,
            #pressed: $root.on_close
        }
    }
}
```

---

## Importing a Component

Use the `import` directive at the top of a `.guml` file (before the root component) to bring in another component:

```guml
import "components/card.guml" as Card

VBoxContainer {
    Card {
        title: "Notice",
        body: "Your session will expire soon.",
        #on_close: $controller.on_notice_closed
    }
}
```

The alias (e.g., `Card`) is the name used as the component type in the document. It must start with an uppercase letter.

### Import with `res://` Path

Use an absolute Godot resource path to reference components from anywhere in the project:

```guml
import "res://gui/components/card.guml" as Card
```

Relative paths are resolved from the importing file's directory.

---

## Accessing Parameters — `$root`

Inside a reusable component, use `$root.<param_name>` to access the declared parameters. `$root` refers to the component's own root node and parameter scope — not the hosting document's root:

```guml
// components/progress_bar_labeled.guml
HBoxContainer {
    param string label: "Progress"
    param float value: 0.0
    param float max_value: 100.0

    Label { text := $root.label }

    ProgressBar {
        size_flags_horizontal: .ExpandFill,
        value := $root.value,
        max_value := $root.max_value
    }

    Label {
        text := $"{$root.value}/{$root.max_value}"
    }
}
```

---

## Parameter Declarations

### Required Parameters

A parameter without a default value is **required** — the parent must supply it:

```guml
PanelContainer {
    param string name         // required — no default
    param int score           // required

    Label { text := $"{$root.name}: {$root.score}" }
}
```

Omitting a required parameter at the use site is a compile-time error.

### Optional Parameters (with Default)

```guml
PanelContainer {
    param string title: "Default Title"   // optional with literal default
    param boolean closeable: true
    param float opacity: 1.0
}
```

### Dynamic Default (`:=`)

A parameter with `:=` default is optional but pulls its default from the controller binding context:

```guml
PanelContainer {
    param float opacity := 1.0   // default is the literal 1.0, but `:=` enables binding
}
```

### Generic Parameters

For collection types or typed dictionaries, use the generic syntax:

```guml
ScrollContainer {
    param IEnumerable items
    param ObservableCollection<string> labels: null

    VBoxContainer {
        each $root.items { |i, item|
            Label { text := item }
        }
    }
}
```

---

## Event Declarations

Events expose points where the child component notifies the parent of user actions.

### No-Argument Event

```guml
PanelContainer {
    event on_confirm
    event on_cancel

    HBoxContainer {
        Button { text: "OK",     #pressed: $root.on_confirm }
        Button { text: "Cancel", #pressed: $root.on_cancel }
    }
}
```

### Event with Arguments

```guml
PanelContainer {
    event on_value_changed(string new_value)
    event on_count_changed(int count)

    LineEdit {
        #text_changed: $controller.on_text_changed
    }
}
```

The controller of the component raises the event by delegating the signal to `$root.event_name`.

---

## Subscribing to Events

In the parent document, subscribe using `#event_name: handler`:

```guml
import "components/card.guml" as Card

VBoxContainer {
    Card {
        title: "Settings",
        body: "Configure your preferences.",
        show_footer: true,
        #on_close: $controller.on_settings_closed
    }
}
```

```c#
public void OnSettingsClosed()
{
    SettingsVisible = false;
}
```

---

## Multiple Imports

A document can import multiple components:

```guml
import "components/header.guml" as Header
import "components/sidebar.guml" as Sidebar
import "components/footer.guml" as Footer

VBoxContainer {
    Header { title := $controller.page_title }

    HBoxContainer {
        size_flags_vertical: .ExpandFill

        Sidebar { #nav_changed: $controller.on_navigate }

        @content_area: PanelContainer {
            size_flags_horizontal: .ExpandFill
        }
    }

    Footer { version: "1.0.0" }
}
```

The Source Generator creates typed properties on the host controller for each imported component whose controller type is known. See [Source Generator](source_generator.md) for details.

---

## Content Projection

The projection form of `each` lets the parent define the per-item template, while the component handles iteration:

```guml
// components/ListView.guml
ScrollContainer {
    param IEnumerable items
    param Node item_template

    size_flags_vertical: .Fill,
    size_flags_horizontal: .Fill

    VBoxContainer {
        size_flags_horizontal: .Fill
        each $root.items => item_template
    }
}
```

The parent supplies the body:

```guml
import "components/ListView.guml" as ListView

ListView {
    items: $controller.entries,
    |idx, entry|
    HBoxContainer {
        Label {
            text := $"{idx + 1}. {entry.name}",
            size_flags_horizontal: .Fill
        }
        Label { text := entry.value }
    }
}
```

> In Source Generator mode, a plain `each` block in the parent document is the recommended approach for projection. The `item_template` pattern is primarily for interpreter mode.

---

## Full Example — Confirm Dialog

```guml
// components/ConfirmDialog.guml
PanelContainer {
    param string title: "Confirm"
    param string message: "Are you sure?"
    param string confirm_text: "OK"
    param string cancel_text: "Cancel"
    event on_confirm
    event on_cancel

    theme_override_styles_panel: style_box_flat({
        bg_color: color(0.15, 0.15, 0.2, 1.0),
        corner_radius_top_left: 8,
        corner_radius_top_right: 8,
        corner_radius_bottom_left: 8,
        corner_radius_bottom_right: 8
    }),

    VBoxContainer {
        Label {
            text := $root.title,
            label_settings: new LabelSettings { font_size: 18 }
        }
        Label { text := $root.message }
        HBoxContainer {
            Button {
                text := $root.confirm_text,
                size_flags_horizontal: .ExpandFill,
                #pressed: $root.on_confirm
            }
            Button {
                text := $root.cancel_text,
                size_flags_horizontal: .ExpandFill,
                #pressed: $root.on_cancel
            }
        }
    }
}
```

```guml
// gui/main.guml
import "components/ConfirmDialog.guml" as ConfirmDialog

VBoxContainer {
    Button {
        text: "Delete Item",
        #pressed: $controller.show_confirm
    }

    @confirm_dialog: ConfirmDialog {
        title: "Delete Item",
        message: "This cannot be undone.",
        confirm_text: "Delete",
        cancel_text: "Keep",
        visible := $controller.confirm_visible,
        #on_confirm: $controller.on_delete,
        #on_cancel: $controller.on_cancel_delete
    }
}
```

---

## Next Steps

- **[Source Generator](source_generator.md)** — see how import controllers get typed properties.
- **[Events](events.md)** — detailed event handling patterns.
- **[List Rendering](list_rendering.md)** — use `param` collections with `each` in nested components.
