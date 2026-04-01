# §5 Types

GUML supports a set of types designed to interoperate with the Godot engine's type system.

## §5.1 Value Types

### §5.1.1 Primitive Types

The following primitive types are supported as literal values in expressions (§6):

| GUML Type | Description | Corresponding C#/Godot Type |
|-----------|-------------|----------------------------|
| `string` | A sequence of characters. | `System.String` |
| `int` | A 32-bit signed integer. | `System.Int32` |
| `float` | A 32-bit floating-point number. | `System.Single` |
| `boolean` | A truth value (`true` or `false`). | `System.Boolean` |

### §5.1.2 Enum Types

Enumeration values are represented by member names prefixed with a dot (`.`), as defined in §3.6.1.

```antlr
enum_value
    : '.' uppercase_letter identifier_part*
    ;
```

The concrete enum type is inferred from the context of the property being assigned. The enum member name is resolved in generated code against the property's declared type.

> *Example:*
> ```guml
> Label {
>     horizontal_alignment: .Center,
>     vertical_alignment: .Fill
> }
> ```
> Here `.Center` is resolved to `HorizontalAlignment.Center` and `.Fill` is resolved to `VerticalAlignment.Fill` based on the property types of the `Label` node.
> *end example*

> *Note:* An enum value token `.Name` is only recognized when the `.` is NOT immediately preceded by an identifier character. This rule ensures that member access expressions such as `$controller.center` (which is preceded by `r`) are not misinterpreted as enum values. *end note*

### §5.1.3 Object Literal Type

An object literal is an untyped, unordered collection of key-value pairs, enclosed in braces. Keys are identifiers (property names), and values are expressions (§6).

```antlr
object_literal
    : '{' property_assignment_list? '}'
    ;

property_assignment_list
    : property_assignment (',' property_assignment)*
    ;
```

Object literals do not enforce a specific type. They are used for:

- Struct constructor named initializers (§5.2.1).
- `each` block configuration parameters (§8.8).
- Passing complex parameter values.

> *Example:*
> ```guml
> Panel {
>     metadata: { key1: 10, key2: "value" }
> }
> ```
> *end example*

### §5.1.4 Array Types

An array literal represents a typed, ordered collection of values. The element type must be specified.

```antlr
array_literal
    : type_name '[' argument_list? ']'
    ;
```

All elements must be compatible with the declared element type (see §6.6 for implicit conversion rules).

| GUML Syntax | Corresponding C# Type |
|-------------|----------------------|
| `int[...]` | `int[]` or `Godot.Collections.Array<int>` |
| `string[...]` | `string[]` or `Godot.Collections.Array<string>` |
| `Texture2D[...]` | `Texture2D[]` or `Godot.Collections.Array<Texture2D>` |

The concrete C# collection type is determined by the implementation based on the target property type.

> *Example:*
> ```guml
> Panel {
>     scores: int[100, 95, 87, 92],
>     names: string["Alice", "Bob", "Charlie"],
>     empty_list: int[]
> }
> ```
> *end example*

### §5.1.5 Typed Dictionary Type

A typed dictionary represents a strongly-typed collection of key-value pairs. Both key and value types must be specified.

```antlr
typed_dictionary_literal
    : component_name '[' type_name ',' type_name ']' '{' typed_dictionary_entry_list? '}'
    ;

typed_dictionary_entry_list
    : typed_dictionary_entry (',' typed_dictionary_entry)*
    ;

typed_dictionary_entry
    : expression ':' expression
    ;
```

The leading `component_name` shall resolve to a dictionary type (e.g., `Dictionary`). The two `type_name` tokens in brackets specify the key type and value type respectively.

Unlike object literals (§5.1.3) where keys are identifiers, typed dictionary keys are expressions.

> *Example:*
> ```guml
> Panel {
>     scores: Dictionary[string, int]{ "Alice": 100, "Bob": 95 },
>     mapping: Dictionary[int, string]{ 1: "one", 2: "two" }
> }
> ```
> *end example*

## §5.2 Struct Mapping Types

GUML uses a unified constructor syntax to map GUML expressions to C# `struct`/`object` values. All such constructor expressions are represented as `GumlStructNode` in the AST.

### §5.2.1 Unified Struct Constructor Syntax

```antlr
struct_expression
    : struct_type_name '(' struct_initializer ')'
    ;

struct_type_name
    : identifier
    | component_name
    ;

struct_initializer
    : argument_list
    | named_initializer
    ;

argument_list
    : expression (',' expression)*
    ;

named_initializer
    : object_literal
    ;
```

- `type(args...)`: positional form, typically mapped to constructor parameters.
- `type({ key: value, ... })`: named mapping form, typically mapped by member/property name.

Both forms shall produce a `GumlStructNode`.

### §5.2.2 Type Resolution and Mapping

For each `GumlStructNode`, an implementation shall resolve `struct_type_name` to a target type using available API metadata.

- `struct_type_name` shall not be a reserved keyword token (§3.6.2).
- If the resolved target is a C# `struct`, it shall be treated as struct mapping.
- If the resolved target is a C# reference type (`object`/class), it shall be treated as object mapping.
- If the type cannot be resolved, the document is ill-formed and an implementation shall produce a diagnostic.

