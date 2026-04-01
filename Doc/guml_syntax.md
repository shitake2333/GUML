# GUML Syntax

GUML (Godot UI Markup Language) is a declarative markup language for building user interfaces in Godot .NET applications. Each `.guml` file defines exactly one root component hierarchy and is paired with a C# controller class.

## Document Structure

A GUML document consists of:
1. Zero or more `import` directives (at the top of the file).
2. Exactly one root component declaration.

```
// Optional imports
import "components/header"
import "../shared/footer" as Footer

// Root component declaration
Panel {
    Header {}
    Footer {}
    Label { text: "Content" }
}
```

## Comments

GUML supports single-line comments and documentation comments.

```
// This is a single-line comment

Label {
    text: "hello" // Inline comment
}
```

Documentation comments use `///` and are associated with the immediately following declaration. They are used by tooling (LSP, code generation) for hover information.

```
/// A labeled greeting component.
/// @name greeting_label
Label {
    text: "Hello"
}
```

Documentation tag `@name` specifies a node naming marker for code generation.
Documentation tag `@param` (on `event` declarations) documents individual event arguments.

## Variables

GUML supports three kinds of variables:

### Alias Variable (`@name`)

An alias assigns a name to a UI node so it can be referenced in the controller. The alias syntax uses `@name:` before a component declaration:

```
@hello: Label {
    text: "world"
}
```

In Source Generator mode, `@hello: Label { ... }` generates a strongly-typed property `public Label Hello { get; internal set; }` on the controller, providing compile-time type checking and IDE auto-completion.

Alias names use `snake_case`. They are converted to `PascalCase` for the generated C# property name.

### Global References

- `$controller` — always refers to the controller instance bound to the current document.
- `$root` — refers to the root node of the current component. Primarily used inside imported reusable components to access parameters set by the parent.

### Local Variables

Local variables are introduced by `each` blocks. Each `each` block declares an index variable and a value variable whose scope is limited to that block.

## Import Directive

An `import` directive includes another GUML file, making its root component available for use in the current document.

```
import <guml path>
import <guml path> as <component alias>
```

- `guml path` is a string — an absolute resource path or a relative path from the current file. The `.guml` extension is optional.
- `component alias` is optional. If omitted, the component name is derived from the file name.

```
import "components/header"
import "../shared/footer" as Footer

Panel {
    Header {}
    Footer {}
}
```

Controller association is declared in C# via `[GumlController("...")]`, not by a GUML directive.

## Component Declarations

A component declaration defines a UI element. The component type must start with an uppercase letter and corresponds to a Godot node type, a custom C# class, or an imported component alias.

```
[@alias:] ComponentType {
    property_name: value,
    property_name := binding_expression,
    #signal_name: handler_expression,

    ChildComponent { ... }
}
```

- `@alias:` — optional. Names the node for controller access.
- `ComponentType` — must start with an uppercase letter (e.g., `Label`, `VBoxContainer`, `Button`).
- `property_name` — property name using `snake_case`.
- `#signal_name` — event/signal name starting with `#`, in `snake_case` (e.g., `#pressed`, `#text_changed`).
- `handler_expression` — a reference to a method or delegate on the controller. String literals are not supported.

Example:

```
@submit_btn: Button {
    text: "Submit",
    custom_minimum_size: vec2(120, 40),
    #pressed: $controller.on_submit_pressed
}
```

### Value Types

| Type | Syntax | C#/Godot Type |
|------|--------|---------------|
| `string` | `"hello"` | `System.String` |
| `int` | `42`, `-10` | `System.Int32` |
| `float` | `3.14`, `0.5` | `System.Single` |
| `boolean` | `true`, `false` | `System.Boolean` |
| `null` | `null` | `null` |
| `enum` | `.Center`, `.Fill` | Godot enum member |
| `object literal` | `{ key: value, ... }` | Named initializer / dictionary |
| `array` | `int[1, 2, 3]`, `string["a", "b"]` | `int[]` / `Godot.Collections.Array<T>` |
| `typed dictionary` | `Dictionary[string, int]{ "a": 1 }` | `Godot.Collections.Dictionary<K,V>` |
| `vec2` | `vec2(100, 200)` or `vec2({ x: 100, y: 200 })` | `Godot.Vector2` |
| `vec2i` | `vec2i(100, 200)` | `Godot.Vector2I` |
| `vec3` | `vec3(1.0, 2.0, 3.0)` | `Godot.Vector3` |
| `vec3i` | `vec3i(1, 2, 3)` | `Godot.Vector3I` |
| `vec4` | `vec4(1.0, 2.0, 3.0, 4.0)` | `Godot.Vector4` |
| `vec4i` | `vec4i(1, 2, 3, 4)` | `Godot.Vector4I` |
| `rect2` | `rect2(x, y, w, h)` | `Godot.Rect2` |
| `rect2i` | `rect2i(x, y, w, h)` | `Godot.Rect2I` |
| `color` | `color(r, g, b, a)` (0.0–1.0) | `Godot.Color` |
| `new` | `new LabelSettings { font_size: 24 }` | Object with property initializers |
| StyleBox | `style_box_empty()` / `style_box_flat({...})` / `style_box_line({...})` / `style_box_texture({...})` | `Godot.StyleBox` |
| `image` | `image("res://icon.png")` | `Godot.Texture2D` |
| `font` | `font("res://font.ttf")` | `Godot.Font` |
| `audio` | `audio("res://sound.ogg")` | `Godot.AudioStream` |
| `video` | `video("res://video.ogv")` | `Godot.VideoStream` |

