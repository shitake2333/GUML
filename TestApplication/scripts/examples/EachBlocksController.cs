using GUML;
using GUML.Shared;
using System.Collections.ObjectModel;

namespace TestApplication.scripts.examples;

/// <summary>
/// 08 - Each Blocks.
/// Demonstrates each-block rendering with live add/remove items and toggle user active.
/// </summary>
[GumlController("res://gui/examples/08_each_blocks.guml")]
public partial class EachBlocksController : GuiController
{
    private int _nextItemId = 4;
    private static readonly string[] s_itemNames = ["Sword", "Shield", "Potion", "Arrow", "Axe", "Bow", "Staff", "Wand"];

    // GUML: $controller.items  (each loop source)
    public ObservableCollection<ItemData> Items { get; } =
    [
        new ItemData { Name = "Sword",  Qty = 2 },
        new ItemData { Name = "Shield", Qty = 1 },
        new ItemData { Name = "Potion", Qty = 5 },
    ];

    // GUML: $controller.item_count
    public int ItemCount => Items.Count;

    // GUML: $controller.users  (each loop source with toggle)
    public ObservableCollection<UserInfo> Users { get; } =
    [
        new UserInfo { Id = 1, Name = "Alice",   IsActive = true  },
        new UserInfo { Id = 2, Name = "Bob",     IsActive = false },
        new UserInfo { Id = 3, Name = "Charlie", IsActive = true  },
    ];

    // GUML: $controller.rows  (nested each demo)
    public ObservableCollection<RowData> Rows { get; } =
    [
        new RowData { Items = [new CellData { Display = "A1" }, new CellData { Display = "A2" }, new CellData { Display = "A3" }] },
        new RowData { Items = [new CellData { Display = "B1" }, new CellData { Display = "B2" }, new CellData { Display = "B3" }] },
        new RowData { Items = [new CellData { Display = "C1" }, new CellData { Display = "C2" }, new CellData { Display = "C3" }] },
    ];

    // GUML: #pressed: $controller.add_item
    public void AddItem()
    {
        string name = s_itemNames[_nextItemId % s_itemNames.Length];
        Items.Add(new ItemData { Name = name, Qty = _nextItemId });
        _nextItemId++;
        // ObservableCollection fires CollectionChanged automatically; PropertyChanged propagates item_count
        OnPropertyChanged(nameof(ItemCount));
    }

    // GUML: #pressed: $controller.remove_item
    public void RemoveItem()
    {
        if (Items.Count == 0) return;
        Items.RemoveAt(Items.Count - 1);
        OnPropertyChanged(nameof(ItemCount));
    }

    // GUML: #pressed: $controller.toggle_user(user.id)
    public void ToggleUser(int userId)
    {
        for (int i = 0; i < Users.Count; i++)
        {
            if (Users[i].Id == userId)
            {
                // ObservableCollection indexed setter fires CollectionChanged(Replace), re-renders the row
                Users[i] = Users[i] with { IsActive = !Users[i].IsActive };
                break;
            }
        }
    }
}

public record ItemData
{
    public string Name { get; init; } = "";
    public int Qty { get; init; } = 1;
}

public class RowData
{
    public ObservableCollection<CellData> Items { get; set; } = [];
}

public class CellData
{
    public string Display { get; set; } = "";
}

public record UserInfo
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public bool IsActive { get; init; }
}
