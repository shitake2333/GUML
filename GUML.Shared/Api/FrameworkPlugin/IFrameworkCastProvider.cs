namespace GUML.Shared.Api.FrameworkPlugin;

/// <summary>
/// Provides framework-specific property value cast and conversion expressions
/// for the source generator's binding and assignment code.
/// </summary>
public interface IFrameworkCastProvider
{
    /// <summary>
    /// Returns the cast or conversion prefix expression for the given property type,
    /// or <c>null</c> if the type is unknown (which triggers a <c>dynamic</c>-dispatch fallback).
    /// </summary>
    /// <param name="fullyQualifiedTypeName">
    /// The fully-qualified CLR type name of the property
    /// (e.g. <c>"Godot.Vector2"</c>, <c>"System.Int32"</c>, <c>"Godot.NodePath"</c>).
    /// </param>
    /// <param name="isEnum">Whether the type is an enum.</param>
    /// <param name="isValueType">Whether the type is a value type (struct).</param>
    /// <param name="baseTypeFullNames">
    /// The fully-qualified CLR type names (e.g. <c>"Godot.Node"</c>, no <c>global::</c> prefix)
    /// of every base class in the inheritance chain of the target type, from direct parent up
    /// to (but not including) <c>System.Object</c>. Used to detect subtypes of framework root
    /// classes (e.g. <c>Godot.Node</c>, <c>Godot.Resource</c>).
    /// </param>
    /// <returns>
    /// <para>
    /// One of:
    /// <list type="bullet">
    /// <item>A C-style cast prefix such as <c>"(Vector2)"</c> — used as <c>(Vector2)valExpr</c>.</item>
    /// <item>A conversion method such as <c>"Convert.ToInt32"</c> — used as <c>Convert.ToInt32(valExpr)</c>.</item>
    /// <item><c>null</c> to indicate the type is unresolvable (emits <c>(dynamic)</c> fallback).</item>
    /// </list>
    /// </para>
    /// </returns>
    string? GetCastExpression(string fullyQualifiedTypeName, bool isEnum, bool isValueType,
        IReadOnlyList<string> baseTypeFullNames);
}
