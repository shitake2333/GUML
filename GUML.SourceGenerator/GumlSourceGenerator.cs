using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

[assembly: InternalsVisibleTo("GUML.SourceGenerator.Tests")]

namespace GUML.SourceGenerator;

/// <summary>
/// Information about a controller class annotated with [GumlController].
/// </summary>
internal readonly struct ControllerInfo : IEquatable<ControllerInfo>
{
    /// <summary>The resolved absolute path to the .guml file.</summary>
    public string AbsoluteGumlPath { get; }

    /// <summary>The simple class name of the controller (e.g., "MainController").</summary>
    public string ControllerSimpleName { get; }

    /// <summary>The namespace of the controller class, or null for global namespace.</summary>
    public string? ControllerNamespace { get; }

    /// <summary>Whether the controller class is declared as partial.</summary>
    public bool IsPartial { get; }

    /// <summary>The location of the attribute for diagnostics.</summary>
    public Location AttributeLocation { get; }

    public ControllerInfo(string absoluteGumlPath, string controllerSimpleName,
        string? controllerNamespace, bool isPartial, Location attributeLocation)
    {
        AbsoluteGumlPath = absoluteGumlPath;
        ControllerSimpleName = controllerSimpleName;
        ControllerNamespace = controllerNamespace;
        IsPartial = isPartial;
        AttributeLocation = attributeLocation;
    }

    public bool Equals(ControllerInfo other) =>
        AbsoluteGumlPath == other.AbsoluteGumlPath &&
        ControllerSimpleName == other.ControllerSimpleName &&
        ControllerNamespace == other.ControllerNamespace &&
        IsPartial == other.IsPartial;

    public override bool Equals(object? obj) => obj is ControllerInfo other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = AbsoluteGumlPath.GetHashCode();
            hash = hash * 31 + ControllerSimpleName.GetHashCode();
            hash = hash * 31 + (ControllerNamespace?.GetHashCode() ?? 0);
            hash = hash * 31 + IsPartial.GetHashCode();
            return hash;
        }
    }
}

