# §9 Controller Integration

Every GUML document is associated with a C# controller class. The controller provides the data model, event handlers, and business logic for the UI defined in the GUML file.

## §9.1 GumlController Attribute

The association between a C# class and a GUML file is established via the `[GumlController]` attribute.

```csharp
[GumlController("<path_to_guml_file>")]
public partial class <ClassName> : <BaseGodotNode>
{
    ...
}
```

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| Path | `string` | The resource path to the `.guml` file. |

A conforming language-level workflow shall treat `[GumlController]` as the normative association mechanism between controller types and GUML documents.

**Generated Members:**

The Source Generator produces the following in the partial class:

- An `InitializeComponent()` method that constructs the UI hierarchy and performs all property assignments, bindings, and event subscriptions.
- A strongly-typed property for each alias (§9.2) declared in the GUML file.

> *Example:*
> ```csharp
> using Godot;
> using GUML;
>
> [GumlController("res://Scenes/Menu.guml")]
> public partial class MenuController : Control
> {
>     public override void _Ready()
>     {
>         InitializeComponent();
>     }
>
>     private void OnStartPressed()
>     {
>         // Handle button press
>         // Accessed from GUML as: $controller.on_start_pressed
>     }
> }
> ```
> *end example*

**Naming Convention:** Each `.guml` file shall have a corresponding controller class. The naming convention is to convert the `.guml` file name to PascalCase and append `Controller`. For example, `main.guml` corresponds to `MainController`.

## §9.2 Aliases

An alias is a named reference to a specific node in the component tree, declared with the `@` prefix (§3.6.1).

```antlr
alias_prefix
    : '@' identifier_part+ ':'
    ;
```

> *Example:*
> ```guml
> @name_input: LineEdit {
>     placeholder_text: "Enter your name"
> }
> ```
> *end example*

**Behavior:**

In Source Generator mode, a public property is generated for each alias, for example:

`public LineEdit NameInput { get; internal set; }`

> *Note:* In Source Generator mode, the alias name is converted to PascalCase for the property name. For example, `@name_input` becomes `NameInput`. Alias names in GUML shall use `snake_case` (§3.6.1.1). *end note*

## §9.3 Global References

Global references are identifiers prefixed with `$` (§3.6.1) that resolve to externally provided objects.

### §9.3.1 Predefined Global References

| Identifier | Description |
|------------|-------------|
| `$controller` | Always available. References the controller instance bound to the current document. Has special treatment in Source Generator mode — mapped directly to the controller variable rather than a dictionary lookup. |
| `$root` | References the root control node of the current component. Primarily used within imported reusable components (§7.1) to access properties set by the parent context. |

> *Note:* `$controller` and `$root` are predefined globals in Source Generator workflows. Any other global reference name must be supplied through compile-time metadata/configuration available to the generator. *end note*

> *Example:* Using `$root` inside a reusable component:
> ```guml
> // In MyButton.guml
> Button {
>     param string button_text: "default",
>     text: $root.button_text
> }
> ```
> When used by a parent:
> ```guml
> import "components/MyButton"
>
> Panel {
>     MyButton { button_text: "Click Me" }
> }
> ```
> Here `$root` resolves to the `Button` root node of `MyButton`, and `.button_text` accesses the parameter set by the parent.
> *end example*

