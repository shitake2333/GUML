# §11.2 Code Generation Specification

This extension specification defines naming and shape constraints for C# code generated from GUML documents.

This chapter is normative for Source Generator implementations and focuses on deterministic readable naming.

## §11.2.0 Normative Terms

The key words MUST, MUST NOT, SHOULD, SHOULD NOT, and MAY in this document indicate requirement levels.

- MUST / MUST NOT: mandatory for conformance.
- SHOULD / SHOULD NOT: strongly recommended unless justified otherwise.
- MAY: optional behavior.

## §11.2.1 Scope

This chapter defines code generation naming constraints for:

- Generated C# member names (fields, properties, local variables, helper methods).
- Scene-tree node names (`Node.Name`) for generated nodes.
- Collision handling and deterministic suffixing rules.

This chapter does not redefine GUML syntax. Core syntax remains in §3, §8, and §9.

## §11.2.2 Naming Goals

A conforming implementation MUST generate names that are:

- Deterministic: same GUML input and same configuration produce identical names.
- Readable: names preserve GUML source intent whenever possible.
- Stable: incremental edits outside a node's naming context SHOULD NOT rename unrelated generated symbols.

## §11.2.3 C# Generated Symbol Naming

### §11.2.3.1 Alias-based members

For a component declared with alias `@alias_name`:

- Generated public member name MUST be `NormalizeAliasToPascalCase(alias_name)`.
- Generated local variable name SHOULD be `camelCase(GeneratedMemberName)`.
- If normalization causes collision, implementation MUST apply deterministic disambiguation and emit a diagnostic when required.

### §11.2.3.2 Non-aliased nodes

For a component without alias:

- Base variable name SHOULD be derived from `ComponentTypeName` in camelCase.
- Same-scope collisions MUST be resolved by deterministic numeric suffixes based on source order.
- Example: `label`, `label2`, `label3`.

### §11.2.3.3 Helper method names

Generated helper method names (binding/setup/signal wiring) SHOULD follow PascalCase and include semantic role.

Example patterns:

- `BuildUiTree`
- `WireSignals`
- `ApplyBindings`

### §11.2.3.4 Snake_case to PascalCase Conversion

GUML uses `snake_case` for all identifiers except component names and type names (§3.6.1.1). The Source Generator MUST convert `snake_case` identifiers to C# PascalCase when generating code that references C# members.

Conversion rule: split the identifier on `_` boundaries, capitalize the first letter of each segment, then concatenate.

| GUML identifier | Generated C# member |
|----------------|---------------------|
| `user_name` | `UserName` |
| `on_button_pressed` | `OnButtonPressed` |
| `text` | `Text` |
| `is_logged_in` | `IsLoggedIn` |

This conversion applies to:

- Controller property/method access: `$controller.user_name` → C# `controller.UserName`.
- Chained member access: `$controller.data.name` → C# `controller.Data.Name`.
- Parameter names: `param string button_text` → generated C# property `ButtonText`.
- Event names: `event on_submit` → generated C# delegate `OnSubmit`.
If the converted PascalCase name does not match any member on the target C# type, a conforming implementation MUST produce a diagnostic.

## §11.2.4 Scene-tree Node Name Rules

For scene-tree node names, implementations MUST derive a document namespace prefix from the GUML file name (without extension).

Let `DocumentNamespace = NormalizeFileStem(fileNameWithoutExtension)`.

Unless otherwise specified, non-root generated node names MUST start with `DocumentNamespace`.

### §11.2.4.1 Root node naming

The generated root node `Node.Name` MUST equal the GUML file name without extension.

Example:

- File: `main_menu.guml`
- Root `Node.Name`: `main_menu`

If the file name cannot be used directly as a valid node name, implementation MUST apply deterministic normalization and SHOULD emit a diagnostic indicating the normalization.

### §11.2.4.2 Aliased node naming

For an aliased component, generated `Node.Name` MUST preserve alias semantics and MUST include the document namespace prefix.

If a valid documentation-comment naming marker exists on the same component, the marker name takes precedence over alias semantic naming (§11.2.4.5).

Recommended format:

- `DocumentNamespace + "__" + aliasSemanticName`

Example:

```guml
@submit_btn: Button {
    text: "Submit"
}
```

Generated node name:

- `main_menu__submit_btn`

### §11.2.4.3 Documentation-comment naming marker

If a component declaration has an immediately preceding documentation comment in the form `/// marker`, the marker MUST be interpreted as an explicit node naming marker.

Example:

```guml
/// actor_name
Label {}
```

For non-root nodes, when marker naming is enabled, the generated `Node.Name` MUST use:

- `DocumentNamespace + "__" + marker`

Marker validation rules:

- Marker text MUST be normalized to a valid node-name-safe identifier.
- Empty marker text is invalid and MUST produce a diagnostic.
- If marker-based naming collides after normalization and deterministic suffixing cannot resolve it, a diagnostic MUST be produced.

### §11.2.4.4 Non-aliased node naming

For a non-aliased component, generated `Node.Name` MUST include the document namespace prefix and SHOULD use a readable type-derived base name.

- Base: `DocumentNamespace + "__" + component type name`.
- Collisions: deterministic numeric suffixes in source order applied to the type suffix.

Example:

- `main_menu__Label`, `main_menu__Label2`, `main_menu__Label3`.

Bare names such as `Label1` without a document namespace prefix SHOULD NOT be generated.

