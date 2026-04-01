# Events

GUML provides a concise syntax for subscribing to Godot signals directly in the markup. You can also declare custom events on reusable components that parent components can subscribe to.

---

## Signal Subscriptions

Use `#signal_name: handler` to connect a Godot signal to a controller method. The signal name uses `snake_case` matching Godot's signal names:

```guml
Button {
    text: "Click Me",
    #pressed: $controller.on_button_pressed
}
```

```c#
public partial class MyController : GuiController
{
    public void OnButtonPressed()
    {
        GD.Print("Button was pressed!");
    }
}
```

### Signal with Arguments

If the signal passes arguments, declare matching parameters on the handler method:

```guml
HSlider {
    value: 50.0,
    max_value: 100.0,
    #value_changed: $controller.on_slider_changed
}

LineEdit {
    #text_changed: $controller.on_text_changed,
    #text_submitted: $controller.on_text_submitted
}

CheckBox {
    #toggled: $controller.on_toggle_changed
}
```

```c#
public void OnSliderChanged(double value)
{
    GD.Print($"Slider: {value}");
}

public void OnTextChanged(string newText)
{
    SearchQuery = newText;
}

public void OnTextSubmitted(string text)
{
    GD.Print($"Submitted: {text}");
}

public void OnToggleChanged(bool pressed)
{
    IsChecked = pressed;
}
```

The GUML runtime connects signals using reflection. The handler method signature must match the signal's argument types exactly.

---

## Multiple Signals on One Node

A single component can have multiple signal subscriptions:

```guml
LineEdit {
    placeholder_text: "Search...",
    #text_changed: $controller.on_search_changed,
    #text_submitted: $controller.on_search_submitted,
    #focus_entered: $controller.on_search_focused,
    #focus_exited: $controller.on_search_blurred
}
```

---

## Common Godot Signals

| Node | Signal | Handler Signature |
|------|--------|-------------------|
| `Button`, `CheckButton` | `pressed` | `void OnX()` |
| `Button` | `button_down` / `button_up` | `void OnX()` |
| `CheckBox`, `CheckButton` | `toggled` | `void OnX(bool pressed)` |
| `LineEdit` | `text_changed` | `void OnX(string text)` |
| `LineEdit` | `text_submitted` | `void OnX(string text)` |
| `TextEdit` | `text_changed` | `void OnX()` |
| `HSlider`, `VSlider`, `SpinBox` | `value_changed` | `void OnX(double value)` |
| `OptionButton` | `item_selected` | `void OnX(int index)` |
| `ItemList` | `item_selected` | `void OnX(int index)` |
| `Timer` | `timeout` | `void OnX()` |
| `Control` | `focus_entered` / `focus_exited` | `void OnX()` |
| `Control` | `gui_input` | `void OnX(InputEvent event)` |
| `Node` | `tree_entered` / `tree_exited` | `void OnX()` |

---

## Custom Event Declarations

Reusable components (loaded via `import`) can expose custom events that the parent document subscribes to. Declare them with the `event` keyword at the top of the `.guml` file:

```guml
// components/ConfirmDialog.guml
param title: string = "Confirm"
param message: string
event confirmed
event cancelled

PanelContainer {
    VBoxContainer {
        Label { text := $root.title }
        Label { text := $root.message }
        HBoxContainer {
            Button {
                text: "OK",
                #pressed: $root.confirmed   // raises the 'confirmed' event
            }
            Button {
                text: "Cancel",
                #pressed: $root.cancelled
            }
        }
    }
}
```

### Subscribing to Custom Events

In the parent document, subscribe using the same `#event_name: handler` syntax after the component's property assignments:

```guml
// gui/main.guml
import "components/ConfirmDialog.guml" as ConfirmDialog

VBoxContainer {
    @confirm: ConfirmDialog {
        title: "Delete Item",
        message: "Are you sure you want to delete this item?",
        #confirmed: $controller.on_delete_confirmed,
        #cancelled: $controller.on_delete_cancelled
    }
}
```

