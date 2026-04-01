# §3 Lexical Structure

## §3.1 Documents

A GUML *document* consists of a single source file, typically with the `.guml` extension. A source file is an ordered sequence of Unicode characters. Each document defines exactly one root component hierarchy.

Conceptually, a document is processed using the following steps:

1. **Lexical analysis** (§3.3): Translates a stream of Unicode input characters into a stream of tokens.
2. **Syntactic analysis**: Translates the stream of tokens into an abstract syntax tree (`GumlDoc`).

## §3.2 Grammars

This specification presents the syntax of the GUML language using two grammars. The *lexical grammar* (§3.3) defines how characters are combined into tokens. The *syntactic grammar* (§7, §8) defines how tokens are combined into GUML documents.

### §3.2.1 Grammar Notation

Throughout this specification, syntactic and lexical grammars are presented using the following BNF-like notation:

- Nonterminals are shown in *italic* type.
- Terminals are shown in `fixed-width` type.
- Alternatives are separated by `|`.
- Optional elements are enclosed in `[` and `]`.
- Repetition (zero or more) is indicated by `{ }` or `*`.
- Grouping is indicated by `( )`.

## §3.3 Lexical Analysis

The lexical analysis phase converts a source text into a sequence of tokens. White space and comments are discarded and do not appear in the token stream.

The following token kinds are defined:

- *identifier* tokens
- *keyword* tokens
- *literal* tokens (string, integer, float, boolean, null)
- *operator* tokens
- *punctuator* tokens

When multiple lexical grammar rules match a prefix of the input, the longest match is used. Whitespace and newlines are consumed but do not produce tokens.

## §3.4 Comments

GUML supports single-line comments and documentation comments.

```antlr
comment
    : '//' input_character*
    ;

documentation_comment
    : documentation_comment_line+
    ;

documentation_comment_line
    : '///' input_character*
    ;

input_character
    : '<Any Unicode character except newline characters>'
    ;
```

Single-line comments start with the characters `//` and extend to the end of the source line.

Documentation comments start with `///` and extend to the end of the source line. Consecutive `///` lines form a **documentation block** that is associated with the immediately following declaration (component, parameter, or event). Documentation comments are preserved as syntax metadata for tooling, code generation, and LSP integration.

### §3.4.1 Documentation Tags

Within a documentation block, lines beginning with `/// @` (after optional whitespace) are **tag lines**. Tag lines provide structured metadata and are excluded from the description text. Non-tag lines constitute the **description text** of the documentation block.

| Tag | Applicable Target | Syntax | Description |
|-----|-------------------|--------|-------------|
| `@name` | Component declaration | `/// @name identifier` | Specifies a node naming marker for code generation (§11.2). |
| `@param` | Event declaration | `/// @param arg_name description` | Documents an event argument. |

- `@name` shall only appear in documentation blocks attached to component declarations. The identifier shall follow `snake_case` naming.
- `@param` shall only appear in documentation blocks attached to event declarations. The `arg_name` should match a declared event argument name.

> *Example:*
> ```guml
> // This is a comment
> /// A labeled greeting component.
> /// @name greeting_label
> Label {
>     text: "Hello" // This is also a comment
> }
> ```
> *end example*

> *Example:* Multi-line documentation with event parameter docs:
> ```guml
> /// Emitted when the selection changes.
> /// @param index The zero-based index of the selected item.
> /// @param value The value of the selected item.
> event on_selection_changed(int index, string value)
> ```
> *end example*

## §3.5 White Space

White space is defined as the horizontal tab character (U+0009), the space character (U+0020), the carriage return character (U+000D), and the line feed character (U+000A).

```antlr
whitespace
    : ' '
    | '\t'
    ;

new_line
    : '\r'
    | '\n'
    | '\r\n'
    ;
```

White space and new lines may occur between any two tokens. They serve only to separate tokens and have no other syntactic significance.

## §3.6 Tokens

There are several kinds of tokens: identifiers, keywords, literals, operators, and punctuators.

### §3.6.1 Identifiers

An *identifier* is a sequence of characters that names an entity in a GUML document. GUML distinguishes several categories of identifiers by their leading character.

