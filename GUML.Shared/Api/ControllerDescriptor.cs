namespace GUML.Shared.Api;

/// <summary>
/// Describes a controller class discovered from [GumlController] usage (§11.1.2).
/// </summary>
public sealed class ControllerDescriptor
{
    /// <summary>
    /// The fully qualified controller type name (e.g. "MyApp.MainController").
    /// </summary>
    public string FullName { get; set; } = "";

    /// <summary>
    /// The simple class name (e.g. "MainController").
    /// </summary>
    public string SimpleName { get; set; } = "";

    /// <summary>
    /// The associated GUML file path (e.g. "res://gui/main.guml").
    /// </summary>
    public string GumlPath { get; set; } = "";

    /// <summary>
    /// Public properties exposed by the controller.
    /// </summary>
    public List<ParameterDescriptor> Properties { get; set; } = new();

    /// <summary>
    /// Public method names exposed by the controller.
    /// </summary>
    public List<MethodDescriptor> Methods { get; set; } = new();

    /// <summary>
    /// Whether the controller type supports reactive change notifications
    /// (e.g. implements <c>INotifyPropertyChanged</c>).
    /// </summary>
    public bool IsReactive { get; set; }

    /// <summary>
    /// The absolute file-system path of the C# source file that declares this controller.
    /// </summary>
    public string? SourceFile { get; set; }

    /// <summary>
    /// The 0-based line number of the controller class declaration in <see cref="SourceFile"/>.
    /// </summary>
    public int SourceLine { get; set; }

    /// <summary>
    /// Per-member 0-based source positions within <see cref="SourceFile"/>,
    /// keyed by member name (property, field, or method).
    /// </summary>
    public Dictionary<string, SourcePosition> MemberSourceLines { get; set; } = new();

    /// <summary>
    /// The XML documentation summary of the controller class, if any.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Per-member documentation summaries, keyed by member name.
    /// </summary>
    public Dictionary<string, string> MemberDescriptions { get; set; } = new();
}