**Enum values** start with `.` followed by a PascalCase member name. The concrete C# enum type is inferred from the property being assigned:

```
Label {
    horizontal_alignment: .Center,
    vertical_alignment: .Fill
}
```

**Array literals** require a type prefix:

```
Panel {
    scores: int[100, 95, 87],
    names: string["Alice", "Bob"],
    empty: int[]
}
```

**Typed dictionary literals** use `Dictionary[KeyType, ValueType]{ ... }`:

```
Panel {
    scores: Dictionary[string, int]{ "Alice": 100, "Bob": 95 }
}
```

**Struct constructors** support both positional and named (dictionary-style) forms:

```
Control {
    position: vec2(100, 200),          // positional
    modulate: color({ r: 1.0, g: 0.5, b: 0.0, a: 1.0 })  // named
}
```

## Child Components

Any component declaration can contain nested child components, forming a tree. Children are added to the parent in the order they appear.

```
Panel {
    Label {
        text: "hello"
    }

    Panel {
        Button {}
        Button {}
    }
}
```

## Expressions

### String Literals

Regular strings are enclosed in double quotes. Supported escape sequences: `\\`, `\"`, `\n`, `\t`, `\r`.

```
Label { text: "Hello, World!" }
Label { text: "Line1\nLine2" }
```

### Template Strings (Interpolation)

Template strings start with `$"` and allow embedding expressions inside `{ }`:

```
Label {
    text: $"Hello, {$controller.user_name}!",
    tooltip := $"Score: {$controller.score} / {$controller.max_score}"
}
```

- Use `{{` and `}}` for literal `{` and `}` inside a template string.
- When used in a binding (`:=`), all embedded references become binding sources — the string is re-evaluated whenever any source changes.

### Arithmetic and Logical Operators

| Precedence | Operators | Description |
|------------|-----------|-------------|
| 70 | `!` `+` `-` (unary) | Logical NOT, unary plus/minus |
| 50 | `*` `/` `%` | Multiplication, division, remainder |
| 40 | `+` `-` | Addition, subtraction |
| 30 | `<` `>` `<=` `>=` | Comparison |
| 20 | `==` `!=` | Equality |
| 15 | `&&` | Logical AND |
| 10 | `\|\|` | Logical OR |
| 5 | `? :` | Conditional (ternary) |

Parentheses `( )` can be used to override precedence.

> **Note:** `+` only applies to numeric types. For string composition, use template strings `$"..."`.

### Conditional (Ternary) Expression

```
text: $controller.is_vip ? "VIP User" : "Normal User"
modulate := $controller.health > 50 ? color(0, 1, 0, 1) : color(1, 0, 0, 1)
```

### Member Access

Use `.` to access properties on a reference:

```
text := $controller.data.name
```

`$controller.data.name` in GUML maps to `controller.Data.Name` in C# (snake_case → PascalCase).

### Call Expressions

Methods can be called in expressions, including binding contexts:

```
Label {
    text := $controller.format_name($controller.first_name, $controller.last_name),
    visible := $controller.should_show(idx)
}
```

When used in a binding (`:=`), the method is re-invoked whenever any argument reference changes.

### Object Creation (`new`)

The `new` keyword creates an instance of a type with property initializers:

```
Label {
    label_settings: new LabelSettings {
        font_size: 24,
        font_color: color(1.0, 0.0, 0.0, 1.0)
    }
}
```

