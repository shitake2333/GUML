# §6 Expressions

An *expression* is a sequence of operators and operands that computes a value.

```antlr
expression
    : conditional_expression
    ;
```

## §6.1 Expression Classifications

Every expression is classified as one of the following:

- **Value expression**: Evaluates to a concrete value (literal, struct, resource, array, or object creation).
- **Reference expression**: Evaluates to a reference chain targeting a property on a controller or node.
- **Call expression**: Invokes a function/method and produces a return value.
- **Operator expression**: Combines one or more sub-expressions with an operator.

## §6.2 Operators

### §6.2.1 Prefix (Unary) Operators

Prefix operators appear before their operand.

```antlr
prefix_expression
    : prefix_operator unary_expression
    ;

prefix_operator
    : '!'
    | '+'
    | '-'
    ;
```

All prefix operators have a precedence of **70**.

| Operator | Operand Type | Description |
|----------|-------------|-------------|
| `!` | `boolean` | Logical negation |
| `+` | `int`, `float` | Unary plus (no-op) |
| `-` | `int`, `float` | Unary negation |

> *Example:*
> ```guml
> Panel {
>     visible: !$controller.is_hidden,
>     offset_x: -100,
>     offset_y: -$controller.margin
> }
> ```
> *end example*

> *Note:* Negative number values are expressed via the unary `-` operator applied to a positive literal. There are no negative number literals in GUML. *end note*

### §6.2.2 Infix (Binary) Operators

Infix operators appear between two operands.

```antlr
infix_expression
    : expression infix_operator expression
    ;
```

The following table lists all infix operators, ordered by precedence from lowest to highest. Operators at the same precedence level are evaluated left-to-right (left-associative).

| Precedence | Operators | Description | Operand Types |
|------------|-----------|-------------|---------------|
| 10 | `\|\|` | Logical OR | `boolean` |
| 15 | `&&` | Logical AND | `boolean` |
| 20 | `==` `!=` | Equality, Inequality | any comparable |
| 30 | `<` `>` `<=` `>=` | Relational comparison | `int`, `float` |
| 40 | `+` `-` | Addition, Subtraction | `int`, `float` |
| 50 | `*` `/` `%` | Multiplication, Division, Remainder | `int`, `float` |

> *Note:* The `+` operator is restricted to numeric operands (`int` and `float`). String concatenation via `+` is not supported; applying `+` to non-numeric operands is ill-formed and shall produce a diagnostic (`GUML2009`). Use template strings `$"..."` (§3.6.3.5) for string composition. *end note*

> *Note:* For mixed-type numeric operations, see §5.5 Type Conversion Rules. *end note*

### §6.2.3 Operator Precedence and Parenthesization

When an expression contains multiple operators, precedence determines the grouping of operands. Higher precedence binds tighter.

Parentheses `(` `)` may be used to override the default precedence. A parenthesized sub-expression is marked with `FirstPrecedence = true` internally, ensuring it evaluates before surrounding operators.

> *Example:*
> ```guml
> Panel {
>     // Parsed as: 1 + (2 * 3) = 7
>     value_a: 1 + 2 * 3,
>     // Parsed as: (1 + 2) * 3 = 9
>     value_b: (1 + 2) * 3,
>     // Logical: && binds tighter than ||
>     show: $controller.flag_a || $controller.flag_b && $controller.flag_c
> }
> ```
> *end example*

### §6.2.4 Conditional (Ternary) Expression

The conditional expression uses the `?` and `:` operators to select between two values based on a boolean condition.

```antlr
conditional_expression
    : logical_or_expression '?' expression ':' expression
    | logical_or_expression
    ;
```

The conditional expression has the **lowest** precedence among all expression operators (precedence 5). It is **right-associative**: `a ? b : c ? d : e` is parsed as `a ? b : (c ? d : e)`.

The condition (left of `?`) shall evaluate to `boolean`. The two branches (between `?` and `:`, and after `:`) may be any expression. If the branches have different types, the result type follows the implicit conversion rules (§5.5).

> *Example:*
> ```guml
> Label {
>     text: $controller.is_vip ? "VIP User" : "Normal User",
>     modulate := $controller.health > 50 ? color(0, 1, 0, 1) : color(1, 0, 0, 1)
> }
> ```
> *end example*

### §6.2.5 Assignment Operators

Assignment operators are not used within expressions. They appear only in property assignments (§8.2) and property mapping/binding (§8.3).

