using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using GUML.Shared.Converter;
using GUML.Shared.Syntax;
using GUML.Shared.Syntax.Nodes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using CSharpSyntaxKind = Microsoft.CodeAnalysis.CSharp.SyntaxKind;
using RoslynDiagnostic = Microsoft.CodeAnalysis.Diagnostic;

[assembly: InternalsVisibleTo("GUML.SourceGenerator.Tests")]

namespace GUML.SourceGenerator;

/// <summary>
/// Information about a controller class annotated with [GumlController].
/// </summary>
internal readonly struct ControllerInfo : IEquatable<ControllerInfo>
{
    /// <summary>The raw path string from [GumlController("...")].</summary>
    public string RawGumlPath { get; }

    /// <summary>Directory of the controller source file, for relative path resolution.</summary>
    public string SourceFileDir { get; }

    /// <summary>The simple class name of the controller (e.g., "MainController").</summary>
    public string ControllerSimpleName { get; }

    /// <summary>The namespace of the controller class, or null for global namespace.</summary>
    public string? ControllerNamespace { get; }

    /// <summary>Whether the controller class is declared as partial.</summary>
    public bool IsPartial { get; }

    /// <summary>The location of the attribute for diagnostics.</summary>
    public Location AttributeLocation { get; }

    public ControllerInfo(string rawGumlPath, string sourceFileDir, string controllerSimpleName,
        string? controllerNamespace, bool isPartial, Location attributeLocation)
    {
        RawGumlPath = rawGumlPath;
        SourceFileDir = sourceFileDir;
        ControllerSimpleName = controllerSimpleName;
        ControllerNamespace = controllerNamespace;
        IsPartial = isPartial;
        AttributeLocation = attributeLocation;
    }

    public bool Equals(ControllerInfo other) =>
        RawGumlPath == other.RawGumlPath &&
        SourceFileDir == other.SourceFileDir &&
        ControllerSimpleName == other.ControllerSimpleName &&
        ControllerNamespace == other.ControllerNamespace &&
        IsPartial == other.IsPartial;

    public override bool Equals(object? obj) => obj is ControllerInfo other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = RawGumlPath.GetHashCode();
            hash = hash * 31 + SourceFileDir.GetHashCode();
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
                transform: static (ctx, _) => ExtractControllerInfo(ctx))
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
        var allControllersWithProjectDir = allControllers.Combine(projectDir);
        context.RegisterSourceOutput(allControllersWithProjectDir, static (spc, item) =>
        {
            (ImmutableArray<ControllerInfo> controllers, string? projDir) = item;
            var pathGroups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var ctrl in controllers)
            {
                string? resolved = ResolveGumlPath(ctrl.RawGumlPath, ctrl.SourceFileDir, projDir ?? "");
                if (resolved == null) continue;
                string normalizedPath = resolved.Replace('\\', '/').ToLowerInvariant();
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
                    string controllerNames = string.Join(", ", group.Value);
                    foreach (var ctrl in controllers)
                    {
                        string? resolved = ResolveGumlPath(ctrl.RawGumlPath, ctrl.SourceFileDir, projDir ?? "");
                        if (resolved == null) continue;
                        string normalized = resolved.Replace('\\', '/').ToLowerInvariant();
                        if (string.Equals(normalized, group.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            spc.ReportDiagnostic(RoslynDiagnostic.Create(
                                GumlDiagnostics.DuplicateGumlPath,
                                ctrl.AttributeLocation,
                                group.Key,
                                controllerNames));
                        }
                    }
                }
            }
        });
    }

    /// <summary>
    /// Extracts controller information from a [GumlController] attribute application.
    /// Stores the raw path; actual resolution is deferred to ExecuteForController
    /// where projectDir is available.
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

        string sourceFilePath = ctx.TargetNode.SyntaxTree.FilePath;
        if (string.IsNullOrEmpty(sourceFilePath)) return null;

        string sourceDir = Path.GetDirectoryName(sourceFilePath) ?? "";

        // Check if the class is partial
        bool isPartial = classDecl.Modifiers.Any(m => m.IsKind(CSharpSyntaxKind.PartialKeyword));

        // Get namespace
        string? controllerNamespace = symbol.ContainingNamespace?.IsGlobalNamespace == true
            ? null
            : symbol.ContainingNamespace?.ToDisplayString();

        // Get attribute location for diagnostics
        var attrLocation = ctx.Attributes[0].ApplicationSyntaxReference?.GetSyntax().GetLocation()
            ?? classDecl.GetLocation();

        return new ControllerInfo(
            gumlPath!,
            sourceDir,
            symbol.Name,
            controllerNamespace,
            isPartial,
            attrLocation);
    }

    /// <summary>
    /// Resolves a GUML path from [GumlController] to an absolute file system path.
    /// Supports: res:// paths, relative paths, project-root-relative paths, absolute paths.
    /// </summary>
    /// <param name="rawPath">The raw path from the attribute.</param>
    /// <param name="sourceFileDir">Directory of the controller source file.</param>
    /// <param name="projectDir">The project root directory (GumlProjectDir MSBuild property).</param>
    /// <returns>The resolved absolute path, or null if resolution fails.</returns>
    internal static string? ResolveGumlPath(string rawPath, string sourceFileDir, string projectDir)
    {
        try
        {
            // 1. res:// Godot resource path → resolve relative to project directory
            if (rawPath.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
            {
                string relativePart = rawPath.Substring("res://".Length);
                if (!string.IsNullOrEmpty(projectDir))
                    return Path.GetFullPath(Path.Combine(projectDir, relativePart));
                return null;
            }

            // 2. Full absolute path
            if (Path.IsPathRooted(rawPath))
                return Path.GetFullPath(rawPath);

            // 3. Relative path with ../ or ./ → relative to source file
            if (rawPath.StartsWith("../") || rawPath.StartsWith("./")
                || rawPath.StartsWith("..\\") || rawPath.StartsWith(".\\"))
            {
                return Path.GetFullPath(Path.Combine(sourceFileDir, rawPath));
            }

            // 4. Project-root-relative path (e.g. "gui/test.guml")
            if (!string.IsNullOrEmpty(projectDir))
                return Path.GetFullPath(Path.Combine(projectDir, rawPath));

            // 5. Fallback: relative to source file
            return Path.GetFullPath(Path.Combine(sourceFileDir, rawPath));
        }
        catch
        {
            return null;
        }
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
        // Resolve the raw path to an absolute path
        string? absoluteGumlPath = ResolveGumlPath(controller.RawGumlPath, controller.SourceFileDir, projectDir);
        if (absoluteGumlPath == null)
        {
            context.ReportDiagnostic(RoslynDiagnostic.Create(
                GumlDiagnostics.GumlFileNotFound,
                controller.AttributeLocation,
                controller.RawGumlPath,
                controller.ControllerSimpleName));
            return;
        }

        // Find matching .guml file in AdditionalFiles
        AdditionalText? matchedFile = null;
        foreach (var file in gumlFiles)
        {
            try
            {
                string normalizedAdditional = Path.GetFullPath(file.Path);
                if (string.Equals(normalizedAdditional, absoluteGumlPath,
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
            context.ReportDiagnostic(RoslynDiagnostic.Create(
                GumlDiagnostics.GumlFileNotFound,
                controller.AttributeLocation,
                absoluteGumlPath,
                controller.ControllerSimpleName));
            return;
        }

        string gumlText = matchedFile.GetText()?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(gumlText)) return;

        string filePath = matchedFile.Path;
        string fileName = Path.GetFileNameWithoutExtension(filePath);

        try
        {
            // Parse the .guml content using CST parser (error-tolerant, never throws)
            var parseResult = GumlSyntaxTree.Parse(gumlText);
            var doc = parseResult.Root;

            // Report any parse diagnostics
            foreach (var diag in parseResult.Diagnostics)
            {
                if (diag.Severity == Shared.Syntax.DiagnosticSeverity.Error)
                {
                    // Compute line/column from character offset
                    int line = 0, col = 0;
                    for (int i = 0; i < diag.Span.Start && i < gumlText.Length; i++)
                    {
                        if (gumlText[i] == '\n') { line++; col = 0; }
                        else { col++; }
                    }

                    var lineSpan = new LinePositionSpan(
                        new LinePosition(line, col),
                        new LinePosition(line, col));

                    var location = Location.Create(
                        filePath,
                        Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(
                            diag.Span.Start,
                            diag.Span.Start + diag.Span.Length),
                        lineSpan);

                    context.ReportDiagnostic(RoslynDiagnostic.Create(
                        GumlDiagnostics.ParseError,
                        location,
                        fileName + ".guml",
                        diag.Message));
                }
            }

            // Create type scanner for zero-reflection binding
            var scanner = new CompilationApiScanner(compilation);

            // Build an ImportResolver that resolves relative import paths
            // by looking up parsed .guml files from AdditionalTexts.
            GumlDocumentSyntax? ResolveImport(string importPath)
            {
                string dir = Path.GetDirectoryName(filePath) ?? "";
                string resolved;
                try { resolved = Path.GetFullPath(Path.Combine(dir, importPath)); }
                catch { return null; }

                foreach (var f in gumlFiles)
                {
                    try
                    {
                        if (string.Equals(Path.GetFullPath(f.Path), resolved, StringComparison.OrdinalIgnoreCase))
                        {
                            string importText = f.GetText()?.ToString() ?? "";
                            if (string.IsNullOrWhiteSpace(importText)) return null;
                            return GumlSyntaxTree.Parse(importText).Root;
                        }
                    }
                    catch { /* skip */ }
                }
                return null;
            }

            // Emit View class code (with controller type name and registry key)
            // Include the controller's namespace so the generated view can reference
            // the controller type without fully-qualified names.
            // Also resolve namespaces of imported component controllers.
            var nsList = new List<string>(additionalNamespaces);
            if (!string.IsNullOrEmpty(controller.ControllerNamespace))
            {
                nsList.Add(controller.ControllerNamespace!);
            }

            // Resolve imported controller namespaces from the compilation
            foreach (var import in doc.Imports)
            {
                string importFileName = GumlCodeEmitter.GetImportFileName(import);
                string importControllerName = KeyConverter.ToPascalCase(importFileName) + "Controller";

                // Search the compilation for a type with this name
                foreach (var typeSymbol in FindTypesByName(compilation, importControllerName))
                {
                    if (typeSymbol.ContainingNamespace is { IsGlobalNamespace: false })
                    {
                        string ns = typeSymbol.ContainingNamespace.ToDisplayString();
                        if (!nsList.Contains(ns))
                            nsList.Add(ns);
                    }
                    break; // use the first match
                }
            }

            // Resolve namespaces for non-imported custom component types used in the document
            CollectComponentTypeNamespaces(doc.RootComponent, doc, scanner, nsList);

            IReadOnlyList<string> viewNamespaces = nsList;
            string code = GumlCodeEmitter.Emit(
                filePath, doc, viewNamespaces, scanner,
                controller.ControllerSimpleName, controller.ControllerSimpleName, ResolveImport);
            string viewHintName = GumlCodeEmitter.SanitizeIdentifier(
                KeyConverter.ToPascalCase(fileName)) + "GumlView.g.cs";
            context.AddSource(viewHintName, SourceText.From(code, Encoding.UTF8));

            // Emit Controller partial class for named node,
            // parameter, and event properties
            var aliases = GumlCodeEmitter.CollectAliases(doc.RootComponent);
            var parameters = GumlCodeEmitter.GetParameters(doc.RootComponent).ToList();
            var events = GumlCodeEmitter.GetEvents(doc.RootComponent).ToList();

            if (aliases.Count > 0 || parameters.Count > 0 || events.Count > 0)
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
                        existingMembers,
                        additionalNamespaces: viewNamespaces);
                    if (partialCode != null)
                    {
                        string partialHintName = controller.ControllerSimpleName + ".NamedNodes.g.cs";
                        context.AddSource(partialHintName, SourceText.From(partialCode, Encoding.UTF8));
                    }
                }
                else
                {
                    context.ReportDiagnostic(RoslynDiagnostic.Create(
                        GumlDiagnostics.ControllerNotPartial,
                        controller.AttributeLocation,
                        controller.ControllerSimpleName));
                }
            }

            // Report success diagnostic
            string className = GumlCodeEmitter.SanitizeIdentifier(
                KeyConverter.ToPascalCase(fileName)) + "GumlView";
            context.ReportDiagnostic(RoslynDiagnostic.Create(
                GumlDiagnostics.GenerationSuccess,
                Location.None,
                className,
                fileName + ".guml"));
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(RoslynDiagnostic.Create(
                GumlDiagnostics.ParseError,
                Location.None,
                fileName + ".guml",
                $"Unexpected error: {ex.Message}"));
        }
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

    /// <summary>
    /// Finds named type symbols matching the given simple name across the entire compilation.
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> FindTypesByName(Compilation compilation, string simpleName)
    {
        return FindTypesInNamespace(compilation.GlobalNamespace, simpleName);
    }

    private static IEnumerable<INamedTypeSymbol> FindTypesInNamespace(INamespaceSymbol ns, string simpleName)
    {
        foreach (var type in ns.GetTypeMembers(simpleName))
        {
            yield return type;
        }
        foreach (var child in ns.GetNamespaceMembers())
        {
            foreach (var type in FindTypesInNamespace(child, simpleName))
            {
                yield return type;
            }
        }
    }

    /// <summary>
    /// Recursively collects namespaces of non-imported custom component types
    /// referenced in the GUML document tree.
    /// </summary>
    private static void CollectComponentTypeNamespaces(
        ComponentDeclarationSyntax comp, GumlDocumentSyntax doc,
        CompilationApiScanner scanner, List<string> namespaces)
    {
        string typeName = comp.TypeName.Text;

        // Skip imported components (their controller namespaces are already resolved)
        bool isImported = false;
        foreach (var import in doc.Imports)
        {
            string importFileName = GumlCodeEmitter.GetImportFileName(import);
            string nameInGuml = import.Alias != null
                ? import.Alias.Name.Text
                : KeyConverter.ToPascalCase(importFileName);
            if (nameInGuml == typeName)
            {
                isImported = true;
                break;
            }
        }

        if (!isImported)
        {
            string? ns = scanner.ResolveComponentNamespace(typeName);
            if (ns != null && !namespaces.Contains(ns))
                namespaces.Add(ns);
        }

        // Recurse into child components
        foreach (var member in comp.Members)
        {
            if (member is ComponentDeclarationSyntax child)
                CollectComponentTypeNamespaces(child, doc, scanner, namespaces);
            else if (member is EachBlockSyntax each && each.Body != null)
            {
                foreach (var bodyChild in each.Body)
                {
                    if (bodyChild is ComponentDeclarationSyntax eachChild)
                        CollectComponentTypeNamespaces(eachChild, doc, scanner, namespaces);
                }
            }
            else if (member is TemplateParamAssignmentSyntax templateParam)
                CollectComponentTypeNamespaces(templateParam.Component, doc, scanner, namespaces);
        }
    }

}
