# Expressions Reference

Complete reference for all GUML expressions, operators, and their semantics.

---

## Expression Categories

Every expression is classified as:

- **Value expression** — evaluates to a concrete value (literal, struct, array, resource, `new`)
- **Reference expression** — navigates properties on a controller or scoped context (`$controller.foo`, `$root.bar`, `val.prop`)
- **Call expression** — invokes a method/function: `$controller.method(args...)`
- **Operator expression** — combines subexpressions with unary or binary operators

---

## Operator Precedence

Operators bind from highest (70) to lowest (5). Operators at the same level are left-associative.

| Precedence | Operator(s) | Description | Notes |
|------------|-------------|-------------|-------|
| 70 | `!` `+` `-` (prefix) | Unary logical NOT, plus, negation | Right-associative |
| 50 | `*` `/` `%` | Multiplication, Division, Remainder | |
| 40 | `+` `-` | Addition, Subtraction | `+` is numeric only — no string concat |
| 30 | `<` `>` `<=` `>=` | Relational comparison | |
| 20 | `==` `!=` | Equality, Inequality | |
| 15 | `&&` | Logical AND | |
| 10 | `\|\|` | Logical OR | |
| 5  | `? :` | Conditional (ternary) | Right-associative |

Use parentheses `( )` to override precedence:

```guml
Panel {
    value_a: 1 + 2 * 3,    // 7 (multiplication first)
    value_b: (1 + 2) * 3,  // 9 (addition first)
    show: a || b && c       // a || (b && c)
}
```

---

## Unary Operators

| Operator | Operand | Description | Example |
|----------|---------|-------------|---------|
| `!` | `boolean` | Logical NOT | `!$controller.is_hidden` |
| `+` | `int`, `float` | Unary plus (no-op) | `+42` |
| `-` | `int`, `float` | Negation | `-$controller.offset` |

> There are no negative number literals in GUML. Write `-10` not as a literal but as unary minus applied to positive `10`.

---

## Binary Operators

### Arithmetic

```guml
Panel {
    width: $controller.base_width * 2,
    gap: ($controller.total - $controller.used) / 3,
    remainder: $controller.count % 5
}
```

> `+` is **only valid for numeric types**. Use template strings `$"..."` to compose strings.

### Comparison

```guml
Label {
    visible: $controller.score > 0,
    text: $controller.level >= 10 ? "Max" : $"Level {$controller.level}"
}
```

### Logical

```guml
Button {
    disabled: !$controller.can_proceed || $controller.is_loading,
    visible: $controller.is_admin && $controller.in_edit_mode
}
```

### Combining

```guml
ProgressBar {
    // Compute normalized 0..1 as percentage using ternary guard
    value := $controller.max > 0 ? $controller.current / $controller.max * 100 : 0
}
```

---

## Conditional (Ternary) Expression

```
condition ? true_expr : false_expr
```

Right-associative. The condition must be `boolean`; branches can be any type:

```guml
Label {
    text: $controller.is_vip ? "VIP" : "User",
    modulate := $controller.health > 50
                ? color(0.0, 1.0, 0.0, 1.0)
                : color(1.0, 0.2, 0.2, 1.0)
}

Button {
    disabled := !$controller.can_submit || $controller.is_loading ? true : false
}
```

Chained ternary (right-associative):

```guml
Label {
    text := $controller.score >= 90 ? "A"
          : $controller.score >= 80 ? "B"
          : $controller.score >= 70 ? "C"
          : "F"
}
```

---

## Reference Expressions

References navigate to properties using `.` member access.

### Global References

| Reference | Description |
|-----------|-------------|
| `$controller` | The host controller instance |
| `$root` | The root node / parameter scope (inside imported components) |
| `$item` | Loop context inside `each` blocks |

### Controller Property Access

```guml
Label { text := $controller.user_name }
Button { disabled := $controller.is_busy }
```

`snake_case` identifiers after `$controller.` are automatically converted to `PascalCase` in generated C# code: `$controller.user_name` → `controller.UserName`.

### Nested Member Access

