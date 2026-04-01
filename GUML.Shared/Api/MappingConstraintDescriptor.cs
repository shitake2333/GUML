namespace GUML.Shared.Api;

/// <summary>
/// Directional capability metadata for property mapping operators (§11.1.3).
/// </summary>
public sealed class MappingConstraintDescriptor
{
    /// <summary>
    /// Whether the property supports static assignment (:).
    /// </summary>
    public bool CanStaticMap { get; set; }

    /// <summary>
    /// Whether the property supports data-to-property binding (:=).
    /// </summary>
    public bool CanBindDataToProperty { get; set; }

    /// <summary>
    /// Whether the property supports property-to-data binding (=:).
    /// </summary>
    public bool CanBindPropertyToData { get; set; }

    /// <summary>
    /// Whether the property supports two-way binding (&lt;=&gt;).
    /// </summary>
    public bool CanBindTwoWay { get; set; }

    /// <summary>
    /// Whether the property supports change notification observation.
    /// </summary>
    public bool IsObservableProperty { get; set; }

    /// <summary>
    /// The source of change notifications for this property.
    /// </summary>
    public ObservabilitySource ObservabilitySource { get; set; }
}

/// <summary>
/// Classifies the source of property change notifications.
/// </summary>
public enum ObservabilitySource
{
    /// <summary>No change notification support.</summary>
    None,

    /// <summary>INotifyPropertyChanged-based notifications.</summary>
    NotifyPropertyChanged,

    /// <summary>Godot signal-based notifications.</summary>
    Signal,

    /// <summary>Custom notification mechanism.</summary>
    Custom
}