### Translation Expressions (i18n)

GUML provides built-in `tr()` and `ntr()` expressions for internationalization, following the gettext convention. Translation is performed at runtime through `Guml.StringProvider` (an `IStringProvider` implementation). When no provider is configured, the original text is returned as-is.

#### `tr()` — Singular Translation

`tr()` translates a message identifier (msgid). The msgid is the source-language text itself (not an abstract key).

```
// Simple translation
Label { text: tr("Start Game") }

// With named placeholders — {name} is replaced by the translated string
Label { text := tr("Hello, {name}!", { name: $controller.user_name }) }

// With disambiguation context (gettext msgctxt)
Label { text: tr("File", { context: "noun" }) }

// Context + placeholders
Label { text := tr("Hello, {name}!", { context: "formal", name: $controller.user_name }) }
```

The optional second argument is an **options object** containing:
- `context` (reserved key): A string providing disambiguation context for the same msgid.
- Other keys: Named placeholder values. Each key maps to a `{key}` placeholder in the translated text.

#### `ntr()` — Plural Translation

`ntr()` selects between singular and plural forms based on a count value (gettext `ngettext`).

```
// Basic singular/plural
Label { text := ntr("One item", "{count} items", $controller.item_count) }

// With named placeholders
Label { text := ntr("One item", "{count} items", $controller.item_count,
                    { count: $controller.item_count }) }

// With disambiguation context
Label { text := ntr("One apple", "{count} apples", $controller.n,
                    { context: "fruit", count: $controller.n }) }
```

Arguments: `ntr(singular_msgid, plural_msgid, count_expression [, options_object])`

#### Combination with Template Strings

`tr()` and `ntr()` are ordinary expressions and can be used inside template string interpolation:

```
// Translation fragment in template string
Label { text := $"[{tr("Role")}] {$controller.user_name}" }

// Template string as a placeholder value
Label { text: tr("Score: {detail}", { detail: $"({$controller.score}/{$controller.max})" }) }
```

> **Guideline:** Use `tr()` with named placeholders for full sentences that translators need to reorder. Use template strings for UI-level assembly (brackets, separators) that doesn't need translation.

#### Binding Behavior

When used with `:=`, translation expressions automatically track locale changes. Both `$controller` property changes and locale switches trigger re-evaluation.

## Property Mapping

GUML supports four mapping operators between a component property and a value or data expression:

| Operator | Direction | Description |
|----------|-----------|-------------|
| `:` | static | One-time assignment at build time. |
| `:=` | data → property | One-way binding: re-evaluates when data changes. |
| `=:` | property → data | One-way binding: writes back when component property changes. |
| `<=>` | property ↔ data | Two-way binding combining both directions. |

```
Label {
    text: "static value",          // evaluated once
    text := $controller.user_name, // re-evaluated when UserName changes
}

CustomToggle {
    checked <=> $controller.is_enabled  // two-way
}
```

For `=:` and `<=>`, the right-hand side must be a writable reference (e.g., a settable controller property).

Built-in Godot UI nodes generally support `:=` only. Custom nodes that provide property change notifications can support `=:` and `<=>`.

## List Rendering (`each`)

The `each` block iterates over a collection and renders child components for each item.

### Block Form

```
each <data source> { |<index>, <value>|
    // component declarations per item
}
```

Example:

```
VBoxContainer {
    each $controller.names { |idx, name|
        Label {
            text: name
        }
    }
}
```

### With Optional Parameters

An optional `(object literal)` before the data source passes configuration to the each block:

```
GridContainer {
    each ({ columns: 3 }) $controller.images { |i, img|
        TextureRect {
            texture: img
        }
    }
}
```

### Projection Form

When a component declares a template parameter via `param`, the `each` block can use projection form (`=>`):

```
// In ListView.guml
VBoxContainer {
    param IEnumerable items,
    param Node item_template,

    each $root.items => item_template
}

// In InventoryPage.guml
Panel {
    ListView {
        items: $controller.items,
        item_template => ItemRow {
            text: $"{$item.index}: {$item.value.display_name}"
        }
    }
}
```

In projection-instantiated templates, a read-only `$item` context is available:

| Member | Description |
|--------|-------------|
| `$item.index` | Current element index. |
| `$item.value` | Current element value. |
| `$item.root` | Root node of the template component instance. |

### Scoping

Each `each` block creates its own scope. In nested `each` blocks (with an intermediate component), the inner scope's parent points to the outer scope, forming a scope chain — similar to JavaScript.

