# §8 Components

Components are the fundamental building blocks of a GUML user interface. A component maps to a Godot Node in the scene tree.

## §8.1 Component Declarations

A component declaration defines a UI element with an optional alias, a type name, and a body.

GUML distinguishes between the **root component declaration** and **child component declarations**. A GUML document contains exactly one root component declaration (§4.1). Only the root component may contain parameter declarations (§8.6) and event declarations (§8.7), because these define the external interface of the document when it is imported as a reusable component (§7.1).

### §8.1.1 Root Component Declaration

```antlr
root_component_declaration
    : documentation_comment? alias_prefix? component_name '{' root_component_body '}'
    ;

documentation_comment
    : documentation_comment_line+
    ;

documentation_comment_line
    : '///' input_character*
    ;

alias_prefix
    : alias_ref ':'
    ;

root_component_body
    : root_component_body_element*
    ;

root_component_body_element
    : property_assignment ','?
    | mapping_assignment ','?
    | event_subscription ','?
    | template_param_assignment ','?
    | parameter_declaration ','?
    | event_declaration ','?
    | each_block
    | component_declaration
    ;
```

### §8.1.2 Child Component Declaration

Child component declarations appear inside the root component body or nested within other child components. They **shall not** contain `parameter_declaration` or `event_declaration`.

```antlr
component_declaration
    : documentation_comment? alias_prefix? component_name '{' component_body '}'
    ;

component_body
    : component_body_element*
    ;

component_body_element
    : property_assignment ','?
    | mapping_assignment ','?
    | event_subscription ','?
    | template_param_assignment ','?
    | each_block
    | component_declaration
    ;
```

The *component_name* shall start with an uppercase letter (§3.6.1). It corresponds to a Godot node type (e.g., `Label`, `Button`, `VBoxContainer`) or a custom C# class, or an imported component alias.

When `documentation_comment` is present, it is associated with the current component declaration. The documentation block may span multiple `///` lines. Non-tag lines form the description text; tag lines provide structured metadata (§3.4.1).

A `@name` tag within a component's documentation comment specifies a node naming marker for code generation workflows (§11.2).

> *Example:*
> ```guml
> /// A submit button for the form.
> /// @name submit_btn_node
> @submit_btn: Button {
>     text: "Submit",
>     #pressed: $controller.on_submit
> }
> ```
> In this example, `@submit_btn` is the alias prefix, `Button` is the component name, `submit_btn_node` is the node naming marker (from the `@name` tag), and the body contains one property assignment and one event subscription.
> *end example*

### §8.1.3 Comma Separators

Commas are used to separate adjacent **value-assignment members** within a component body. Value-assignment members include property assignments (§8.2), mapping assignments (§8.3), parameter declarations (§8.6), event declarations (§8.7), event subscriptions (§8.4), and template parameter assignments.

The following rules apply:

1. A comma is **required** between two adjacent value-assignment members. A missing comma shall produce a diagnostic (`GUML1016`).
2. A **trailing comma** after the last value-assignment member (before a structural element or closing brace) is **optional**.
3. Commas are **not required** before or after **structural elements**: child component declarations (§8.5), `each` blocks (§8.8), and alias-prefixed declarations (`@name: Component { ... }`).

> *Example:*
> ```guml
> Label {
>     // Comma required between adjacent value-assignments
>     text: "hello",
>     visible: true,
>
>     // No comma needed before/after structural child components
>     Label { text: "child" }
>
>     // Trailing comma is optional
>     modulate: color(1.0, 1.0, 1.0, 1.0),
> }
> ```
> *end example*

> *Example:* Ill-formed — missing comma between value-assignment members:
> ```guml
> // ERROR: missing comma between text and visible (GUML1016)
> Label { text: "hello" visible: true }
> ```
> *end example*

## §8.2 Property Assignments

A property assignment sets the initial value of a property on the component.

```antlr
property_assignment
    : property_name ':' expression
    ;

property_name
    : identifier
    ;
```

The *property_name* is always an `identifier` token (lowercase-starting, §3.6.1). All property names in GUML use `snake_case` (§3.6.1.1), including Godot node properties and custom component parameters (§8.6).

