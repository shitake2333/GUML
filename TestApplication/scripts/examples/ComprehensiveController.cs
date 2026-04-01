using Godot;
using GUML;
using GUML.Shared;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TestApplication.scripts.examples;

/// <summary>
/// 12 - Comprehensive: User Management Panel.
/// Combines data binding, events, each blocks, aliases and template strings.
/// </summary>
[GumlController("res://gui/examples/12_comprehensive.guml")]
public partial class ComprehensiveController : GuiController
{
    // GUML: $controller.total_users
    public int TotalUsers => _allUsers.Count;

    // GUML: $controller.active_count
    public int ActiveCount => _allUsers.Count(u => u.IsActive);

    // GUML: $controller.filtered_count
    public int FilteredCount => FilteredUsers.Count;

    // GUML: $controller.filtered_users  (each loop source)
    public ObservableCollection<ManagedUser> FilteredUsers { get; private set; }

    // GUML: $controller.is_loading
    public bool IsLoading { get; } = false;

    private readonly List<ManagedUser> _allUsers =
    [
        new ManagedUser { Id = 1, DisplayName = "Alice",   IsActive = true  },
        new ManagedUser { Id = 2, DisplayName = "Bob",     IsActive = true  },
        new ManagedUser { Id = 3, DisplayName = "Charlie", IsActive = false },
        new ManagedUser { Id = 4, DisplayName = "Diana",   IsActive = true  },
    ];

    public ComprehensiveController()
    {
        FilteredUsers = new ObservableCollection<ManagedUser>(_allUsers);
    }

    // GUML: #on_search: $controller.filter_users
    public void FilterUsers(string query)
    {
        var result = string.IsNullOrEmpty(query)
            ? _allUsers
            : _allUsers.Where(u =>
                u.DisplayName.Contains(query, System.StringComparison.OrdinalIgnoreCase));
        FilteredUsers = new ObservableCollection<ManagedUser>(result);
        OnPropertyChanged(nameof(FilteredUsers));
        OnPropertyChanged(nameof(FilteredCount));
    }

    // GUML: #on_clear: $controller.clear_filter
    public void ClearFilter()
    {
        FilteredUsers = new ObservableCollection<ManagedUser>(_allUsers);
        OnPropertyChanged(nameof(FilteredUsers));
        OnPropertyChanged(nameof(FilteredCount));
    }

    // GUML: $controller.select_user(user.id)
    public void SelectUser(int userId)
    {
        GD.Print($"Selected user: {userId}");
    }

    // GUML: $controller.edit_user(user.id)
    public void EditUser(int userId)
    {
        int idx = _allUsers.FindIndex(u => u.Id == userId);
        if (idx < 0) return;
        _allUsers[idx].IsActive = !_allUsers[idx].IsActive;
        OnPropertyChanged(nameof(ActiveCount));
        // Re-apply current filter
        FilterUsers("");
    }

    // GUML: $controller.can_edit(user.id)
    public bool CanEdit(int userId) =>
        _allUsers.Any(u => u.Id == userId && u.IsActive);
}

public class ManagedUser
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; }
}