The positional form `type(args...)` and named form `type({ key: value, ... })` use the same resolution pipeline and differ only in initializer binding strategy.

### §5.2.3 Examples

> *Example:* Positional initialization:
> ```guml
> Control {
>     position: vec2(100, 200),
>     modulate: color(1.0, 0.5, 0.0, 1.0)
> }
> ```
> *end example*

> *Example:* Named dictionary-style initialization:
> ```guml
> Panel {
>     custom_minimum_size: vec2({ x: 490 + idx * 35, y: 100 }),
>     theme_override_styles_panel: style_box_flat({
>         bg_color: color({ r: 0.1, g: 0.1, b: 0.1, a: 1.0 }),
>         corner_radius_top_left: 5
>     })
> }
> ```
> *end example*

> *Note:* Names such as `vec2`, `color`, and `style_box_flat` are not special-cased in this section. They are resolved through `GumlStructNode` type lookup like any other `struct_type_name`. *end note*

## §5.3 Resource Types

GUML provides built-in functions for loading Godot resources by path.

```antlr
resource_expression
    : resource_keyword '(' expression ')'
    ;

resource_keyword
    : 'image' | 'font' | 'audio' | 'video'
    ;
```

| GUML Syntax | Description | Godot Type |
|-------------|-------------|------------|
| `image(path)` | Loads an image/texture resource. | `Texture2D` |
| `font(path)` | Loads a font resource. | `Font` |
| `audio(path)` | Loads an audio stream resource. | `AudioStream` |
| `video(path)` | Loads a video stream resource. | `VideoStream` |

The *path* argument is typically a string literal using Godot's resource path format.

> *Example:*
> ```guml
> TextureRect {
>     texture: image("res://icon.png")
> }
> ```
> *end example*

## §5.4 The Null Type

The null literal `null` (§3.6.3.6) represents the absence of a value. It is compatible with any reference-type property.

> *Note:* The null literal is spelled `null` in lowercase. *end note*

## §5.5 Type Conversion Rules

GUML supports a limited set of implicit type conversions. There is no explicit cast syntax.

### §5.5.1 Implicit Conversions

| Source Type | Target Type | Rule |
|------------|-------------|------|
| `int` | `float` | Widening conversion. Always permitted. |
| `int`, `float` | `string` | Only within template string interpolation `$"{expr}"` (§3.6.3.5). Not permitted in other contexts. |
| `null` | Any reference type | Permitted. |

### §5.5.2 Mixed-type Arithmetic

When arithmetic operators (`+`, `-`, `*`, `/`, `%`) are applied to operands of mixed numeric types:

- If either operand is `float`, the `int` operand is implicitly promoted to `float`.
- The result type is `float`.

> *Example:*
> ```guml
> Panel {
>     // int + float -> float
>     value: 10 + 3.14
> }
> ```
> *end example*

### §5.5.3 Comparison Type Promotion

When relational operators (`<`, `>`, `<=`, `>=`, `==`, `!=`) compare operands of mixed numeric types, the `int` operand is promoted to `float` before comparison.

### §5.5.4 Disallowed Conversions

The following conversions are not permitted and shall produce a diagnostic:

- `string` → numeric types (`int`, `float`).
- `boolean` → `int` or `float`.
- `int` or `float` → `boolean`.
- Any explicit cast syntax (GUML does not provide a cast operator).

> *Note:* For string formatting, use template strings `$"{value}"` instead of concatenation. The `+` operator is restricted to numeric types only (§6.2.2); violation produces diagnostic `GUML2009`. *end note*

## §5.6 Generic Type Names

Type names used in parameter declarations (§8.6) and event argument declarations (§8.7) may include generic type arguments, enabling strongly-typed collections and other parameterized types.

```antlr
type_name
    : simple_type_name
    | simple_type_name '<' type_argument_list '>'
    ;

simple_type_name
    : identifier
    | component_name
    ;

type_argument_list
    : type_name (',' type_name)*
    ;
```

Generic type names nest arbitrarily — each type argument in `type_argument_list` is itself a `type_name` and may include its own generic arguments.

The parser consumes the full generic type (including nested `<` `>` pairs and commas) into a single synthetic token. The resulting token's `Text` property contains the complete type string (e.g., `"Dictionary<String, List<int>>"`).

> *Example:* Simple generic type:
> ```guml
> VBoxContainer {
>     param List<int> scores
> }
> ```
> *end example*

> *Example:* Nested generic type:
> ```guml
> Panel {
>     param Dictionary<String, List<InventoryItem>> grouped_items
> }
> ```
> *end example*

### §5.6.1 Type Resolution

Generic type names are resolved by the implementation against the host platform's type system (e.g., .NET CLR types). The `simple_type_name` portion identifies the open generic type, and each `type_name` in the `type_argument_list` is resolved as a type argument.

- `List<int>` resolves to `System.Collections.Generic.List<System.Int32>`.
- `Dictionary<String, List<int>>` resolves to `System.Collections.Generic.Dictionary<System.String, System.Collections.Generic.List<System.Int32>>`.

If a generic type or any of its type arguments cannot be resolved, the document is ill-formed and an implementation shall produce a diagnostic.

> *Note:* Generic type syntax is only valid in `type_name` contexts (parameter declarations and event argument types). It is not valid in property assignment values, struct constructors, or other expression positions. *end note*