```guml
Label { text := $controller.current_user.display_name }
Label { text := $controller.config.max_retries }
```

### Loop Variable Access

Inside `each` blocks, the declared loop variable is a reference:

```guml
each $controller.users { |idx, user|
    Label { text := $"{idx + 1}. {user.name}" }
    Label { text := user.is_active ? "Active" : "Offline" }
}
```

### `$item` Scope

`$item.index`, `$item.value`, `$item.root` are available inside `each` bodies when you need the current row context:

```guml
each $controller.items { ||
    Label { text := $"Row {$item.index}: {$item.value}" }
}
```

### `$root` in Reusable Components

Inside an imported component, `$root.param_name` accesses the `param` values passed by the parent:

```guml
// Inside card.guml
Label { text := $root.title }
Button { visible: $root.show_footer }
```

---

## Template Strings

Template strings embed expressions inside `$"..."`:

```
$"text {expr} more text"
```

- Use `{{` and `}}` for literal braces.
- Any expression is valid inside `{ }`.

```guml
Label { text := $"Hello, {$controller.user_name}!" }
Label { text := $"Score: {$controller.score}/{$controller.max_score}" }
Label { text := $"Load: {$controller.progress * 100}%" }
Label { text := $"{{escaped braces}}" }   // "{{escaped braces}}"
```

Template strings used with `:=` are reactive: the string re-evaluates when any referenced controller property fires `PropertyChanged`.

---

## Call Expressions

Signal handlers can be specified as method references or call expressions with arguments. Call expressions pass static values to the handler:

```
handler: $controller.method(arg1, arg2, ...)
```

```guml
each $controller.items { |idx, item|
    Button {
        text: "Delete",
        #pressed: $controller.delete_item(item.id)
    }
}

Button {
    #pressed: $controller.set_mode("edit")
}
```

---

## Object Creation (`new`)

Creates an instance with property initializers:

```
new TypeName { prop: expr, prop2: expr2 }
```

```guml
Label {
    label_settings: new LabelSettings {
        font_size: 24,
        font_color: color(1.0, 0.0, 0.0, 1.0),
        outline_size: 2
    }
}
```

---

## Struct Constructors

Vector, color, and StyleBox types use positional or named form:

```guml
// Positional
Control { position: vec2(100, 200) }
ColorRect { color: color(1.0, 0.0, 0.0, 1.0) }

// Named (partial)
Control { custom_minimum_size: vec2({ x: 200 }) }
ColorRect { color: color({ r: 1.0, a: 0.5 }) }
```

See [Types Reference](types.md) for all struct constructor types.

---

## Parenthesized Expressions

Parentheses override precedence:

```guml
Panel {
    result: (a + b) * c,
    cond: (x > 0 && y > 0) || z == 0
}
```

---

## Enum Values

Enum values use `.PascalCase` (preceded by nothing or whitespace, never another identifier character):

```guml
Label { horizontal_alignment: .Center }
Control { size_flags_horizontal: .ExpandFill }
```

Distinguishing from member access:
- `.Center` — enum value (`.` is the first character of the token)
- `$controller.center` — member access (`.` follows an identifier character)

---

## Property Assignment Operators

These are not expressions but appear in property assignments:

| Operator | Semantics |
|----------|-----------|
| `:` | One-time static assignment at build time |
| `:=` | Reactive binding: controller → UI (one-way) |
| `=:` | Reactive binding: UI → controller (one-way, reversed) |
| `<=>` | Two-way binding (both directions) |

---

## String Escapes

Standard C-style escape sequences inside `"..."` and `$"..."`:

| Sequence | Character |
|----------|-----------|
| `\\` | Backslash `\` |
| `\"` | Double quote `"` |
| `\n` | Newline |
| `\t` | Horizontal tab |
| `\r` | Carriage return |

---

## Related

- [Types Reference](types.md) — all value types and struct constructors.
- [Data Binding Guide](../guide/data_binding.md) — `:=`, `=:`, `<=>` semantics.
- [List Rendering Guide](../guide/list_rendering.md) — `each` blocks and `$item` context.