```c#
public void OnDeleteConfirmed()
{
    DeleteSelectedItem();
}

public void OnDeleteCancelled()
{
    GD.Print("Deletion cancelled.");
}
```

### Events with Arguments

Events can carry arguments using the `event` declaration syntax with typed parameters:

```guml
// components/ColorPicker.guml
event color_selected(r: float, g: float, b: float)

PanelContainer {
    // ... color picker UI
    Button {
        text: "Apply",
        #pressed: $controller.on_apply
    }
}
```

The controller of the component raises it (currently via the runtime event bridge). For no-arg events, a bare `#pressed: $root.event_name` does the same thing.

---

## Input Handling

For keyboard and mouse input, subscribe to `gui_input` or use Godot's `_Input`/`_UnhandledInput` override methods:

```guml
Panel {
    focus_mode: .All,
    #gui_input: $controller.on_gui_input
}
```

```c#
public void OnGuiInput(InputEvent evt)
{
    if (evt is InputEventKey key && key.Pressed)
    {
        GD.Print($"Key: {key.Keycode}");
    }
}
```

---

## Signal Subscriptions in `each` Blocks

Signals inside `each` blocks work naturally. The handler receives the same arguments as usual; use the binding scope to pass item context if needed:

```guml
VBoxContainer {
    each $controller.items {
        |idx, val|
        Button {
            text := val,
            #pressed: $controller.on_item_click
        }
    }
}
```

> Currently, passing `idx` or `val` directly into a signal handler requires the controller to track the active index separately. For targeted item handling, use Godot's `GetMeta` or a factory-based approach.

---

## Complete Example

```guml
// gui/login_form.guml
VBoxContainer {
    @username_field: LineEdit {
        placeholder_text: "Username",
        #text_changed: $controller.on_username_changed,
        #text_submitted: $controller.on_field_submitted
    }

    @password_field: LineEdit {
        placeholder_text: "Password",
        secret: true,
        #text_changed: $controller.on_password_changed,
        #text_submitted: $controller.on_field_submitted
    }

    @error_label: Label {
        visible := $controller.has_error,
        text := $controller.error_message,
        modulate: color(1.0, 0.3, 0.3, 1.0)
    }

    HBoxContainer {
        @login_btn: Button {
            text: "Login",
            size_flags_horizontal: .ExpandFill,
            disabled := !$controller.can_login,
            #pressed: $controller.on_login
        }

        Button {
            text: "Cancel",
            #pressed: $controller.on_cancel
        }
    }
}
```

```c#
[GumlController("res://gui/login_form.guml")]
public partial class LoginFormController : GuiController
{
    private string _username = "";
    public string Username { get => _username; set { _username = value; OnPropertyChanged(); UpdateCanLogin(); } }

    private string _password = "";
    public string Password { get => _password; set { _password = value; OnPropertyChanged(); UpdateCanLogin(); } }

    private bool _canLogin;
    public bool CanLogin { get => _canLogin; set { _canLogin = value; OnPropertyChanged(); } }

    private bool _hasError;
    public bool HasError { get => _hasError; set { _hasError = value; OnPropertyChanged(); } }

    private string _errorMessage = "";
    public string ErrorMessage { get => _errorMessage; set { _errorMessage = value; OnPropertyChanged(); } }

    public void OnUsernameChanged(string text) => Username = text;
    public void OnPasswordChanged(string text) => Password = text;

    public void OnFieldSubmitted(string _) => OnLogin();

    public void OnLogin()
    {
        if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
        {
            ErrorMessage = "Please fill in all fields.";
            HasError = true;
            return;
        }
        HasError = false;
        // proceed with login...
    }

    public void OnCancel()
    {
        GumlRootNode.QueueFree();
    }

    private void UpdateCanLogin()
    {
        CanLogin = !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
    }
}
```

---

## Next Steps

- **[List Rendering](list_rendering.md)** — render dynamic collections with `each`.
- **[Reusable Components](reusable_components.md)** — declare and consume custom events via `event` in imported components.