Because property names are always lowercase-starting and component names are always uppercase-starting, there is no syntactic ambiguity between a property assignment and a child component declaration.

> *Example:*
> ```guml
> Label {
>     text: "Hello",
>     horizontal_alignment: .Center,
>     custom_minimum_size: vec2(100, 50),
>     label_settings: new LabelSettings { font_size: 24 }
> }
> ```
> *end example*

## §8.3 Property Mapping and Binding

GUML supports four mapping modes between component properties and data expressions.

```antlr
mapping_assignment
    : property_name mapping_operator expression
    ;

mapping_operator
    : ':='
    | '=:'
    | '<=>'
    ;
```

### §8.3.1 Mapping Modes

| Operator | Direction | Description |
|----------|-----------|-------------|
| `:` | static | One-time assignment (§8.2). |
| `:=` | data -> property | One-way binding from data source to component property. |
| `=:` | property -> data | One-way binding from component property back to data target. |
| `<=>` | property <-> data | Two-way binding combining both directions. |

For `=:` and `<=>`, the right-hand side expression shall be a writable reference expression (typically a settable controller property).

### §8.3.2 Capability Constraints

- Built-in Godot UI nodes generally guarantee `:=` (data -> property) behavior.
- `=:` and `<=>` require the component property side to expose change notifications (for example, custom nodes implementing `INotifyPropertyChanged`-compatible behavior or equivalent notifications).
- If a required direction is not supported by the component/data endpoint, a conforming Source Generator implementation shall produce a diagnostic.

> *Example:* Data -> property
> ```guml
> Label {
>     text := $controller.user_name,
>     visible := $controller.is_logged_in
> }
> ```
> *end example*

> *Example:* Property -> data (requires property change notification support on the component side)
> ```guml
> CustomTextInput {
>     text =: $controller.user_name
> }
> ```
> *end example*

> *Example:* Two-way binding
> ```guml
> CustomToggle {
>     checked <=> $controller.is_enabled
> }
> ```
> *end example*

## §8.4 Event Subscriptions

An event subscription binds an event to a handler on the controller. In GUML, both Godot signals and C# delegate-based events are unified under the single concept of **events**. The `#` prefix identifies the event being subscribed to.

```antlr
event_subscription
    : event_ref ':' expression
    ;
```

The *event_ref* (§3.6.1) begins with `#` and names an event using `snake_case` (e.g., `#pressed`, `#button_down`, `#on_submit`).

The handler *expression* shall be a reference expression (§6.5) pointing to a method or delegate on the controller, or a call expression (§6.6). String literals are not permitted as handler expressions.

**Unified Event Model:**

At the GUML language level, there is no distinction between Godot signals and C# events/delegates. Both are referred to as "events". The Source Generator determines the appropriate connection mechanism at code generation time:

| Event Origin | Generated Connection |
|-------------|---------------------|
| Godot built-in signal (e.g., `#pressed`) | `node.Pressed += handler` or `node.Connect("pressed", ...)` |
| Custom C# event (e.g., `#on_submit`) | `component.OnSubmit += handler` |
| Declared event (§8.7) on imported component | Generated delegate subscription |

> *Example:*
> ```guml
> Button {
>     text: "Click Me",
>     #pressed: $controller.on_button_pressed,
>     #toggled: $controller.on_button_toggled
> }
> ```
> *end example*

> *Example:* Subscribing to an imported component's declared event:
> ```guml
> import "components/SearchBar.guml"
>
> Panel {
>     SearchBar {
>         #on_search: $controller.handle_search
>     }
> }
> ```
> *end example*

> *Note:* Event subscriptions always use `:` (static assignment), not `:=` (binding). *end note*

## §8.5 Child Components

A component declaration may contain nested component declarations as children. Child components become children of the parent node in the Godot scene tree.

Child components use `component_body` (§8.1.2), which does not permit `parameter_declaration` or `event_declaration`. These declarations are only valid in the root component body (§8.1.1).

Children are added to the parent in the order they appear in the source.