| Operator | Context | Description |
|----------|---------|-------------|
| `:` | §8.2 | Static mapping. The expression is evaluated once. |
| `:=` | §8.3 | One-way mapping from data to component property. |
| `=:` | §8.3 | One-way mapping from component property to data. |
| `<=>` | §8.3 | Two-way mapping between component property and data. |

## §6.3 Primary Expressions

A primary expression is a literal (§3.6.3), a reference expression (§6.5), a struct constructor (§5.2), a resource function (§5.3), an object creation expression (§6.4), an array literal (§5.1.4), a typed dictionary literal (§5.1.5), a call expression (§6.6), or a translation expression (§6.7).

```antlr
primary_expression
    : literal
    | reference_expression
    | call_expression
    | struct_expression
    | resource_expression
    | new_expression
    | array_literal
    | typed_dictionary_literal
    | translate_expression
    | ntr_expression
    | enum_value
    | '(' expression ')'
    ;
```

For `struct_expression`, implementations shall construct a `GumlStructNode` and resolve the target type according to §5.2.2.

`vec2(...)`, `color(...)`, `style_box_flat(...)`, and similar forms are parsed through `struct_expression` (not through dedicated per-type expression rules).

### §6.3.1 Value Separators

Within property assignment lists and argument lists, values are separated by `,` (comma).

> *Note:* The `|` (pipe) character is used exclusively as a variable delimiter in `each` block headers (§8.8). It is not a general-purpose value separator. *end note*

## §6.4 Object Creation Expressions

The `new` keyword creates an instance of a type with property initializers.

```antlr
new_expression
    : 'new' component_name '{' property_assignment_list? '}'
    ;
```

The *component_name* shall be a type name starting with an uppercase letter. The body is a dictionary-like structure of property assignments.

> *Example:*
> ```guml
> Label {
>     label_settings: new LabelSettings {
>         font_size: 24,
>         font_color: color(1.0, 0.0, 0.0, 1.0)
>     }
> }
> ```
> *end example*

## §6.5 Member Access

Member access uses the `.` punctuator to access properties on a reference.

```antlr
reference_expression
    : global_ref ('.' identifier)*
    | alias_ref ('.' identifier)*
    | identifier ('.' identifier)*
    ;
```

A reference expression forms a chain. Each `.identifier` adds a `PropertyRef` node to the chain.

