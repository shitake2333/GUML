namespace GUML.Shared.Api;

/// <summary>
/// Describes an event (signal) declared on a component type (§11.1.2).
/// </summary>
public sealed class EventDescriptor
{
    /// <summary>
    /// The event/signal name (e.g. "pressed", "text_changed").
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Documentation summary for this event.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Parameters of the event, if any.
    /// </summary>
    public List<ParameterDescriptor> Parameters { get; set; } = new();
}
