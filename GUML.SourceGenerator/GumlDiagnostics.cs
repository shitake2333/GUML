using Microsoft.CodeAnalysis;

namespace GUML.SourceGenerator;

/// <summary>
/// Contains diagnostic descriptors for GUML source generator messages.
/// These diagnostics are reported to the IDE/build output during compilation.
/// </summary>
internal static class GumlDiagnostics
{
    private const string Category = "GUML.SourceGenerator";

    /// <summary>
    /// Reported when a .guml file has a syntax error and cannot be parsed.
    /// </summary>
    public static readonly DiagnosticDescriptor ParseError = new(
        id: "GUML001",
        title: "GUML parse error",
        messageFormat: "Failed to parse GUML file '{0}': {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The .guml file contains a syntax error and cannot be compiled into a view class.");

    /// <summary>
    /// Reported when a component type referenced in .guml is not a known Godot control.
    /// </summary>
    public static readonly DiagnosticDescriptor UnknownComponent = new(
        id: "GUML002",
        title: "Unknown GUML component",
        messageFormat: "Unknown component type '{0}' in file '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The component type used in the .guml file is not recognized.");

    /// <summary>
    /// Reported when a binding expression cannot be fully resolved at compile time.
    /// The binding will be skipped in the generated code.
    /// </summary>
    public static readonly DiagnosticDescriptor UnresolvableBinding = new(
        id: "GUML003",
        title: "Unresolvable GUML binding",
        messageFormat: "Binding expression in '{0}' could not be fully resolved and will use runtime evaluation",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The binding expression references types that cannot be resolved at compile time.");

    /// <summary>
    /// Informational diagnostic emitted when a view class is successfully generated.
    /// </summary>
    public static readonly DiagnosticDescriptor GenerationSuccess = new(
        id: "GUML004",
        title: "GUML view generated",
        messageFormat: "Successfully generated view class '{0}' from '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "A strongly-typed view class was successfully generated from the .guml file.");

    /// <summary>
    /// Reported when the .guml file specified in [GumlController] is not found
    /// in AdditionalFiles or does not exist.
    /// </summary>
    public static readonly DiagnosticDescriptor GumlFileNotFound = new(
        id: "GUML005",
        title: "GUML file not found",
        messageFormat: "The .guml file '{0}' specified in [GumlController] on '{1}' was not found in AdditionalFiles",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The .guml file path specified in [GumlController] could not be matched to any AdditionalFiles entry. Ensure the file exists and is included as <AdditionalFiles> in the project.");

    /// <summary>
    /// Reported when multiple controller classes reference the same .guml file path.
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateGumlPath = new(
        id: "GUML006",
        title: "Duplicate GUML path",
        messageFormat: "Multiple controllers reference the same .guml file '{0}': {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Each .guml file can only be associated with one [GumlController]-annotated class.");

    /// <summary>
    /// Reported when a [GumlController]-annotated class is not declared as partial,
    /// preventing the source generator from injecting named node properties.
    /// </summary>
    public static readonly DiagnosticDescriptor ControllerNotPartial = new(
        id: "GUML007",
        title: "Controller not partial",
        messageFormat: "Controller '{0}' is annotated with [GumlController] but is not declared as 'partial'; named node properties cannot be generated",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "To enable strongly-typed named node property injection, the controller class must be declared as 'partial'.");
}
