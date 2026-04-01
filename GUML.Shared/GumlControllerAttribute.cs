namespace GUML.Shared;

/// <summary>
/// Marks a controller class and associates it with a specific .guml file.
/// The source generator uses this attribute to:
/// 1. Resolve the controller type for the generated view class.
/// 2. Generate a partial class with strongly-typed named node properties.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class GumlControllerAttribute : Attribute
{
    /// <summary>
    /// The path to the .guml file, relative to the source file
    /// containing this controller class.
    /// </summary>
    public string GumlPath { get; }

    /// <param name="gumlPath">
    /// Path to the .guml file, relative to the directory of the source file
    /// where this attribute is applied.
    /// Example: "../../gui/main.guml" or "../gui/setting.guml"
    /// </param>
    public GumlControllerAttribute(string gumlPath)
    {
        GumlPath = gumlPath;
    }
}