```
Panel {
    each $controller.rows { |row_idx, row|
        VBoxContainer {
            each row.items { |col_idx, item|
                Label { text: item.name }
            }
        }
    }
}
```

### Nesting Rules

`each` blocks cannot be directly nested. An intermediate component is required.

❌ **Error:**
```
each $controller.data_a { |idx_a, val_a|
    each $controller.data_b { |idx_b, val_b|
        Label {}
    }
}
```

✔️ **Correct:**
```
each $controller.data_a { |idx_a, val_a|
    Control {
        each $controller.data_b { |idx_b, val_b|
            Label {}
        }
    }
}
```

### Incremental Updates

When the data source (a `NotifyList<T>`) changes:
- **Add**: Only the newly added item's nodes are created. Existing nodes are untouched.
- **Remove / Insert / Clear**: All `each` children are fully re-rendered.

## Parameter Declarations

Reusable GUML components can declare typed parameters using the `param` keyword. Parameters appear only in the **root component body** and define the component's public interface. Generic types are supported.

```
param <type> <name>
param <type> <name>: <default value>
```

- *type* supports generic syntax: `List<int>`, `Dictionary<String, List<int>>`, etc.
- *name* must be `snake_case`.
- Parameters with a default value are **optional**; without defaults they are **required**.

Example:

```
VBoxContainer {
    param string title,                       // required
    param string subtitle: "No subtitle",     // optional
    param List<InventoryItem> items,          // generic type

    Label { text: title }
}
```

When using the component, required parameters must be provided:

```
import "components/MyCard"

Panel {
    MyCard { title: "Hello" }         // ✔️
    // MyCard { subtitle: "world" }   // ❌ missing required 'title'
}
```

Inside the component body, parameters are referenced directly by name (e.g., `title`). From outside (projection), use `$root.<param_name>`.

## Event Declarations

Custom components can declare events to allow parent components to subscribe. Event declarations appear only in the **root component body**.

```
event <name>
event <name>(<type> <arg_name>, ...)
```

- *name* must be `snake_case`. It is converted to `PascalCase` in generated C#.
- Arguments are optional. Each argument has a type and a name.

Example — declaring events in `SearchBar.guml`:

```
Panel {
    event on_search(string query),
    event on_clear,

    // ...component body...
}
```

Subscribing to events from the parent:

```
import "components/SearchBar"

Panel {
    SearchBar {
        #on_search: $controller.handle_search,
        #on_clear: $controller.handle_clear
    }
}
```

Documentation comments with `@param` can document event arguments:

```
/// Emitted when the selection changes.
/// @param index The zero-based index of the selected item.
/// @param value The selected value.
event on_selection_changed(int index, string value)
```

## Comma Separators

Commas separate adjacent **value-assignment members** within a component body. Value-assignment members include property assignments, property mappings, event subscriptions, parameter declarations, event declarations, and template parameter assignments.

**Rules:**
1. A comma is **required** between two adjacent value-assignment members.
2. A **trailing comma** after the last value-assignment member is **optional**.
3. Commas are **not required** before or after **structural elements**: child component declarations, `each` blocks, and alias-prefixed declarations (`@name: Component { ... }`).

```
Panel {
    text: "hello",
    visible: true,         // comma required between value-assignments

    Label { text: "child" }  // no comma needed around structural children

    size: vec2(100, 200),  // trailing comma is optional
}
```

## Keywords

The following identifiers are reserved and cannot be used as property names:

`import` `as` `each` `param` `event` `new` `image` `font` `audio` `video` `tr` `ntr`

The following built-in constructor names are also reserved:

`vec2` `vec2i` `vec3` `vec3i` `vec4` `vec4i` `rect2` `rect2i` `color` `style_box_empty` `style_box_flat` `style_box_line` `style_box_texture`

## Identifier Naming Conventions

| Category | Convention | Examples |
|----------|-----------|---------|
| Component types, type names | PascalCase | `Label`, `VBoxContainer`, `LabelSettings` |
| Enum member values | `.PascalCase` | `.Center`, `.Fill`, `.Left` |
| Godot node properties | snake_case | `text`, `custom_minimum_size` |
| Controller members (in GUML) | snake_case (mapped to PascalCase in C#) | `$controller.user_name` → `UserName` |
| Parameter names | snake_case | `param string button_text` |
| Event names | snake_case | `event on_submit` |
| Alias references | snake_case | `@submit_btn`, `@name_input` |
| Event references | snake_case | `#pressed`, `#text_changed` |