```antlr
identifier
    : letter_or_underscore identifier_part*
    ;

identifier_part
    : letter_or_underscore
    | decimal_digit
    ;

letter_or_underscore
    : unicode_letter
    | '_'
    ;

component_name
    : uppercase_letter identifier_part*
    ;

global_ref
    : '$' identifier_part+
    ;

alias_ref
    : '@' identifier_part+
    ;

event_ref
    : '#' identifier_part+
    ;

enum_value
    : '.' uppercase_letter identifier_part*
    ;
```

Identifier categories are determined by their first character:

- **Component names** begin with an uppercase letter (e.g., `Label`, `VBoxContainer`). A token starting with an uppercase letter is always classified as `component_name`.
- **Property names** begin with a lowercase letter or underscore (e.g., `text`, `size_flags_horizontal`).
- **Global references** begin with `$` (e.g., `$controller`, `$root`). See §9.3 for predefined global references.
- **Alias references** begin with `@` (e.g., `@submit_btn`).
- **Event references** begin with `#` (e.g., `#pressed`, `#button_down`). Used to subscribe to events (§8.4).
- **Enum member values** begin with `.` followed by an uppercase letter (e.g., `.Center`, `.Fill`). An enum value token is only recognized when the `.` is NOT immediately preceded by an identifier character (letter, digit, or `_`), to distinguish from member access (§6.5).

#### §3.6.1.1 Naming Conventions

GUML source code follows a uniform naming convention:

| Category | Case | Examples |
|----------|------|----------|
| Component names, type names | PascalCase | `Label`, `VBoxContainer`, `LabelSettings` |
| Enum member values | `.PascalCase` | `.Center`, `.Fill`, `.Left` |
| All other identifiers | snake_case | `text`, `size_flags_horizontal`, `on_submit` |

Specifically:

- **Godot node properties**: `snake_case` — matching Godot's own naming (e.g., `text`, `custom_minimum_size`).
- **Controller member access**: `snake_case` in GUML — the Source Generator converts to C# PascalCase at code generation time (e.g., `$controller.user_name` → C# `UserName`). See §11.2.3.4.
- **Parameter names** (§8.6): `snake_case` (e.g., `param string button_text`).
- **Event names** (§8.7): `snake_case` (e.g., `event on_submit`).
- **Alias references**: `snake_case` (e.g., `@submit_btn`, `@name_input`).
- **Event references**: `snake_case` — matching Godot signal names (e.g., `#pressed`, `#text_changed`).
- **Enum member values**: PascalCase — referencing C# enum members directly (e.g., `.Center`, `.Fill`).

Member access (`.identifier`) after a reference always uses `identifier` (lowercase-starting) tokens. A `.` followed by an uppercase letter that is NOT preceded by an identifier character is an enum value token, not member access.

### §3.6.2 Keywords

The following identifiers are reserved as keywords and shall not be used as property names:

```antlr
keyword
    : 'as'
    | 'audio'
    | 'each'
    | 'event'
    | 'font'
    | 'image'
    | 'import'
    | 'new'
    | 'ntr'
    | 'param'
    | 'tr'
    | 'video'
    ;
```

### §3.6.3 Literals

A *literal* is a source code representation of a value.

```antlr
literal
    : boolean_literal
    | integer_literal
    | real_literal
    | string_literal
    | template_string_literal
    | null_literal
    ;
```

#### §3.6.3.1 Boolean Literals

There are two boolean literal values: `true` and `false`.

```antlr
boolean_literal
    : 'true'
    | 'false'
    ;
```

The type of a *boolean_literal* is `boolean`.

#### §3.6.3.2 Integer Literals

Integer literals are written as a sequence of decimal digits.

```antlr
integer_literal
    : decimal_digit+
    ;

decimal_digit
    : '0'..'9'
    ;
```

The type of an *integer_literal* is `int`.

> *Example:* `0`, `42`, `100` *end example*

> *Note:* GUML does not define negative number literals. Negative values are expressed using the unary negation operator `-` applied to a positive literal (e.g., `-100`). See §6.2.1. *end note*

#### §3.6.3.3 Real Literals