> *Example:*
> ```guml
> Label {
>     // Chain: $controller -> .data -> .name
>     text := $controller.data.name
> }
> ```
> The reference resolves as: start from `$controller` (global ref), access `data` (resolved to C# `Data`), then access `name` (resolved to C# `Name`). See §3.6.1.1 and §11.2.3.4 for the snake_case to PascalCase conversion rule.
> *end example*

## §6.6 Call Expressions

A call expression invokes a method on a reference and produces a return value.

```antlr
call_expression
    : reference_expression '(' argument_list? ')'
    ;

argument_list
    : expression (',' expression)*
    ;
```

The target of a call expression shall be a reference expression (§6.5) pointing to a callable member — typically a method on the controller or a global reference.

Call expressions are themselves expressions and may appear anywhere an expression is expected, including as arguments to other calls.

**Constraints:**

- Lambda/anonymous functions are not supported.
- The callable member must be accessible from the GUML document's scope.
- Method overload resolution follows C# rules and is performed at code generation time.

> *Example:*
> ```guml
> Label {
>     text := $controller.format_name($controller.first_name, $controller.last_name),
>     visible := $controller.should_show(idx)
> }
> ```
> *end example*

> *Example:* Nested call:
> ```guml
> Label {
>     text := $controller.translate($controller.get_key())
> }
> ```
> *end example*

> *Note:* When a call expression is used in a binding context (`:=`), the binding is re-evaluated whenever any of the argument reference expressions change. The call itself is re-invoked on each update. *end note*

## §6.7 Translation Expressions

Translation expressions provide built-in internationalization (i18n) support following the gettext convention. Two forms are defined: `tr()` for singular translation and `ntr()` for plural-aware translation.

```antlr
translate_expression
    : 'tr' '(' string_literal ')'
    | 'tr' '(' string_literal ',' translate_options ')'
    ;

ntr_expression
    : 'ntr' '(' string_literal ',' string_literal ',' expression ')'
    | 'ntr' '(' string_literal ',' string_literal ',' expression ',' translate_options ')'
    ;

translate_options
    : '{' translate_option_list? '}'
    ;

translate_option_list
    : translate_option (',' translate_option)* ','?
    ;

translate_option
    : 'context' ':' string_literal
    | identifier ':' expression
    ;
```

### §6.7.1 `tr()` — Singular Translation

The `tr()` expression translates a message identifier (msgid) using the current locale. The first argument shall be a string literal representing the source-language text (the msgid in gettext terms).

An optional second argument is a *translate_options* object literal containing:

- **`context`** (reserved key): A string literal providing disambiguation context (gettext `msgctxt` / `pgettext`). Used when the same msgid has different meanings in different contexts.
- **Other keys**: Named placeholder arguments. Each key corresponds to a `{name}` placeholder in the translated string, and the value is an arbitrary expression.

> *Example:*
> ```guml
> // Simple translation
> Label { text: tr("Start Game") }
>
> // Translation with named placeholders
> Label { text := tr("Hello, {name}!", { name: $controller.user_name }) }
>
> // Translation with disambiguation context
> Label { text: tr("File", { context: "noun" }) }
>
> // Context and placeholders combined
> Label { text := tr("Hello, {name}!", { context: "formal", name: $controller.user_name }) }
> ```
> *end example*

### §6.7.2 `ntr()` — Plural Translation

The `ntr()` expression selects between singular and plural forms based on a count value, corresponding to gettext `ngettext` / `npgettext`.

Arguments (positional):

1. *string_literal* — singular msgid.
2. *string_literal* — plural msgid.
3. *expression* — the count expression that drives singular/plural selection.
4. *translate_options* (optional) — same structure as `tr()`, supporting `context` and named placeholders.

> *Example:*
> ```guml
> // Basic singular/plural
> Label { text := ntr("One item", "{count} items", $controller.item_count) }
>
> // With named placeholders
> Label { text := ntr("One item", "{count} items", $controller.item_count,
>                     { count: $controller.item_count }) }
>
> // With disambiguation context (npgettext)
> Label { text := ntr("One apple", "{count} apples", $controller.n,
>                     { context: "fruit", count: $controller.n }) }
> ```
> *end example*

### §6.7.3 Semantics and Fallback

Translation expressions are resolved at runtime through `Guml.StringProvider` (§11.1):

- `tr(msgid, options?)` maps to `Guml.StringProvider?.Tr(msgid, context, args)`.
- `ntr(singular, plural, count, options?)` maps to `Guml.StringProvider?.Ntr(singular, plural, count, context, args)`.

When `Guml.StringProvider` is `null`, the fallback behavior is:

- `tr()` returns the msgid unchanged.
- `ntr()` returns the singular msgid when count equals 1, otherwise the plural msgid.

The `context` key is extracted from the options object and passed as the dedicated `context` parameter. All remaining keys are collected into a `Dictionary<string, object>` and passed as the `args` parameter.

### §6.7.4 Binding and Dependency Tracking

When a translation expression appears in a binding context (`:=`), the code generator shall automatically inject a dependency on the pseudo-property `"_locale"`. This ensures the bound property is re-evaluated when the application locale changes.

The `"_locale"` dependency is raised by `GuiController` in response to `Guml.StringProvider.PropertyChanged("CurrentLocale")` events. See §9 for the controller lifecycle details.

If the translate_options contain reference expressions (e.g., `{ name: $controller.user_name }`), those references are also tracked as binding dependencies. A translation expression with dynamic arguments therefore has **two dependency sources**:

| Dependency Source | Trigger |
|-------------------|---------|
| `Guml.StringProvider["CurrentLocale"]` | Locale switch — re-invokes translation |
| `_controller["PropertyName"]` | Controller property change — re-evaluates arguments |

### §6.7.5 Combination with Template Strings

Translation expressions are ordinary expressions and may appear in any expression context, including template string interpolation holes (`$"...{expr}..."`).

> *Example:*
> ```guml
> // Translation fragment embedded in template string
> Label { text := $"[{tr("Role")}] {$controller.user_name}" }
>
> // Template string as a placeholder value
> Label { text: tr("Score: {detail}", { detail: $"({$controller.score}/{$controller.max})" }) }
> ```
> *end example*

> *Note:* When entire sentences require translation, prefer `tr()` with named placeholders over template strings, so translators can control word order. Use template strings for UI-level assembly (prefixes, brackets, separators) that does not need translation. *end note*

