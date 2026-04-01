# Data Binding

GUML supports reactive data binding — properties on UI nodes can be automatically synchronized with properties on the controller. This page covers all binding operators and the patterns needed to use them.

---

## Binding Operators

GUML has four assignment operators for component properties:

| Operator | Direction | Evaluation |
|----------|-----------|------------|
| `:` | None (static) | Once at build time |
| `:=` | Controller → UI | On every `INotifyPropertyChanged` change |
| `=:` | UI → Controller | When the node raises `notify/property_list_changed` |
| `<=>` | Two-way | Both of the above |

---

## Static Assignment (`:`)

A plain `:` is a **one-time assignment** evaluated when the component is built. Use this for fixed values that do not change at runtime:

```guml
Button {
    text: "Submit",
    custom_minimum_size: vec2(120, 40),
    disabled: false
}
```

---

## One-Way Binding (`:=`, Controller → UI)

`:=` establishes a **reactive binding** from a controller property to a UI property. Whenever the controller fires `PropertyChanged` for the referenced property, the UI node is updated automatically.

```guml
Label {
    text := $controller.score
}

ProgressBar {
    value := $controller.health,
    max_value := $controller.max_health
}
```

### Template String Bindings

A template string `$"..."` used with `:=` creates a multi-property binding. It re-evaluates whenever any referenced controller property changes:

```guml
Label {
    text := $"Score: {$controller.score} / {$controller.max_score}"
}

Label {
    text := $"{$controller.first_name} {$controller.last_name}"
}
```

### Expression Bindings

Any expression — arithmetic, comparison, ternary — used with `:=` is tracked:

```guml
ProgressBar {
    // value mapped from [0..1] to [0..100]
    value := $controller.health * 100
}

Label {
    modulate := $controller.is_active
                ? color(1.0, 1.0, 1.0, 1.0)
                : color(0.5, 0.5, 0.5, 1.0)
}

Button {
    disabled := !$controller.can_submit
}
```

---

## Making Controller Properties Bindable

For controller side, a property must call `OnPropertyChanged()` after assignment. Use `SetField` pattern or call it manually:

```c#
[GumlController("res://gui/profile.guml")]
public partial class ProfileController : GuiController
{
    private string _userName = "";
    public string UserName
    {
        get => _userName;
        set
        {
            _userName = value;
            OnPropertyChanged();   // notify all := bindings to "UserName"
        }
    }

    private int _score;
    public int Score
    {
        get => _score;
        set
        {
            _score = value;
            OnPropertyChanged();
        }
    }

    private bool _canSubmit;
    public bool CanSubmit
    {
        get => _canSubmit;
        set
        {
            _canSubmit = value;
            OnPropertyChanged();
        }
    }

    public override void Created()
    {
        UserName = "Player1";
        Score = 0;
        CanSubmit = true;
    }
}
```

> `OnPropertyChanged()` uses `[CallerMemberName]` to auto-detect the property name — no need to pass the name string explicitly.

---

## Reverse Binding (`=:`, UI → Controller)

`=:` pushes the value from the UI node back to the controller when the node itself updates it. This is uncommon and usually reserved for custom node types that modify their own properties.

```guml
CustomSlider {
    current_value =: $controller.volume
}
```

The node must raise `notify/property_list_changed` or call the equivalent Godot notification for changes to propagate.

---

## Two-Way Binding (`<=>`)

`<=>` combines both directions. The initial value comes from the controller, and changes in either direction update the other side:

```guml
LineEdit {
    text <=> $controller.search_query
}
```

> Two-way binding on standard Godot nodes has limitations. `LineEdit.text` must be set from a signal to properly reflect in the controller. In practice it is more common to use `:=` for display and `#text_changed` or `#text_submitted` for controller update.

---

## Binding vs Signal Pattern

For most input widgets, the recommended approach is:

```guml
HBoxContainer {
    // HSlider sends its own value to the UI — use :=
    @health_slider: HSlider {
        value := $controller.health,
        max_value: 100.0,
        #value_changed: $controller.on_health_changed
    }
}
```

