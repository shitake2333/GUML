# List Rendering

GUML supports rendering dynamic collections with `each` blocks. When the data source changes, only the affected rows are added, removed, or updated — no full rebuild needed.

---

## Block Form

The `each` block form iterates over a collection and renders a component template for each element:

```
each <expression> {
    |<index_var>, <value_var>|
    // component tree
}
```

- **`<expression>`** — any expression that yields a collection. Typically `$controller.property_name`.
- **`|idx, val|`** — declares the loop variables. `idx` is 0-based; `val` is the item.
- The body is a component tree rendered once per item.

### Basic Example

```guml
VBoxContainer {
    each $controller.messages { |i, msg|
        Label {
            text := $"{i + 1}. {msg}"
        }
    }
}
```

```c#
public NotifyList<string> Messages { get; } = new();
```

### Item Properties

If the item is an object, access its properties directly in expressions:

```guml
each $controller.users { |i, user|
    HBoxContainer {
        Label {
            text := $"{i + 1}. {user.name}",
            size_flags_horizontal: .Fill,
            modulate := user.is_active
                        ? color(1, 1, 1, 1)
                        : color(0.5, 0.5, 0.5, 1)
        }
        Label {
            text := user.is_active ? "Active" : "Inactive"
        }
        Button {
            text: "Toggle",
            #pressed: $controller.toggle_user(user.id)
        }
    }
}
```

### Passing Arguments Through Signal Handlers

Signal handlers inside `each` blocks can receive item-specific arguments. Call the handler with arguments using the call expression syntax:

```guml
each $controller.items { |idx, item|
    Button {
        text := item.name,
        #pressed: $controller.on_select(item.id)
    }
}
```

```c#
public void OnSelect(int id)
{
    SelectedId = id;
}
```

---

## `NotifyList<T>` — Reactive Collections

For `each` to track changes incrementally, the data source must implement `INotifyListChanged`. Use the built-in `NotifyList<T>`:

```c#
public partial class InventoryController : GuiController
{
    public NotifyList<InventoryItem> Items { get; } = new();

    public override void Created()
    {
        Items.Add(new InventoryItem(1, "Sword", 1));
        Items.Add(new InventoryItem(2, "Potion", 5));
    }

    public void AddItem(InventoryItem item)
    {
        Items.Add(item);       // triggers Add row at the end
    }

    public void RemoveItem(int id)
    {
        var item = Items.FirstOrDefault(x => x.Id == id);
        if (item != null)
            Items.Remove(item);  // triggers Remove row
    }

    public void UseItem(int id)
    {
        int index = Items.IndexOf(Items.First(x => x.Id == id));
        var item = Items[index];
        item.Quantity--;
        Items[index] = item;   // triggers ValueChanged — only updates bindings on that row
    }
}
```

### NotifyList Operations

| Operation | UI Effect |
|-----------|-----------|
| `Add(item)` | Appends a new row |
| `Insert(index, item)` | Inserts a row at position |
| `Remove(item)` / `RemoveAt(index)` | Removes that row |
| `Items[i] = newItem` | Fires `ValueChanged` — updates only `:=` bindings referencing `val` on that row |
| `Clear()` | Removes all rows |

### Value-Changed Bindings

`:=` bindings that reference the loop variable `val` or `item` re-evaluate when the element at that index is replaced via `Items[i] = ...`:

```guml
each $controller.inventory_items { |idx, item|
    Label {
        text := $"x{item.quantity}"   // updates when Items[i] = ... fires ValueChanged
    }
}
```

---

## Nested `each` Blocks

`each` blocks **cannot** be directly nested inside each other's body. To render a nested list, use an intermediate component that accepts a collection parameter:

```guml
// components/RowItems.guml
param items: NotifyList<CellData>

HBoxContainer {
    each $root.items { |col, cell|
        Label {
            text := cell.display,
            custom_minimum_size: vec2(36, 24),
            horizontal_alignment: .Center
        }
    }
}
```

```guml
// main.guml
import "components/RowItems.guml" as RowItems

VBoxContainer {
    each $controller.rows { |row_idx, row|
        RowItems {
            items: row.items
        }
    }
}
```

---

## `$item` Scope Reference

Inside an `each` block, the special `$item` object provides runtime context:

| Reference | Type | Description |
|-----------|------|-------------|
| `$item.index` | `int` | 0-based index in the collection |
| `$item.value` | `T` | The current element |
| `$item.root` | `Control` | The root node of this row's component |

Use `$item` when you need the index or value but did not declare named loop variables:

```guml
each $controller.lines { ||
    Label {
        text := $"Line {$item.index}: {$item.value}"
    }
}
```

Or access both the declared variable form and `$item.root` for the row node.

---

## Projection Form (`=>`)

> Available in interpreter mode; in source generator mode, use the block form inside the parent document.

The projection form delegates the per-item template to the parent:

```guml
// components/ItemList.guml
param items: NotifyList<ItemData>
param template_param

VBoxContainer {
    each $root.items => template_param
}
```

In the consuming file, the body inside `ItemList { }` provides the template for each item:

```guml
ItemList {
    items: $controller.inventory,
    |idx, item|
    HBoxContainer {
        Label { text := item.name }
        Label { text := $"x{item.qty}" }
    }
}
```

---

## Dynamic Add / Remove Example

```guml
VBoxContainer {
    HBoxContainer {
        Label {
            text := $"Items ({$controller.item_count}):",
            size_flags_horizontal: .Fill
        }
        Button {
            text: "+ Add",
            #pressed: $controller.add_item
        }
        Button {
            text: "- Remove Last",
            disabled := $controller.item_count == 0,
            #pressed: $controller.remove_last
        }
    }

    each $controller.items { |idx, item|
        HBoxContainer {
            Label {
                text := $"{idx + 1}. {item.name}",
                size_flags_horizontal: .Fill
            }
            Label {
                text := $"Qty: {item.qty}",
                modulate: color(0.7, 0.9, 0.7, 1.0)
            }
        }
    }
}
```

```c#
using System.Collections.ObjectModel;

[GumlController("res://gui/inventory.guml")]
public partial class InventoryController : GuiController
{
    private int _nextId = 1;

    public ObservableCollection<ItemData> Items { get; } = new();

    public int ItemCount => Items.Count;

    public void AddItem()
    {
        Items.Add(new ItemData(_nextId++, $"Item {_nextId}", 1));
        OnPropertyChanged(nameof(ItemCount));
    }

    public void RemoveLast()
    {
        if (Items.Count > 0)
        {
            Items.RemoveAt(Items.Count - 1);
            OnPropertyChanged(nameof(ItemCount));
        }
    }
}

public record ItemData(int Id, string Name, int Qty);
```

---

## Reconciliation Rules

GUML performs **incremental reconciliation** for `ObservableCollection` changes:

- **Add** — a new row node tree is built and inserted at the correct position.
- **Insert** — same as Add at specified index.
- **Remove** — the row at the specified index is freed.
- **Replace** (`Items[i] = ...`) — only `:=` bindings within that row re-evaluate; no node is created or destroyed.
- **Reset / Clear** — all row nodes are freed and rebuilt.

This means: prefer `ObservableCollection<T>` over plain `List<T>` for performance. A `List<T>` data source will trigger a full rebuild whenever the property fires `PropertyChanged`.

---

## Next Steps

- **[Reusable Components](reusable_components.md)** — pass lists as parameters and define projection-based templates.
- **[Data Binding](data_binding.md)** — understand how `INotifyPropertyChanged` and `:=` work together.
- **[API Reference](../reference/api.md)** — `ObservableCollection<T>` change events and reconciliation details.
