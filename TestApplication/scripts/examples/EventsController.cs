using GUML;
using GUML.Shared;

namespace TestApplication.scripts.examples;

/// <summary>
/// 05 - Events.
/// All event handlers update visible state so results can be observed live.
/// </summary>
[GumlController("res://gui/examples/05_events.guml")]
public partial class EventsController : GuiController
{
    // GUML: $controller.last_event  (shown in event log label)
    public string LastEvent { get; private set; } = "(no events yet)";

    // GUML: $controller.press_count  (shown on button text)
    public int PressCount { get; private set; }

    // GUML: $controller.typed_text  (shown below LineEdit)
    public string TypedText { get; private set; } = "";

    // GUML: $controller.search_query
    public string SearchQuery { get; private set; } = "";

    // GUML: $controller.selected_id  (used in confirm_delete call argument)
    public int SelectedId { get; set; } = 1;

    // GUML: #pressed: $controller.on_button_pressed
    public void OnButtonPressed()
    {
        PressCount++;
        LastEvent = $"pressed \u00d7{PressCount}";
        OnPropertyChanged(nameof(PressCount));
        OnPropertyChanged(nameof(LastEvent));
    }

    // GUML: #toggled: $controller.on_toggle_changed
    public void OnToggleChanged(bool toggled)
    {
        LastEvent = $"toggled \u2192 {(toggled ? "ON" : "OFF")}";
        OnPropertyChanged(nameof(LastEvent));
    }

    // GUML: #pressed: $controller.on_submit
    public void OnSubmit()
    {
        LastEvent = "submit pressed";
        OnPropertyChanged(nameof(LastEvent));
    }

    // GUML: #button_down: $controller.on_press_start
    public void OnPressStart()
    {
        LastEvent = "button_down";
        OnPropertyChanged(nameof(LastEvent));
    }

    // GUML: #button_up: $controller.on_press_end
    public void OnPressEnd()
    {
        LastEvent = "button_up";
        OnPropertyChanged(nameof(LastEvent));
    }

    // GUML: #text_changed: $controller.on_text_changed
    public void OnTextChanged(string newText)
    {
        TypedText = newText;
        LastEvent = $"text_changed: \"{newText}\"";
        OnPropertyChanged(nameof(TypedText));
        OnPropertyChanged(nameof(LastEvent));
    }

    // GUML: #text_submitted: $controller.on_text_submitted
    public void OnTextSubmitted(string text)
    {
        LastEvent = $"text_submitted: \"{text}\"";
        OnPropertyChanged(nameof(LastEvent));
    }

    // GUML: #text_changed: $controller.on_search  (on @search_input alias)
    public void OnSearch(string query)
    {
        SearchQuery = query;
        LastEvent = $"search: \"{query}\"";
        OnPropertyChanged(nameof(SearchQuery));
        OnPropertyChanged(nameof(LastEvent));
    }

    // GUML: $controller.confirm_delete($controller.selected_id)
    public void ConfirmDelete(int id)
    {
        LastEvent = $"delete item #{id} confirmed";
        SelectedId = id + 1;
        OnPropertyChanged(nameof(LastEvent));
        OnPropertyChanged(nameof(SelectedId));
    }
}