### §11.2.4.5 Naming precedence

For non-root node names, implementations MUST apply the following precedence:

1. Documentation-comment marker name (`/// marker`).
2. Alias semantic name.
3. Type-derived default name.

All three branches MUST include `DocumentNamespace` as prefix for generated `Node.Name`.

### §11.2.4.6 Name length constraints

Generated node names MUST satisfy a deterministic maximum length policy.

- `MaxNodeNameLength` MUST be defined by the implementation and SHOULD default to 64 characters.
- If a generated name exceeds `MaxNodeNameLength`, the implementation MUST apply deterministic truncation.
- Truncation MUST preserve the `DocumentNamespace` prefix when possible.
- Truncation SHOULD append a short deterministic hash suffix to avoid collisions after shortening.

Recommended truncation shape:

- `<prefix>__<trimmedBase>_<hash8>`

If two names still collide after truncation and deterministic suffixing, a diagnostic MUST be produced.

## §11.2.5 Collision and Determinism Constraints

A conforming implementation MUST apply deterministic conflict resolution for both:

- C# symbols.
- Scene-tree `Node.Name` values.

When deterministic resolution cannot produce a valid unique result, implementation MUST produce a diagnostic.

Named diagnostic codes and severities for this chapter are defined in §10.3.

## §11.2.6 Relation to Other Chapters

- Works with §9.2 alias semantics for member generation.
- Consumes metadata from §11.1 for type/member compatibility checks.
- Does not alter mapping semantics in §8.3.
- Event names declared via `event` (§8.7) are converted to PascalCase for generated C# delegates (§11.2.3.4).
- Event subscriptions (`#event_name`, §8.4) are wired during the Wire Events phase (§4.4.2).
- Content projection rules (`=>` and projected `each`, §8.8) are lowered to per-item template instantiation with `$item` context injection.

## §11.2.7 Conformance Test Cases

### §11.2.7.1 Root name equals file name

- Input: `inventory_panel.guml` root component.
- Expected: generated root `Node.Name = "inventory_panel"`.

### §11.2.7.2 Aliased node name

- Input: aliased node `@title_label: Label`.
- Expected: generated `Node.Name = "inventory_panel__title_label"`; generated member `TitleLabel`.

### §11.2.7.3 Non-aliased repeated node names

- Input: three sibling `Label` nodes without alias.
- Expected: deterministic names `inventory_panel__Label`, `inventory_panel__Label2`, `inventory_panel__Label3` and stable generated locals.

### §11.2.7.4 Collision failure

- Input: aliases normalize to same identifier in same scope.
- Expected: `GUML2001` diagnostic.

### §11.2.7.5 Documentation marker naming

- Input: `/// actor_name` immediately before `Label {}`.
- Expected: generated `Node.Name = "inventory_panel__actor_name"`.

### §11.2.7.6 Invalid documentation marker

- Input: empty marker `///` or marker that normalizes to invalid empty token.
- Expected: `GUML2006` diagnostic.

### §11.2.7.7 Documentation marker overrides alias

- Input: component has both `/// actor_name` and alias `@title_label`.
- Expected: generated `Node.Name = "inventory_panel__actor_name"`.

### §11.2.7.8 Long name truncation

- Input: generated base name exceeds `MaxNodeNameLength`.
- Expected: deterministic truncated name with hash suffix and no ambiguity.

### §11.2.7.9 Truncation collision failure

- Input: two long names collapse to same truncated result and cannot be uniquely resolved.
- Expected: `GUML2007` diagnostic.

## §11.2.8 Translation Expression Code Generation

This section defines how `tr()` and `ntr()` expressions (§6.7) are lowered to C# code.

### §11.2.8.1 `tr()` Lowering

A `tr(msgid)` or `tr(msgid, options)` expression SHALL be lowered to:

```csharp
(Guml.StringProvider?.Tr(msgid, context, args) ?? msgid)
```

Where:

- `msgid` is the string literal (first argument).
- `context` is extracted from the `context` key in the options object, or `null` if absent.
- `args` is a `new Dictionary<string, object> { ... }` containing all non-`context` keys from the options object, or `null` if no such keys exist.

The null-coalescing `?? msgid` ensures fallback to the source text when `Guml.StringProvider` is not configured.

### §11.2.8.2 `ntr()` Lowering

An `ntr(singular, plural, count)` or `ntr(singular, plural, count, options)` expression SHALL be lowered to:

```csharp
(Guml.StringProvider?.Ntr(singular, plural, countExpr, context, args)
    ?? ((countExpr) == 1 ? singular : plural))
```

Where:

- `singular` and `plural` are the first two string literal arguments.
- `countExpr` is the generated C# expression for the third argument.
- `context` and `args` follow the same extraction rules as `tr()`.

The fallback selects singular when count equals 1, otherwise plural.

### §11.2.8.3 Dependency Injection for Locale Binding

When a `tr()` or `ntr()` expression appears in a binding context (`:=`), the code generator MUST add the pseudo-property `"_locale"` to the dependency set of that binding.

The `"_locale"` property change is raised by `GuiController` in response to `Guml.StringProvider.PropertyChanged("CurrentLocale")`. This mechanism allows all translation bindings to refresh automatically when the application locale changes.

If the options object contains reference expressions (e.g., `$controller.user_name`), those references SHALL also be included in the dependency set. The binding therefore re-evaluates on both locale change and referenced property changes.