/// <summary>
/// Roslyn incremental source generator that compiles .guml files
/// into strongly-typed C# view classes at build time.
/// </summary>
/// <remarks>
/// <para>
/// The pipeline is driven by [GumlController] attributes on controller classes.
/// For each annotated controller, it resolves the referenced .guml file from AdditionalFiles,
/// parses it, and emits:
/// 1. A View class ({Name}GumlView.g.cs) with Build() method and [ModuleInitializer] Register()
/// 2. A Controller partial class ({Name}.NamedNodes.g.cs) with strongly-typed named node properties
/// </para>
/// <para>
/// To use, add the following to your .csproj:
/// <code>
/// &lt;ItemGroup&gt;
///   &lt;AdditionalFiles Include="gui\**\*.guml" /&gt;
/// &lt;/ItemGroup&gt;
/// </code>
/// And annotate controller classes:
/// <code>
/// [GumlController("../../gui/main.guml")]
/// public partial class MainController : GuiController { }
/// </code>
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public class GumlSourceGenerator : IIncrementalGenerator
{
    private const string GumlControllerAttributeFullName = "GUML.Shared.GumlControllerAttribute";

    /// <summary>
    /// Registers the incremental generation pipeline:
    /// scan [GumlController] attributes → match with .guml AdditionalFiles → emit C# source.
    /// </summary>
    /// <param name="context">The initialization context provided by the Roslyn compiler.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Collect controller info from [GumlController] attributes
        var controllers = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                GumlControllerAttributeFullName,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, ct) => ExtractControllerInfo(ctx))
            .Where(static info => info.HasValue)
            .Select(static (info, _) => info!.Value);

        // 2. Collect all .guml AdditionalFiles
        var gumlFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".guml", StringComparison.OrdinalIgnoreCase))
            .Collect();

        // 3. Read GumlNamespaces MSBuild property
        var namespaces = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) =>
            {
                provider.GlobalOptions.TryGetValue("build_property.GumlNamespaces", out string? value);
                return value ?? "";
            });

        // 3b. Read project directory for computing relative registry keys
        var projectDir = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) =>
            {
                provider.GlobalOptions.TryGetValue("build_property.GumlProjectDir", out string? value);
                return value ?? "";
            });

        // 4. Combine each controller with AdditionalFiles, namespaces, projectDir, and Compilation
        var combined = controllers
            .Combine(gumlFiles)
            .Combine(namespaces)
            .Combine(projectDir)
            .Combine(context.CompilationProvider);

        // 5. Register source output: for each controller, find its .guml and generate code
        context.RegisterSourceOutput(combined, static (spc, item) =>
        {
            ((((ControllerInfo controllerInfo, ImmutableArray<AdditionalText> gumlFiles), string? ns), string? projDir), Compilation? compilation) = item;
            string[] additionalNamespaces = ParseNamespaces(ns);
            ExecuteForController(spc, controllerInfo, gumlFiles, additionalNamespaces, compilation, projDir);
        });

        // 6. Detect duplicate .guml path references across controllers
        var allControllers = controllers.Collect();
        context.RegisterSourceOutput(allControllers, static (spc, controllers) =>
        {
            var pathGroups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var ctrl in controllers)
            {
                string normalizedPath = ctrl.AbsoluteGumlPath.Replace('\\', '/').ToLowerInvariant();
                if (!pathGroups.TryGetValue(normalizedPath, out var list))
                {
                    list = new List<string>();
                    pathGroups[normalizedPath] = list;
                }
                list.Add(ctrl.ControllerSimpleName);
            }

            foreach (var group in pathGroups)
            {
                if (group.Value.Count > 1)
                {
                    string controllers_ = string.Join(", ", group.Value);
                    foreach (var ctrl in controllers)
                    {
                        string normalized = ctrl.AbsoluteGumlPath.Replace('\\', '/').ToLowerInvariant();
                        if (string.Equals(normalized, group.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            spc.ReportDiagnostic(Diagnostic.Create(
                                GumlDiagnostics.DuplicateGumlPath,
                                ctrl.AttributeLocation,
                                group.Key,
                                controllers_));
                        }
                    }
                }
            }
        });
    }

    /// <summary>
    /// Extracts controller information from a [GumlController] attribute application.
    /// </summary>
    private static ControllerInfo? ExtractControllerInfo(GeneratorAttributeSyntaxContext ctx)
    {
        var classDecl = (ClassDeclarationSyntax)ctx.TargetNode;
        var symbol = ctx.TargetSymbol as INamedTypeSymbol;
        if (symbol == null) return null;

        // Get the GumlPath from the attribute constructor argument
        string? gumlPath = null;
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() == GumlControllerAttributeFullName)
            {
                if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string path)
                {
                    gumlPath = path;
                }
                break;
            }
        }

        if (string.IsNullOrEmpty(gumlPath)) return null;

        // Resolve relative path based on the source file location
        string? sourceFilePath = ctx.TargetNode.SyntaxTree.FilePath;
        if (string.IsNullOrEmpty(sourceFilePath)) return null;

        string sourceDir = Path.GetDirectoryName(sourceFilePath) ?? "";
        string absoluteGumlPath;
        try
        {
            absoluteGumlPath = Path.GetFullPath(Path.Combine(sourceDir, gumlPath!));
        }
        catch
        {
            return null;
        }

        // Check if the class is partial
        bool isPartial = classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));

        // Get namespace
        string? controllerNamespace = symbol.ContainingNamespace?.IsGlobalNamespace == true
            ? null
            : symbol.ContainingNamespace?.ToDisplayString();

        // Get attribute location for diagnostics
        var attrLocation = ctx.Attributes[0].ApplicationSyntaxReference?.GetSyntax()?.GetLocation()
            ?? classDecl.GetLocation();

        return new ControllerInfo(
            absoluteGumlPath,
            symbol.Name,
            controllerNamespace,
            isPartial,
            attrLocation);
    }

    /// <summary>
    /// Executes code generation for a single [GumlController]-annotated controller.
    /// </summary>
    private static void ExecuteForController(
        SourceProductionContext context,
        ControllerInfo controller,
        ImmutableArray<AdditionalText> gumlFiles,
        IReadOnlyList<string> additionalNamespaces,
        Compilation compilation,
        string projectDir)
    {
        // Find matching .guml file in AdditionalFiles
        AdditionalText? matchedFile = null;
        foreach (var file in gumlFiles)
        {
            try
            {
                string normalizedAdditional = Path.GetFullPath(file.Path);
                if (string.Equals(normalizedAdditional, controller.AbsoluteGumlPath,
                    StringComparison.OrdinalIgnoreCase))
                {
                    matchedFile = file;
                    break;
                }
            }
            catch
            {
                // Path normalization failed, try next
            }
        }

        if (matchedFile == null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                GumlDiagnostics.GumlFileNotFound,
                controller.AttributeLocation,
                controller.AbsoluteGumlPath,
                controller.ControllerSimpleName));
            return;
        }

        string gumlText = matchedFile.GetText()?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(gumlText)) return;

        string filePath = matchedFile.Path;
        string fileName = Path.GetFileNameWithoutExtension(filePath);

        try
        {
            // Parse the .guml content
            var parser = new GumlParser();
            parser.WithConverter(new KeyConverter());
            var doc = parser.Parse(gumlText);

            // Create type scanner for zero-reflection binding
            var scanner = new CompilationApiScanner(compilation);

            // Compute normalized registry key for ViewRegistry (relative to project directory)
            string registryKey = NormalizeRegistryKey(matchedFile.Path, projectDir);

            // Emit View class code (with controller type name and registry key)
            string code = GumlCodeEmitter.Emit(
                filePath, doc, additionalNamespaces, scanner,
                controller.ControllerSimpleName, registryKey);
            string viewHintName = KeyConverter.ToPascalCase(fileName) + "GumlView.g.cs";
            context.AddSource(viewHintName, SourceText.From(code, Encoding.UTF8));

            // Emit Controller partial class for named node and import controller properties
            if (doc.LocalAlias.Count > 0 || doc.Imports.Count > 0)
            {
                if (controller.IsPartial)
                {
                    // Look up existing members to avoid generating duplicate properties
                    ISet<string>? existingMembers = null;
                    string metadataName = string.IsNullOrEmpty(controller.ControllerNamespace)
                        ? controller.ControllerSimpleName
                        : $"{controller.ControllerNamespace}.{controller.ControllerSimpleName}";
                    var typeSymbol = compilation.GetTypeByMetadataName(metadataName);
                    if (typeSymbol != null)
                    {
                        existingMembers = new HashSet<string>(
                            typeSymbol.GetMembers().Select(m => m.Name));
                    }

                    string? partialCode = GumlCodeEmitter.EmitControllerPartial(
                        controller.ControllerSimpleName,
                        controller.ControllerNamespace,
                        doc,
                        existingMembers);
                    if (partialCode != null)
                    {
                        string partialHintName = controller.ControllerSimpleName + ".NamedNodes.g.cs";
                        context.AddSource(partialHintName, SourceText.From(partialCode, Encoding.UTF8));
                    }
                }
                else
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        GumlDiagnostics.ControllerNotPartial,
                        controller.AttributeLocation,
                        controller.ControllerSimpleName));
                }
            }

            // Report success diagnostic
            string className = KeyConverter.ToPascalCase(fileName) + "GumlView";
            context.ReportDiagnostic(Diagnostic.Create(
                GumlDiagnostics.GenerationSuccess,
                Location.None,
                className,
                fileName + ".guml"));
        }
        catch (GumlParserException ex)
        {
            var lineSpan = new LinePositionSpan(
                new LinePosition(Math.Max(0, ex.Line - 1), Math.Max(0, ex.Column - 1)),
                new LinePosition(Math.Max(0, ex.Line - 1), Math.Max(0, ex.Column)));

            var location = Location.Create(
                filePath,
                TextSpan.FromBounds(ex.StartIndex, ex.StartIndex + ex.Length),
                lineSpan);

            context.ReportDiagnostic(Diagnostic.Create(
                GumlDiagnostics.ParseError,
                location,
                fileName + ".guml",
                ex.Message));
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                GumlDiagnostics.ParseError,
                Location.None,
                fileName + ".guml",
                $"Unexpected error: {ex.Message}"));
        }
    }

    /// <summary>
    /// Normalizes a file path for use as a ViewRegistry key.
    /// Computes a path relative to the project directory and uses forward slashes + lowercase
    /// for consistent cross-platform matching with the runtime NormalizePath.
    /// </summary>
    internal static string NormalizeRegistryKey(string filePath, string projectDir)
    {
        // Compute relative path from project directory
        if (!string.IsNullOrEmpty(projectDir))
        {
            try
            {
                string fullPath = Path.GetFullPath(filePath);
                string fullProjectDir = Path.GetFullPath(projectDir);
                if (!fullProjectDir.EndsWith(Path.DirectorySeparatorChar.ToString())
                    && !fullProjectDir.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
                {
                    fullProjectDir += Path.DirectorySeparatorChar;
                }

                if (fullPath.StartsWith(fullProjectDir, StringComparison.OrdinalIgnoreCase))
                {
                    string relativePath = fullPath.Substring(fullProjectDir.Length);
                    return relativePath.Replace('\\', '/').ToLowerInvariant();
                }
            }
            catch
            {
                // Fall through to full path normalization
            }
        }

        return filePath.Replace('\\', '/').ToLowerInvariant();
    }

    /// <summary>
    /// Parses a semicolon-separated namespace string into a list of non-empty namespace entries.
    /// </summary>
    /// <param name="raw">The raw semicolon-separated value from the GumlNamespaces MSBuild property.</param>
    /// <returns>A list of trimmed, non-empty namespace strings.</returns>
    internal static string[] ParseNamespaces(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        return raw.Split(';')
            .Select(static s => s.Trim())
            .Where(static s => s.Length > 0)
            .ToArray();
    }

}