> *Example:*
> ```guml
> VBoxContainer {
>     Label { text: "First" }
>     Label { text: "Second" }
>     Button { text: "Action" }
> }
> ```
> This creates a `VBoxContainer` with three children: two `Label` nodes and one `Button` node.
> *end example*

## §8.6 Parameter Declarations

A parameter declaration defines a public parameter for the document's root component, allowing data to be passed in when the component is used as an imported reusable component (§7.1).

Parameter declarations **shall** only appear in `root_component_body` (§8.1.1). A conforming implementation shall produce a syntax-level error if a `parameter_declaration` appears inside a child component body.

```antlr
parameter_declaration
    : documentation_comment? 'param' type_name parameter_name (':' expression)?
    | documentation_comment? 'param' type_name parameter_name (':=' expression)?
    ;

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

parameter_name
    : identifier
    ;
```

- *documentation_comment*: An optional documentation block (§3.4) providing a description of the parameter. Tooling may display this documentation in hover information and completion suggestions.
- *type_name*: The data type of the parameter. May be a simple identifier (e.g., `string`, `int`), a component name (e.g., `Texture2D`), a keyword type name (e.g., `vec2`), or a **generic type** (e.g., `List<int>`, `Dictionary<String, List<int>>`). Generic type arguments are enclosed in `<` and `>` and may be nested.
- *parameter_name*: The name of the parameter. Shall be `snake_case` (§3.6.1.1).
- The optional default value follows `:` or `:=`.

### §8.6.1 Required vs Optional Parameters

A parameter **without** a default value is a **required parameter**. When the component is used by a parent document, the parent **must** provide a value for every required parameter. Failure to do so is ill-formed and a conforming implementation shall produce a diagnostic (`GUML2008`).

A parameter **with** a default value (`:` or `:=`) is an **optional parameter**. The parent may omit it, in which case the default value is used.

> *Example:*
> ```guml
> // In MyCard.guml
> MyComponent {
>     /// The title text displayed at the top.
>     param string title,                      // Required
>     /// An optional subtitle shown below the title.
>     param string subtitle: "No subtitle",    // Optional
>     param int count: 10,                     // Optional
>     param List<InventoryItem> items,          // Generic type parameter
>
>     Label { text: title }
> }
> ```
> *end example*

> *Example:* Usage — required parameter must be provided:
> ```guml
> import "components/MyCard.guml"
>
> Panel {
>     // Well-formed: required 'title' is provided
>     MyCard { title: "Hello" }
>
>     // Ill-formed: required 'title' is missing -> GUML2008
>     // MyCard { subtitle: "World" }
> }
> ```
> *end example*

Parameter names SHOULD NOT collide with Godot built-in property names on the same node type to avoid shadowing.

## §8.7 Event Declarations

An event declaration defines a custom event that the document's root component can emit, allowing parent components to subscribe to it via event subscriptions (§8.4) when the document is used as an imported reusable component (§7.1).

In GUML, the `event` keyword declares events that unify both C# delegates and Godot signals at the language level. The Source Generator determines the appropriate implementation mechanism (C# `event` delegate, Godot signal, etc.) at code generation time.

Event declarations **shall** only appear in `root_component_body` (§8.1.1). A conforming implementation shall produce a syntax-level error if an `event_declaration` appears inside a child component body.

```antlr
event_declaration
    : documentation_comment? 'event' event_name event_arguments?
    ;

event_name
    : identifier
    ;

event_arguments
    : '(' event_argument_list ')'
    ;

event_argument_list
    : event_argument (',' event_argument)*
    ;

event_argument
    : type_name identifier?
    ;
```

Event names shall be `snake_case` (§3.6.1.1). The Source Generator converts event names to PascalCase for the generated C# delegate/event (e.g., `on_submit` → `OnSubmit`).

An optional documentation block (§3.4) may precede the `event` keyword to describe the event's semantics. The `@param` tag (§3.4.1) within an event's documentation block provides documentation for individual event arguments.

> *Example:*
> ```guml
> MyComponent {
>     /// Emitted when the form is submitted.
>     event on_submit,
>     /// Emitted when the input value changes.
>     /// @param new_value The updated string value.
>     event on_changed(string new_value),
>     event on_clicked(vec2 pos, int button),
>     event on_items_updated(List<InventoryItem> items)
> }
> ```
> *end example*

