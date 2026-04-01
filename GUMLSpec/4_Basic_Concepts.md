# §4 Basic Concepts

## §4.1 Document Structure

A GUML document is the fundamental unit of a GUML program. Each document consists of, in order:

1. Zero or more *import directives* (§7.1).
2. Exactly one *root component declaration* (§8.1).

```antlr
guml_document
    : import_directive* root_component_declaration EOF
    ;
```

A document that contains no root component declaration is ill-formed. An implementation shall produce a diagnostic.

The root component declaration (§8.1.1) is the only component that may contain parameter declarations (§8.6) and event declarations (§8.7). All nested components use the child component declaration form (§8.1.2).

> *Example:* A minimal valid document:
> ```guml
> Label {}
> ```
> *end example*

> *Example:* A complete document with directives:
> ```guml
> import "components/header.guml"
> import "../shared/footer.guml" as Footer
>
> Panel {
>     Header {}
>     Footer {}
>     Label {
>         text: "Hello, World!"
>     }
> }
> ```
> *end example*

Only one root component declaration is permitted. An implementation shall reject a document that contains tokens after the closing `}` of the root component.

## §4.2 Execution Model

### §4.2.1 Source Generator Mode

In this mode, GUML documents are analyzed at compile-time by a C# Source Generator. The generator:

- Parses the `.guml` file associated with a `[GumlController]` attribute (§9.1).
- Emits C# source code (a partial class) that constructs the UI hierarchy.
- Generates strongly-typed properties for aliased nodes (§9.2).
- Validates component types, property names, and event subscriptions at compile time.

> *Note:* Controller-to-document association is defined by §9 and is attribute-driven through `[GumlController]` in language-level workflows. *end note*

## §4.3 Scopes

GUML defines the following scoping levels for name resolution:

### §4.3.1 Global Scope

The global scope contains identifiers available throughout the entire document. These are *global references* (§9.3) prefixed with `$`.

The following global references are predefined:

| Identifier | Description |
|------------|-------------|
| `$controller` | The instance of the C# controller class bound to the document. Always available. |
| `$root` | A reference to the root control node of the current component. Primarily used within imported reusable components (§7.1) to access properties set by the parent. |

Additional global references shall be provided to the Source Generator via compile-time metadata/configuration.

> *Note:* `$controller` and `$root` have special treatment in Source Generator workflows. Any non-predefined global reference must be registered before generation; otherwise the document is ill-formed and a diagnostic shall be produced. *end note*

### §4.3.2 Document Scope

Identifiers defined with the alias prefix `@` (§9.2) are visible throughout the document. They represent named references to specific nodes in the component tree.

In Source Generator mode, each alias generates a strongly-typed property on the controller class.

### §4.3.3 Local Scope

Local variables are introduced by `each` blocks (§8.8). Each `each` block creates its own scope containing the *index variable* and *value variable* declared in its header.

For projected `each` form (`each source => template_param`, §8.8), implementations inject a read-only projection context variable `$item` into the instantiated template scope.

| Projection Member | Meaning |
|-------------------|---------|
| `$item.index` | Current element index in iteration order. |
| `$item.value` | Current element value. |
| `$item.root` | Root component instance of the template declaration document. |

`$item` is only visible inside projection-instantiated templates and is not part of ordinary component local scope.

When `each` blocks are nested (with an intermediate component, as required by §8.8.1), the inner scope's parent points to the outer scope, forming a **scope chain**. Variable lookup proceeds from the innermost scope outward.

> *Example:* Scope chain in nested each blocks:
> ```guml
> Panel {
>     each $controller.rows { |row_idx, row|
>         VBoxContainer {
>             each row.items { |col_idx, item|
>                 // Here, `col_idx` and `item` are in the inner scope.
>                 // `row_idx` and `row` are accessible via the parent scope.
>                 Label { text: item.name }
>             }
>         }
>     }
> }
> ```
> *end example*

## §4.4 Component Lifecycle

A GUML component goes through the following lifecycle phases:

### §4.4.1 Compile-time Phases

| Phase | Description |
|-------|-------------|
| **Parse** | The Source Generator reads the `.guml` file and produces an AST (`GumlDoc`). Syntax errors are reported as diagnostics. |
| **Validate** | Component types, property names, parameter completeness, and event subscriptions are verified against API metadata (§11.1). |
| **CodeGen** | C# source code is emitted as a partial class containing `InitializeComponent()` and related members (§11.2). |

### §4.4.2 Runtime Phases

| Phase | Description |
|-------|-------------|
| **Instantiate** | The controller's `_Ready()` method calls `InitializeComponent()`. |
| **Build Tree** | Node instances are created, properties are assigned, and parent-child relationships are established in the Godot scene tree. |
| **Bind** | Data binding relationships are established for `:=`, `=:`, and `<=>` mappings (§8.3). |
| **Wire Events** | Event subscriptions (`#event_name`) are connected (§8.4). |
| **Active** | The component participates in Godot's scene tree update loop. Bindings propagate changes automatically. |
| **Dispose** | When the node exits the scene tree (`_ExitTree()`), bindings are released and event subscriptions are disconnected. |

> *Note:* The `InitializeComponent()` method encapsulates the Build Tree, Bind, and Wire Events phases into a single call. The controller author only needs to invoke it once in `_Ready()`. *end note*

> *Example:*
> ```csharp
> [GumlController("res://Scenes/Menu.guml")]
> public partial class MenuController : Control
> {
>     public override void _Ready()
>     {
>         InitializeComponent(); // Build Tree + Bind + Wire Events
>     }
>
>     public override void _ExitTree()
>     {
>         // Bindings and events are automatically cleaned up
>     }
> }
> ```
> *end example*


