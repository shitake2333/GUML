using System.Collections.Concurrent;
using Godot;

namespace GUML;

public class TypeErrorException(string msg) : Exception(msg);

/// <summary>
/// Thrown when a GUML runtime operation fails
/// (e.g., missing source-generated view).
/// </summary>
public class GumlRuntimeException(string msg) : Exception(msg);

public enum ThemeValueType
{
    Constant,
    Color,
    Font,
    FontSize,
    Icon,
    Style
}

public static class Guml
{
    /// <summary>
    /// The string provider for i18n translation in GUML binding expressions.
    /// Assign an implementation before loading any GUML files that use
    /// <c>tr()</c> or <c>ntr()</c> expressions. When <see langword="null"/>,
    /// the source-language string is returned as-is (no-op fallback).
    /// </summary>
    public static IStringProvider? StringProvider { get; set; }

    public static IResourceProvider ResourceProvider
    {
        get => field ?? throw new InvalidOperationException(
            "Guml.ResourceProvider must be set before loading GUML files that use resource functions. " +
            "Assign a DefaultResourceProvider or custom implementation in your project's initialization.");
        set;
    }

    /// <summary>
    /// Registry of source-generated view factories keyed by controller type.
    /// Populated automatically by [ModuleInitializer] in generated View classes.
    /// Enables type-safe loading via <see cref="Load{T}"/>.
    /// </summary>
    public static readonly ConcurrentDictionary<Type, Func<Node, GuiController>> ControllerRegistry = new();

    /// <summary>
    /// Loads a GUML view by controller type.
    /// The controller must have a registered view factory
    /// (populated by [ModuleInitializer] in source-generated code).
    /// </summary>
    /// <typeparam name="T">The controller type annotated with [GumlController].</typeparam>
    /// <param name="root">The parent node to attach the generated UI tree to.</param>
    /// <returns>The created controller instance.</returns>
    public static T Load<T>(Node root) where T : GuiController
    {
        var controllerType = typeof(T);
        if (ControllerRegistry.TryGetValue(controllerType, out var factory))
        {
            return (T)factory(root);
        }

        throw new GumlRuntimeException(
            $"No source-generated view found for controller '{controllerType.Name}'. " +
            "Ensure the controller has a [GumlController] attribute.");
    }

}