```c#
public partial class StatsController : GuiController
{
    private double _health = 100;
    public double Health
    {
        get => _health;
        set { _health = value; OnPropertyChanged(); }
    }

    public void OnHealthChanged(double value)
    {
        // Slider moved by user — update model
        Health = value;
    }
}
```

This avoids feedback loops that can occur with `<=>` on widgets that emit signals when their value changes programmatically.

---

## `INotifyPropertyChanged` Under the Hood

`GuiController` implements `System.ComponentModel.INotifyPropertyChanged`. The GUML runtime subscribes to `PropertyChanged` for every `:=` binding. When the event fires:

1. The binding expression is re-evaluated.
2. The result is assigned to the target node property via reflection or a generated setter depending on context.

The binding is tracked at the property level: `OnPropertyChanged()` on `Score` only re-evaluates bindings that reference `$controller.score`.

---

## ObservableCollection for Collections

For list data used with `each` blocks, use `ObservableCollection<T>` from `System.Collections.ObjectModel`. It implements `INotifyCollectionChanged` and notifies the runtime when items are added, removed, or replaced:

```c#
using System.Collections.ObjectModel;

public partial class LeaderboardController : GuiController
{
    public ObservableCollection<string> Entries { get; } = new();

    public override void Created()
    {
        Entries.Add("Alice — 1200");
        Entries.Add("Bob — 950");
    }

    public void Refresh()
    {
        Entries.Clear();
        // re-add updated entries
    }
}
```

```guml
VBoxContainer {
    each $controller.entries {
        |idx, val|
        Label { text := val }
    }
}
```

See [List Rendering](list_rendering.md) for details on `each` blocks.

---

## Binding Scope in Imported Components

Inside a reusable component (`.guml` file loaded via `import`), `$controller` refers to the **host controller** of the parent file. Use `$root.<param_name>` to access the component's own parameters instead:

```guml
// components/ScoreDisplay.guml
param score: int
param label: string = "Score"

Label {
    text := $"[{$root.label}] {$root.score}"
}
```

See [Reusable Components](reusable_components.md) for the full picture.

---

## Complete Binding Example

```guml
// gui/game_hud.guml
VBoxContainer {
    // Health bar
    HBoxContainer {
        Label { text: "HP" }
        ProgressBar {
            size_flags_horizontal: .ExpandFill,
            value := $controller.health,
            max_value := $controller.max_health
        }
        Label {
            text := $"{$controller.health}/{$controller.max_health}"
        }
    }

    // Score display
    HBoxContainer {
        Label { text: "Score:" }
        Label {
            text := $controller.score,
            modulate := $controller.score > 1000
                        ? color(1.0, 0.8, 0.0, 1.0)
                        : color(1.0, 1.0, 1.0, 1.0)
        }
    }

    // Player name input
    HBoxContainer {
        Label { text: "Name:" }
        LineEdit {
            text := $controller.player_name,
            placeholder_text: "Enter name...",
            #text_submitted: $controller.on_name_submitted
        }
    }
}
```

```c#
[GumlController("res://gui/game_hud.guml")]
public partial class GameHudController : GuiController
{
    private double _health = 100;
    public double Health { get => _health; set { _health = value; OnPropertyChanged(); } }

    private double _maxHealth = 100;
    public double MaxHealth { get => _maxHealth; set { _maxHealth = value; OnPropertyChanged(); } }

    private int _score;
    public int Score { get => _score; set { _score = value; OnPropertyChanged(); } }

    private string _playerName = "Player";
    public string PlayerName { get => _playerName; set { _playerName = value; OnPropertyChanged(); } }

    public void OnNameSubmitted(string name)
    {
        PlayerName = name;
    }
}
```

---

## Next Steps

- **[Events](events.md)** — handle user interactions with `#signal` subscriptions.
- **[List Rendering](list_rendering.md)** — bind collections to repeating UI with `each`.
