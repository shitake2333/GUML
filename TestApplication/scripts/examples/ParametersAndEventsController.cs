using GUML;
using GUML.Shared;

namespace TestApplication.scripts.examples;

/// <summary>
/// 07 - Parameters &amp; Events.
/// Manages the internal state of the component: counter and status feedback.
/// </summary>
[GumlController("res://gui/examples/07_parameters_and_events.guml")]
public partial class ParametersAndEventsController : GuiController
{
    // GUML: $controller.item_count  (internal counter)
    public int ItemCount { get; private set; }

    // GUML: $controller.status  (shown after submit/cancel/value change)
    public string Status { get; private set; } = "Ready";

    // GUML: #pressed: $controller.increment
    public void Increment()
    {
        ItemCount++;
        OnPropertyChanged(nameof(ItemCount));
    }

    // GUML: #pressed: $controller.decrement
    public void Decrement()
    {
        if (ItemCount > 0) ItemCount--;
        OnPropertyChanged(nameof(ItemCount));
    }

    // GUML: #text_changed: $controller.on_value_changed
    public void OnValueChanged(string newValue)
    {
        Status = $"Changed: \"{newValue}\"";
        OnPropertyChanged(nameof(Status));
    }

    // GUML: #pressed: $controller.on_submit_pressed
    public void OnSubmitPressed()
    {
        Status = $"Submitted (count={ItemCount})";
        OnPropertyChanged(nameof(Status));
    }

    // GUML: #pressed: $controller.on_cancel_pressed
    public void OnCancelPressed()
    {
        Status = "Cancelled";
        ItemCount = 0;
        OnPropertyChanged(nameof(ItemCount));
        OnPropertyChanged(nameof(Status));
    }
}
