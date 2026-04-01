namespace GUML.Shared.Api;

/// <summary>
/// Describes a method or delegate-typed member on a controller class (§11.1.2).
/// </summary>
public sealed class MethodDescriptor
{
    /// <summary>
    /// The method or member name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// The return type of the method (e.g. "void", "string").
    /// </summary>
    public string ReturnType { get; set; } = "void";

    /// <summary>
    /// The parameter list of the method.
    /// </summary>
    public List<ParameterDescriptor> Parameters { get; set; } = new();

    /// <summary>
    /// The XML documentation summary, if any.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this member is a delegate-typed property or field
    /// (e.g. <c>Action</c>, <c>Func&lt;T&gt;</c>) rather than a regular method.
    /// </summary>
    public bool IsDelegate { get; set; }
}
