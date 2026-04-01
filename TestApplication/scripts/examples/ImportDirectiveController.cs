using GUML;
using GUML.Shared;

namespace TestApplication.scripts.examples;

/// <summary>
/// 06 - Import Directive.
/// Shows imported component usage with visible action feedback.
/// </summary>
[GumlController("res://gui/examples/06_import_directive.guml")]
public partial class ImportDirectiveController : GuiController
{
    // GUML: $controller.last_action  (shown in output panel)
    public string LastAction { get; private set; } = "(no action yet)";

    // GUML: $controller.action_count
    public int ActionCount { get; private set; }

    private void _RecordAction(string description)
    {
        LastAction = description;
        ActionCount++;
        OnPropertyChanged(nameof(LastAction));
        OnPropertyChanged(nameof(ActionCount));
    }

    // GUML: #on_clicked: $controller.on_primary_action
    public void OnPrimaryAction() => _RecordAction("Primary Action clicked");

    // GUML: #on_clicked: $controller.on_secondary_action
    public void OnSecondaryAction() => _RecordAction("Secondary Action clicked");

    // GUML: #on_search: $controller.handle_search
    public void HandleSearch(string query) => _RecordAction($"Search: \"{query}\"");

    // GUML: #on_clear: $controller.clear_search
    public void ClearSearch() => _RecordAction("Search cleared");
}