Real literals represent floating-point values. They consist of a whole-number part, a decimal point, and a fractional part. Scientific notation is not supported.

```antlr
real_literal
    : decimal_digit+ '.' decimal_digit+
    ;
```

The type of a *real_literal* is `float`.

> *Example:* `3.14`, `0.5`, `1.0` *end example*

#### §3.6.3.4 String Literals

String literals are enclosed in double quotation marks (`"`).

```antlr
string_literal
    : '"' string_character* '"'
    ;

string_character
    : '<Any character except ", \\ and newline>'
    | escape_sequence
    ;

escape_sequence
    : '\\' escape_char
    ;

escape_char
    : 'n'     // newline (U+000A)
    | 't'     // horizontal tab (U+0009)
    | 'r'     // carriage return (U+000D)
    | '\\'  // backslash
    | '"'    // double quote
    ;
```

The type of a *string_literal* is `string`.

> *Example:* `"Hello World"`, `"res://icon.png"`, `"Line1\nLine2"` *end example*

#### §3.6.3.5 Template String Literals

Template string literals support expression interpolation. They are prefixed with `$` and enclosed in double quotation marks. Expressions are embedded within `{` and `}` delimiters.

```antlr
template_string_literal
    : '$"' template_string_part* '"'
    ;

template_string_part
    : template_text_segment
    | template_interpolation
    ;

template_text_segment
    : '<Any character sequence not containing {, }, ", \\ or newline>'
    | escape_sequence
    | '{{'
    | '}}'
    ;

template_interpolation
    : '{' expression '}'
    ;
```

Within a template string, `{{` produces a literal `{` and `}}` produces a literal `}`.

The type of a *template_string_literal* is `string`.

When a template string is used in a binding context (`:=`), all embedded reference expressions become binding sources. A change to any source triggers re-evaluation of the entire template string.

> *Example:*
> ```guml
> Label {
>     text: $"Hello, {$controller.user_name}!",
>     tooltip := $"Score: {$controller.score} / {$controller.max_score}"
> }
> ```
> *end example*

> *Note:* Template strings are designed to facilitate i18n — the entire template can serve as a localization key for future replacement. *end note*

#### §3.6.3.6 The Null Literal

```antlr
null_literal
    : 'null'
    ;
```

The null literal is written as `null` (all lowercase). It represents a null reference.

> *Note:* The null literal spelling is consistent with C#: `null`. *end note*

### §3.6.4 Operators and Punctuators

There are several kinds of operators and punctuators. Operators are used in expressions (§6) to describe operations involving one or more operands. Punctuators are used for grouping and separating.

```antlr
operator
    : '!='  | '<='  | '>='  | '=='
    | '<'   | '>'
    | '!'   | '||'  | '&&'
    | '+'   | '-'   | '*'   | '/'  | '%'
    | '?'
    ;

punctuator
    : ','   | '.'   | '|'
    | '<=>' | ':=' | '=:' | ':' | '=>'
    | '('   | ')'
    | '['   | ']'
    | '{'   | '}'
    ;
```

The following operators and punctuators have special meaning:

| Symbol | Description |
|--------|-------------|
| `:` | Static property assignment (§8.2) |
| `=>` | Template projection assignment and projected each form (§8.8) |
| `:=` | One-way mapping: data -> property (§8.3) |
| `=:` | One-way mapping: property -> data (§8.3) |
| `<=>` | Two-way mapping: property <-> data (§8.3) |
| `{` `}` | Component body delimiters (§8.1), dictionary literals (§5.1) |
| `(` `)` | Grouping, function-like constructor argument lists |
| `?` `:` | Conditional (ternary) expression (§6.2.4) |
| `\|` | Each loop variable delimiter (§8.8) |
| `,` | Value separator, property separator |
| `.` | Member access (§6.5) |
| `[` `]` | Array literal delimiters (§5.1.4), type parameters (§5.1.5) |
| `#` | Event reference prefix (§3.6.1) |
| `@` | Alias identifier prefix (§3.6.1) |
| `$` | Global reference identifier prefix (§3.6.1) |

> *Note:* The `#`, `@`, and `$` characters are part of the identifier syntax (§3.6.1) rather than standalone punctuators. *end note*


