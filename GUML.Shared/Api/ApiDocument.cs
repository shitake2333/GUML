namespace GUML.Shared.Api;

/// <summary>
/// Root model for the GUML API description contract (§11.1.2).
/// Contains metadata about the scanned SDK and all discovered type/controller definitions.
/// </summary>
public sealed class ApiDocument
{
    /// <summary>
    /// Schema version identifier for forward/backward compatibility.
    /// </summary>
    public string SchemaVersion { get; set; } = "";

    /// <summary>
    /// Timestamp when this document was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// The SDK version string (e.g. "4.6.1"), or null if unknown.
    /// </summary>
    public string? SdkVersion { get; set; }

    /// <summary>
    /// Dictionary of qualified type name → type descriptor for all discovered component types.
    /// </summary>
    public Dictionary<string, TypeDescriptor> Types { get; set; } = new();

    /// <summary>
    /// Dictionary of qualified controller name → controller descriptor.
    /// </summary>
    public Dictionary<string, ControllerDescriptor> Controllers { get; set; } = new();
}
