using GUML;
using GUML.Shared;
using System.Collections.ObjectModel;

namespace TestApplication.scripts.examples;

/// <summary>
/// 11 - Each Projection.
/// Demonstrates each-block rendering with a live inventory:
/// items can be "used" to reduce quantity (NotifyList ValueChanged re-renders the row).
/// </summary>
[GumlController("res://gui/examples/11_each_projection.guml")]
public partial class EachProjectionController : GuiController
{
    // GUML: $controller.inventory_items — must be ObservableCollection for each binding
    public ObservableCollection<InventoryItem> InventoryItems { get; } =
    [
        new InventoryItem { Id = 1, DisplayName = "Health Potion", Quantity = 5 },
        new InventoryItem { Id = 2, DisplayName = "Mana Potion",   Quantity = 3 },
        new InventoryItem { Id = 3, DisplayName = "Iron Sword",    Quantity = 1 },
        new InventoryItem { Id = 4, DisplayName = "Wooden Shield", Quantity = 2 },
    ];

    // GUML: $controller.use_item($item.value.id)
    public void UseItem(int itemId)
    {
        for (int i = 0; i < InventoryItems.Count; i++)
        {
            if (InventoryItems[i].Id == itemId)
            {
                var item = InventoryItems[i];
                if (item.Quantity > 0)
                    // ObservableCollection indexed setter fires CollectionChanged(Replace), re-renders that row
                    InventoryItems[i] = item with { Quantity = item.Quantity - 1 };
                else
                    InventoryItems.RemoveAt(i);
                break;
            }
        }
    }
}

public record InventoryItem
{
    public int Id { get; init; }
    public string DisplayName { get; init; } = "";
    public int Quantity { get; init; }
}