## §8.8 Each Blocks

An `each` block iterates over a collection and instantiates child components for each item.

```antlr
each_block
    : 'each' each_params? reference_expression '{' '|' index_name ',' value_name '|' component_body '}'
    | 'each' each_params? reference_expression '=>' template_param_name
    ;

template_param_assignment
    : parameter_name '=>' component_declaration
    ;

template_param_name
    : identifier
    ;

each_params
    : '(' object_literal ')'
    ;

index_name
    : identifier
    ;

value_name
    : identifier
    ;
```

- *template_param_assignment*: Assigns a component template to a parameter declared with `param` and used for projection.
- *template_param_name*: The name of a template parameter consumed by projection form.
- *each_params*: An optional parenthesized object literal providing configuration parameters for the each block.
- *reference_expression*: An expression (§6.5) evaluating to a collection (typically on the controller).
- *index_name*: The name of the loop index variable.
- *value_name*: The name of the loop value variable.

There are two `each` forms:

- **Block form**: `each ... { |idx, item| ... }` introduces explicit local variables.
- **Projection form**: `each ... => item_template` instantiates a template parameter once per collection element.

In projection form, implementations shall inject a read-only projection context named `$item` into the instantiated template scope:

| Member | Type | Description |
|--------|------|-------------|
| `$item.index` | `int` | Current element index in iteration order. |
| `$item.value` | `T` | Current element value (`T` is collection element type). |
| `$item.root` | `Node` | Root component instance of the template declaration document. |

For component parameters, collection references SHALL use `$root.<parameter_name>` (for example, `$root.items`) rather than implicit `$items` variables.

`template_param_assignment` and projection form are coupled:

- When a component uses projection form referencing `item_template`, the parent component usage shall provide `item_template => ComponentName { ... }`.
- The right-hand side of `template_param_assignment` shall be exactly one `component_declaration`.
- Using `=>` on a non-parameter target is ill-formed and shall produce a diagnostic (`GUML2010`).
- Accessing `$item` outside a projection-instantiated template scope is ill-formed and shall produce a diagnostic (`GUML2011`).

> *Example:*
> ```guml
> VBoxContainer {
>     each $controller.items { |idx, item|
>         Label {
>             text: item.name
>         }
>     }
> }
> ```
> *end example*

> *Example:* With optional parameters:
> ```guml
> GridContainer {
>     each ({ columns: 3 }) $controller.images { |i, img|
>         TextureRect {
>             texture: img
>         }
>     }
> }
> ```
> *end example*

> *Example:* Content projection with template parameter:
> ```guml
> // In ListView.guml
> ListView {
>     param IEnumerable items
>     param Node item_template
>
>     VBoxContainer {
>         each $root.items => item_template
>     }
> }
>
> // In InventoryPage.guml
> InventoryPage {
>     ListView {
>         items: $controller.items,
>         item_template => ItemRow {
>             text: $"{$item.index}: {$item.value.display_name}",
>             selected := $item.value.id == $item.root.current_selected_id
>         }
>     }
> }
> ```
> *end example*

### §8.8.1 Nesting Rules

`each` blocks **shall not** be directly nested. An intermediate component declaration is required between nested `each` blocks.

> *Example:* This is **ill-formed**:
> ```guml
> // ERROR: each blocks cannot be directly nested
> each $controller.data_a { |ia, va|
>     each $controller.data_b { |ib, vb|
>         Label {}
>     }
> }
> ```
> *end example*

> *Example:* This is **well-formed**:
> ```guml
> each $controller.data_a { |ia, va|
>     Control {
>         each $controller.data_b { |ib, vb|
>             Label {}
>         }
>     }
> }
> ```
> The intermediate `Control` component creates a separate parent context for the inner `each` block.
> *end example*

### §8.8.2 Incremental Updates

When the data source collection changes at runtime:

- **Add**: Only the newly added item is rendered incrementally. Existing nodes are not recreated.
- **Remove / Insert / Clear**: A full re-render of all children within the `each` block is performed.

