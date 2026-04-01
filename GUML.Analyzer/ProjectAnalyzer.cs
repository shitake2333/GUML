using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Serilog;
using GUML.Shared.Api;
using GUML.Analyzer.FrameworkPlugin;
using GUML.Analyzer.Utils;
using TypeKind = Microsoft.CodeAnalysis.TypeKind;

namespace GUML.Analyzer;

/// <summary>
/// Uses Roslyn MSBuildWorkspace to analyze the user's Godot project at runtime.
/// Scans for Control-derived types and their properties, builds an <see cref="ApiDocument"/>,
/// watches for C# file changes and rebuilds the cache incrementally.
/// </summary>
public sealed class ProjectAnalyzer : IDisposable
{
    /// <summary>The path to the currently loaded .csproj.</summary>
    public string WorkProject { get; private set; } = "none";

    private const string GumlControllerAttributeFullName = "GUML.Shared.GumlControllerAttribute";
    private const int DebounceDelayMs = 500;

    private MSBuildWorkspace? _workspace;
    private Project? _project;
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _debounceCts;
    private readonly Lock _debounceLock = new();
    private readonly HashSet<string> _pendingChangedFiles = new();

    private volatile ApiDocument _apiDoc = new() { SchemaVersion = "1.0" };
    private volatile IReadOnlyList<string> _classNames = [];
    private volatile Compilation? _compilation;

    private readonly ConcurrentDictionary<string, Dictionary<string, PropertyDescriptor>>
        _mergedPropertiesCache = new();

    private readonly ConcurrentDictionary<string, Dictionary<string, EventDescriptor>>
        _mergedEventsCache = new();

    // Framework plugin — default to Godot.
    private readonly IGumlApiPlugin _plugin = GodotApiPlugin.Instance;

    private static readonly IDictionary<string, string> s_workspaceGlobalProperties =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RunAnalyzers"] = "false",
            ["RunAnalyzersDuringBuild"] = "false",
            ["RunAnalyzersDuringLiveAnalysis"] = "false",
            ["SkipAnalyzers"] = "true",
            ["DisableAnalyzers"] = "true",
            // Avoid resolving/building project references in design-time workspace load,
            // which can otherwise pull in source-generator project outputs as analyzers.
            ["BuildProjectReferences"] = "false",
            ["ResolveProjectReferences"] = "false"
        };

    /// <summary>Whether the initial project analysis has completed.</summary>
    public bool IsReady { get; private set; }

    /// <summary>The Godot API catalog loaded from external JSON or built-in fallback.</summary>
    internal GodotApiCatalog? GodotCatalog
    {
        get;
        private set
        {
            field = value;
            // Keep the framework plugin in sync so that EnrichApiDocument() can access it.
            GodotApiPlugin.Instance.Catalog = value;
        }
    }

    /// <summary>The Godot SDK version detected from the project's .csproj file.</summary>
    public string? DetectedGodotVersion { get; private set; }

    /// <summary>
    /// Event raised when the API cache has been rebuilt.
    /// The <see cref="ApiCacheDiff"/> parameter describes what changed;
    /// <c>null</c> indicates a full rebuild (initialization or fallback).
    /// </summary>
    public event Action<ApiCacheDiff?>? CacheUpdated;

    /// <summary>
    /// Event raised when the detected Godot version has no matching API file.
    /// The parameters are (godotVersion, expectedPath).
    /// </summary>
    public event Action<string, string>? MissingGodotApi;

    /// <summary>Returns the current API document snapshot.</summary>
    public ApiDocument ApiDocument => _apiDoc;

    /// <summary>
    /// Initializes the analyzer: locates the .csproj, opens MSBuildWorkspace,
    /// runs the initial scan, and starts file watching.
    /// </summary>
    /// <param name="workspaceRoot">The root directory of the user's project.</param>
    /// <param name="projectSelector">
    /// Optional callback invoked when multiple candidate .csproj files are found.
    /// Receives the list of absolute paths and should return the user-selected path,
    /// or <c>null</c> to abort initialization.
    /// </param>
    public async Task InitializeAsync(
        string workspaceRoot,
        Func<List<string>, Task<string?>>? projectSelector = null)
    {
        try
        {
            string? csprojPath = await SelectCsprojAsync(workspaceRoot, projectSelector);
            if (csprojPath == null)
            {
                Log.Logger.Warning("No .csproj found in {Root}, API features will be limited", workspaceRoot);
                return;
            }

            WorkProject = csprojPath;
            Log.Logger.Information("Opening project: {Csproj}", csprojPath);

            // Detect Godot SDK version and load API catalog
            DetectedGodotVersion = GodotApiCatalog.ExtractGodotVersionFromCsproj(csprojPath);
            if (DetectedGodotVersion != null)
                Log.Logger.Information("Detected Godot SDK version: {Version}", DetectedGodotVersion);
            else
                Log.Logger.Debug("No Godot SDK version detected in csproj");

            GodotCatalog = GodotApiCatalog.FindAndLoad(DetectedGodotVersion, workspaceRoot);

            // Notify if external file is missing and we fell back to built-in
            if (DetectedGodotVersion != null && !GodotCatalog.IsFromExternalFile)
            {
                string expectedPath = GodotApiCatalog.GetApiFilePath(DetectedGodotVersion);
                MissingGodotApi?.Invoke(DetectedGodotVersion, expectedPath);
            }

            _workspace = CreateWorkspace();
            _project = await OpenProjectUnlockedAsync(csprojPath);
            Log.Logger.Information("Project opened successfully. Documents: {Count}", _project.Documents.Count());

            await RebuildCacheAsync();

            StartFileWatcher(workspaceRoot);
            IsReady = true;
            Log.Logger.Information("Project analysis complete. IsReady=true. {Count} types found.",
                _apiDoc.Types.Count);
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Failed to initialize project analysis. IsReady remains false.");
        }
    }

    /// <summary>
    /// Reloads the Godot API catalog from the given file path.
    /// Called after a successful <c>guml/generateGodotApi</c> to pick up new data.
    /// </summary>
    public void ReloadGodotCatalog(string jsonPath)
    {
        var catalog = GodotApiCatalog.LoadFromFile(jsonPath);
        if (catalog != null)
        {
            GodotCatalog = catalog;
            Log.Logger.Information("Reloaded Godot API catalog from {Path}", jsonPath);
        }
    }

    // ── Query API ──

    /// <summary>Returns all available type names (simple names).</summary>
    public IReadOnlyList<string> GetAllClassNames() => _classNames;

    /// <summary>
    /// Normalizes a type name to the key used in <see cref="ApiDocument.Types"/>.
    /// Handles qualified names like <c>"Godot.Label"</c> by extracting the simple name.
    /// </summary>
    private string ResolveTypeKey(string typeName)
    {
        if (_apiDoc.Types.ContainsKey(typeName))
            return typeName;

        int lastDot = typeName.LastIndexOf('.');
        if (lastDot >= 0)
        {
            string simpleName = typeName[(lastDot + 1)..];
            if (_apiDoc.Types.ContainsKey(simpleName))
                return simpleName;
        }

        return typeName;
    }

    /// <summary>Returns type descriptor for the given component name, or null if not found.</summary>
    public TypeDescriptor? GetTypeInfo(string className)
    {
        string resolved = ResolveTypeKey(className);
        _apiDoc.Types.TryGetValue(resolved, out var info);
        return info;
    }

    /// <summary>
    /// Returns <c>true</c> when the given type name is present in the current API document.
    /// Use this to guard member-lookup validations against types that are not Godot SDK
    /// types (e.g. imported GUML component types) or when the API cache has not been
    /// built yet (<see cref="IsReady"/> is <c>false</c>).
    /// </summary>
    public bool IsTypeKnown(string className)
    {
        string resolved = ResolveTypeKey(className);
        return _apiDoc.Types.ContainsKey(resolved);
    }

    /// <summary>
    /// Resolves a component type name to its C# source file location.
    /// For source types (user project): returns the .cs file path and declaration line.
    /// For metadata types (SDK assemblies): generates a metadata-as-source stub file and returns its path.
    /// Returns <c>null</c> if the type cannot be resolved (compilation not ready or type unknown).
    /// </summary>
    public TypeSourceLocation? ResolveTypeSource(string componentName)
    {
        var compilation = _compilation;
        if (compilation == null) return null;

        // Resolve the type symbol using the same strategy as CompilationApiScanner
        var typeSymbol = compilation.GetTypeByMetadataName("Godot." + componentName)
                         ?? FindNamedTypeInCompilation(compilation, componentName);
        if (typeSymbol == null) return null;

        // Source type: has declaring syntax references
        var declRef = typeSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (declRef != null)
        {
            var lineSpan = declRef.SyntaxTree.GetLineSpan(declRef.Span);
            return new TypeSourceLocation(
                declRef.SyntaxTree.FilePath,
                lineSpan.StartLinePosition.Line,
                lineSpan.StartLinePosition.Character,
                IsMetadataSource: false);
        }

        // Metadata type: generate metadata-as-source file
        var metaResult = MetadataSourceGenerator.GenerateMetadataSource(typeSymbol);
        return new TypeSourceLocation(
            metaResult.FilePath,
            metaResult.TypeDeclarationLine,
            Column: 0,
            IsMetadataSource: true,
            MemberLines: metaResult.MemberLines);
    }

    /// <summary>
    /// Resolves a property name on a component type to its declaration location.
    /// Walks the inheritance chain to find the type that actually declares the property.
    /// </summary>
    /// <param name="componentName">The GUML component type name (e.g. "Button").</param>
    /// <param name="propertyPascalName">The C# PascalCase property name (e.g. "Text").</param>
    /// <returns>A <see cref="TypeSourceLocation"/> pointing to the property declaration, or <c>null</c>.</returns>
    public TypeSourceLocation? ResolvePropertySource(string componentName, string propertyPascalName)
    {
        var compilation = _compilation;
        if (compilation == null) return null;

        var typeSymbol = compilation.GetTypeByMetadataName("Godot." + componentName)
                         ?? FindNamedTypeInCompilation(compilation, componentName);
        if (typeSymbol == null) return null;

        // Walk the inheritance chain to find the declaring type
        for (var current = typeSymbol; current != null; current = current.BaseType)
        {
            var prop = current.GetMembers(propertyPascalName)
                .OfType<IPropertySymbol>()
                .FirstOrDefault(p => p.DeclaredAccessibility == Accessibility.Public);
            if (prop == null) continue;

            // Source type
            var declRef = prop.DeclaringSyntaxReferences.FirstOrDefault();
            if (declRef != null)
            {
                var lineSpan = declRef.SyntaxTree.GetLineSpan(declRef.Span);
                return new TypeSourceLocation(
                    declRef.SyntaxTree.FilePath,
                    lineSpan.StartLinePosition.Line,
                    lineSpan.StartLinePosition.Character,
                    IsMetadataSource: false);
            }

            // Metadata type: generate source for the declaring type
            var metaResult = MetadataSourceGenerator.GenerateMetadataSource(current);
            if (metaResult.MemberLines.TryGetValue(propertyPascalName, out int line))
            {
                return new TypeSourceLocation(
                    metaResult.FilePath, line, Column: 0,
                    IsMetadataSource: true, MemberLines: metaResult.MemberLines);
            }
        }

        return null;
    }

    /// <summary>
    /// Returns all properties for a type, including inherited properties from the base type chain.
    /// </summary>
    public IReadOnlyDictionary<string, PropertyDescriptor>? GetMergedProperties(string className)
    {
        string resolved = ResolveTypeKey(className);
        if (_mergedPropertiesCache.TryGetValue(resolved, out var cached))
            return cached;

        if (!_apiDoc.Types.TryGetValue(resolved, out var typeDesc))
            return null;

        var merged = new Dictionary<string, PropertyDescriptor>(typeDesc.Properties);

        string? baseType = typeDesc.BaseType;
        while (baseType != null && _apiDoc.Types.TryGetValue(baseType, out var baseInfo))
        {
            foreach (var kvp in baseInfo.Properties)
                merged.TryAdd(kvp.Key, kvp.Value);
            baseType = baseInfo.BaseType;
        }

        _mergedPropertiesCache[resolved] = merged;
        return merged;
    }

    /// <summary>Returns a specific property info for a type (searching up the inheritance chain).</summary>
    public PropertyDescriptor? GetPropertyInfo(string className, string propertyName)
    {
        var props = GetMergedProperties(className);
        if (props != null && props.TryGetValue(propertyName, out var info))
            return info;
        return null;
    }

    /// <summary>
    /// Returns all events for a type, including inherited events from the base type chain.
    /// </summary>
    public IReadOnlyDictionary<string, EventDescriptor>? GetMergedEvents(string className)
    {
        string resolved = ResolveTypeKey(className);
        if (_mergedEventsCache.TryGetValue(resolved, out var cached))
            return cached;

        if (!_apiDoc.Types.TryGetValue(resolved, out var typeDesc))
            return null;

        var merged = new Dictionary<string, EventDescriptor>(typeDesc.Events);

        string? baseType = typeDesc.BaseType;
        while (baseType != null && _apiDoc.Types.TryGetValue(baseType, out var baseInfo))
        {
            foreach (var kvp in baseInfo.Events)
                merged.TryAdd(kvp.Key, kvp.Value);
            baseType = baseInfo.BaseType;
        }

        _mergedEventsCache[resolved] = merged;
        return merged;
    }

    /// <summary>Gets the controller associated with a .guml file path.</summary>
    public ControllerDescriptor? GetControllerForGuml(string gumlFilePath)
    {
        // Canonicalize to absolute path, then normalise separators + case
        string normalized = Path.GetFullPath(gumlFilePath).Replace('\\', '/').ToLowerInvariant();
        _apiDoc.Controllers.TryGetValue(normalized, out var info);
        return info;
    }

    /// <summary>Returns all known controller mappings.</summary>
    public Dictionary<string, ControllerDescriptor> GetAllControllers()
    {
        return new Dictionary<string, ControllerDescriptor>(_apiDoc.Controllers);
    }

    // ── Cache rebuild ──

    /// <summary>
    /// Forces a full rebuild of the API cache by re-opening the project and re-scanning.
    /// Called when the user explicitly requests a rebuild (e.g. via the status bar).
    /// </summary>
    public async Task ForceRebuildAsync()
    {
        if (_workspace == null || _project == null)
        {
            Log.Logger.Warning("Cannot rebuild: no workspace/project loaded");
            return;
        }

        try
        {
            string csprojPath = WorkProject;
            _workspace.Dispose();
            _workspace = CreateWorkspace();
            _project = await OpenProjectUnlockedAsync(csprojPath);
            await RebuildCacheAsync();
            Log.Logger.Information("Force rebuild complete. {Count} types.", _apiDoc.Types.Count);
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Force rebuild failed");
        }
    }

    /// <summary>
    /// Removes all analyzer/source-generator references from the project so that
    /// Roslyn does not load (and file-lock) their DLLs during compilation.
    /// The analyzer only needs type information, not generated source output.
    /// </summary>
    private static Project StripAnalyzerReferences(Project project)
    {
        var refs = project.AnalyzerReferences;
        if (refs.Count == 0) return project;

        foreach (var r in refs)
            project = project.RemoveAnalyzerReference(r);

        Log.Logger.Debug("Stripped {Count} analyzer references to avoid DLL locking", refs.Count);
        return project;
    }

    /// <summary>
    /// Creates an MSBuild workspace configured to avoid loading/running analyzers
    /// from the target project during design-time analysis.
    /// </summary>
    private static MSBuildWorkspace CreateWorkspace()
    {
        var workspace = MSBuildWorkspace.Create(s_workspaceGlobalProperties);
        workspace.RegisterWorkspaceFailedHandler(args =>
        {
            // Promote to Warning so these are visible in the output channel.
            Log.Logger.Warning("MSBuildWorkspace diagnostic: {Message}", args.Diagnostic.Message);
        });

        return workspace;
    }

    /// <summary>
    /// Opens a project and applies lock-avoidance transforms into the workspace solution.
    /// </summary>
    private async Task<Project> OpenProjectUnlockedAsync(string csprojPath,
        CancellationToken cancellationToken = default)
    {
        if (_workspace == null)
            throw new InvalidOperationException("Workspace is not initialized.");

        var project = await _workspace.OpenProjectAsync(csprojPath,
            cancellationToken: cancellationToken);
        project = StripAnalyzerReferences(project);
        project = UnlockMetadataReferences(project);

        try
        {
            if (!_workspace.TryApplyChanges(project.Solution))
            {
                Log.Logger.Warning(
                    "Could not apply lock-avoidance project changes to workspace; continuing with in-memory snapshot.");
                return project;
            }

            var applied = _workspace.CurrentSolution.GetProject(project.Id);
            return applied ?? project;
        }
        catch (Exception ex)
        {
            // MSBuildWorkspace.TryApplyChanges can throw (e.g. when SDK-injected
            // references like GodotSharp have no matching project item).
            // Fall back to the in-memory project snapshot which is fully functional
            // for type analysis purposes.
            Log.Logger.Warning(
                "TryApplyChanges threw; continuing with in-memory snapshot: {Error}", ex.Message);
            return project;
        }
    }

    /// <summary>
    /// Replaces file-based metadata references with in-memory copies so that
    /// Roslyn's compilation does not hold file locks on referenced DLLs.
    /// This allows MSBuild to rebuild those projects while the analyzer is running.
    /// </summary>
    private static Project UnlockMetadataReferences(Project project)
    {
        var replacements = new List<(MetadataReference Old, MetadataReference New)>();

        foreach (var metaRef in project.MetadataReferences)
        {
            if (metaRef is not PortableExecutableReference peRef || peRef.FilePath is null)
                continue;

            try
            {
                // Preserve the XML documentation if an .xml file exists alongside the DLL.
                DocumentationProvider? docProvider = null;
                string xmlPath = Path.ChangeExtension(peRef.FilePath, ".xml");
                if (File.Exists(xmlPath))
                    docProvider = XmlDocumentationProvider.CreateFromFile(xmlPath);

                // Open with FileShare.ReadWrite so we don't conflict with concurrent builds.
                using var fs = new FileStream(
                    peRef.FilePath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);

                // CreateFromStream reads eagerly into memory; the stream can be closed afterwards.
                var inMemoryRef = MetadataReference.CreateFromStream(
                    fs, peRef.Properties, documentation: docProvider, filePath: peRef.FilePath);

                replacements.Add((metaRef, inMemoryRef));
            }
            catch (Exception ex)
            {
                // If a DLL is temporarily unavailable, keep the original file reference.
                Log.Logger.Warning("Could not unlock metadata reference {Path}: {Error}",
                    peRef.FilePath, ex.Message);
            }
        }

        foreach (var (old, @new) in replacements)
            project = project.RemoveMetadataReference(old).AddMetadataReference(@new);

        if (replacements.Count > 0)
            Log.Logger.Debug("Unlocked {Count} metadata references to avoid DLL file locking",
                replacements.Count);

        return project;
    }

    private async Task RebuildCacheAsync()
    {
        if (_project == null) return;

        try
        {
            var compilation = await _project.GetCompilationAsync();
            if (compilation == null) return;

            _compilation = compilation;

            var newDoc = BuildApiDocument(compilation);
            MergeGodotCatalogDescriptions(newDoc);
            _apiDoc = newDoc;
            _mergedPropertiesCache.Clear();
            _mergedEventsCache.Clear();

            string? projectDir = Path.GetDirectoryName(WorkProject);
            ScanControllers(compilation, newDoc, projectDir);
            ScanDataModelTypes(compilation, newDoc);
            _classNames = [.. newDoc.Types.Keys];
            Log.Logger.Information("Cache rebuilt: {TypeCount} types, {CtrlCount} controllers",
                newDoc.Types.Count, newDoc.Controllers.Count);

            CacheUpdated?.Invoke(null); // null = full rebuild
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Failed to rebuild API cache");
        }
    }

    /// <summary>
    /// Incrementally rebuilds the API cache by re-scanning only the types defined
    /// in the changed files. Falls back to a full rebuild when the change scope
    /// exceeds <see cref="IncrementalTypeThreshold"/>.
    /// </summary>
    private async Task RebuildIncrementalAsync(IReadOnlySet<string> changedFiles)
    {
        if (_project == null) return;

        try
        {
            var compilation = await _project.GetCompilationAsync();
            if (compilation == null) return;

            _compilation = compilation;

            var changedTypes = ExtractTypesFromFiles(compilation, changedFiles);

            // Fallback to full rebuild when too many types changed
            if (changedTypes.Count > IncrementalTypeThreshold)
            {
                Log.Logger.Information("Incremental threshold exceeded ({Count} types), falling back to full rebuild",
                    changedTypes.Count);
                await RebuildCacheAsync();
                return;
            }

            string? projectDir = Path.GetDirectoryName(WorkProject);
            var diff = ApplyIncrementalUpdate(compilation, _apiDoc, changedTypes, projectDir);
            _classNames = [.. _apiDoc.Types.Keys];

            if (diff.IsEmpty)
            {
                Log.Logger.Information("Incremental rebuild: no API changes detected");
                return;
            }

            Log.Logger.Information(
                "Incremental rebuild: {UpdTypes} updated types, {RmTypes} removed types, " +
                "{UpdCtrl} updated controllers, {RmCtrl} removed controllers",
                diff.UpdatedTypes.Count, diff.RemovedTypes.Count,
                diff.UpdatedControllers.Count, diff.RemovedControllers.Count);

            CacheUpdated?.Invoke(diff);
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Incremental rebuild failed, falling back to full rebuild");
            await RebuildCacheAsync();
        }
    }

    private const int IncrementalTypeThreshold = 20;

    // ── Scanning logic ──

    /// <summary>
    /// Enriches the API document with descriptions from the Godot API catalog.
    /// GodotSharp.dll has no XML doc comments, so Roslyn-based scanning yields
    /// empty descriptions. This method fills them in from the external JSON data.
    /// </summary>
    private void MergeGodotCatalogDescriptions(ApiDocument doc)
    {
        if (GodotCatalog == null || !GodotCatalog.IsFromExternalFile) return;

        int merged = 0;
        foreach (var (typeName, typeDesc) in doc.Types)
        {
            if (!GodotCatalog.Classes.TryGetValue(typeName, out var godotClass))
                continue;

            // Merge class description
            if (string.IsNullOrEmpty(typeDesc.Description)
                && !string.IsNullOrEmpty(godotClass.BriefDescription))
            {
                typeDesc.Description = godotClass.BriefDescription;
            }

            // Build a lookup from the Godot catalog properties for this class
            // (walk inheritance to cover inherited properties)
            foreach (var (propName, propDesc) in typeDesc.Properties)
            {
                if (!string.IsNullOrEmpty(propDesc.Description)) continue;

                string? desc = GodotCatalog.GetPropertyDescription(typeName, propName);
                if (!string.IsNullOrEmpty(desc))
                {
                    propDesc.Description = desc;
                    merged++;
                }
            }

            // Merge enum value descriptions
            foreach (var (_, propDesc) in typeDesc.Properties)
            {
                if (propDesc.EnumValues == null || propDesc.EnumValues.Count == 0) continue;

                // Find matching Godot enum info
                foreach (var godotEnum in godotClass.Enums)
                {
                    foreach (var enumVal in propDesc.EnumValues)
                    {
                        if (!string.IsNullOrEmpty(enumVal.Description)) continue;

                        // Try exact name match first, then fall back to value match
                        var godotEnumVal = godotEnum.Values
                                               .FirstOrDefault(v => v.Name == enumVal.Name)
                                           ?? godotEnum.Values
                                               .FirstOrDefault(v => v.Value == enumVal.Value);
                        if (godotEnumVal != null && !string.IsNullOrEmpty(godotEnumVal.Description))
                            enumVal.Description = godotEnumVal.Description;
                    }
                }
            }

            // Merge event/signal descriptions
            foreach (var (eventName, eventDesc) in typeDesc.Events)
            {
                if (!string.IsNullOrEmpty(eventDesc.Description)) continue;

                // Walk the Godot class hierarchy to find the signal description
                string? current = typeName;
                while (current != null)
                {
                    if (GodotCatalog.Classes.TryGetValue(current, out var cls))
                    {
                        var signal = cls.Signals.FirstOrDefault(s => s.Name == eventName);
                        if (signal != null && !string.IsNullOrEmpty(signal.Description))
                        {
                            eventDesc.Description = signal.Description;
                            merged++;
                            break;
                        }
                        current = cls.Inherits;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        if (merged > 0)
            Log.Logger.Information(
                "Merged {Count} descriptions from Godot API catalog", merged);

        // Let the framework plugin enrich the document with synthetic types
        // (e.g. per-component ThemeOverrides types for Godot).
        _plugin.EnrichApiDocument(doc);
    }

    private ApiDocument BuildApiDocument(Compilation compilation)
    {
        var controlBaseType = compilation.GetTypeByMetadataName(_plugin.RootBaseTypeName);
        var doc = new ApiDocument { SchemaVersion = "1.0", GeneratedAt = DateTime.UtcNow };

        if (controlBaseType == null) return doc;

        // Scan SDK types from GodotSharp assembly
        ScanAssemblyForControlTypes(doc, controlBaseType.ContainingAssembly, controlBaseType);

        // Also scan the user's own assembly
        foreach (var type in GetAllNamedTypes(compilation.GlobalNamespace))
        {
            if (type.DeclaredAccessibility != Accessibility.Public) continue;
            if (type.TypeKind == TypeKind.Interface || type.TypeKind == TypeKind.Enum) continue;
            if (!InheritsFrom(type, controlBaseType)) continue;

            var typeDesc = BuildTypeDescriptor(type);
            doc.Types.TryAdd(type.Name, typeDesc);
        }

        return doc;
    }

    private void ScanAssemblyForControlTypes(
        ApiDocument doc, IAssemblySymbol assembly, INamedTypeSymbol controlBaseType)
    {
        foreach (var type in GetAllNamedTypes(assembly.GlobalNamespace))
        {
            if (type.DeclaredAccessibility != Accessibility.Public) continue;
            if (type.TypeKind == TypeKind.Interface || type.TypeKind == TypeKind.Enum) continue;
            if (!InheritsFrom(type, controlBaseType)) continue;

            var typeDesc = BuildTypeDescriptor(type);
            doc.Types[type.Name] = typeDesc;
        }
    }

    private TypeDescriptor BuildTypeDescriptor(INamedTypeSymbol type)
    {
        var typeDesc = new TypeDescriptor
        {
            Name = type.Name,
            QualifiedName = type.ToDisplayString(),
            Kind = type.IsValueType ? GumlTypeKind.Struct : GumlTypeKind.Class,
            BaseType = type.BaseType?.Name,
            Description = ExtractSummary(type.GetDocumentationCommentXml())
        };

        var current = type;
        while (current != null && current.Name != "GodotObject")
        {
            foreach (var member in current.GetMembers())
            {
                if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public, GetMethod: not null } prop
                    && _plugin.IsTypeSupported(prop.Type))
                {
                    string snakeName = StringUtils.ToSnakeCase(prop.Name);
                    if (typeDesc.Properties.ContainsKey(snakeName)) continue;

                    bool isWritable = prop.SetMethod is { DeclaredAccessibility: Accessibility.Public };
                    var propDesc = new PropertyDescriptor
                    {
                        Name = snakeName,
                        Type = prop.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        Description = ExtractSummary(prop.GetDocumentationCommentXml()),
                        IsReadable = true,
                        IsWritable = isWritable,
                        Mapping = new MappingConstraintDescriptor
                        {
                            CanStaticMap = isWritable,
                            CanBindDataToProperty = isWritable,
                            CanBindPropertyToData = false,
                            CanBindTwoWay = false,
                            IsObservableProperty = false,
                            ObservabilitySource = ObservabilitySource.None
                        }
                    };

                    // Collect enum values if the property type is an enum
                    if (prop.Type.TypeKind == TypeKind.Enum)
                    {
                        propDesc.EnumValues = prop.Type.GetMembers()
                            .OfType<IFieldSymbol>()
                            .Where(f => f.HasConstantValue)
                            .Select(f => new EnumValueDescriptor
                            {
                                Name = f.Name,
                                Value = f.ConstantValue?.ToString() ?? "",
                                Description = ExtractSummary(f.GetDocumentationCommentXml())
                            })
                            .ToList();
                    }

                    typeDesc.Properties[snakeName] = propDesc;
                }
            }

            // Scan signals via plugin
            _plugin.ScanSignalsOnType(current, typeDesc);

            current = current.BaseType;
        }

        // Resolve property observability from signals and reactive interfaces
        ResolvePropertyObservability(type, typeDesc);

        // Inject pseudo-properties (e.g. "theme_overrides") so that property
        // validation in SemanticModel recognizes them without special-casing.
        foreach (string pseudoName in _plugin.PseudoPropertyNames)
        {
            _plugin.PseudoPropertyDescriptions.TryGetValue(pseudoName, out string? pseudoDesc);
            typeDesc.Properties.TryAdd(pseudoName, new PropertyDescriptor
            {
                Name = pseudoName,
                Type = "object",
                Description = pseudoDesc ?? "",
                IsReadable = false,
                IsWritable = true,
                Mapping = new MappingConstraintDescriptor
                {
                    CanStaticMap = true,
                    CanBindDataToProperty = false,
                    CanBindPropertyToData = false,
                    CanBindTwoWay = false,
                    IsObservableProperty = false,
                    ObservabilitySource = ObservabilitySource.None
                }
            });
        }

        return typeDesc;
    }

    /// <summary>
    /// Updates <see cref="MappingConstraintDescriptor"/> for each property based on
    /// whether the type implements reactive interfaces or has a known change signal
    /// in the Godot API catalog or <see cref="PropertySignalCatalog"/>.
    /// </summary>
    private void ResolvePropertyObservability(INamedTypeSymbol type, TypeDescriptor typeDesc)
    {
        // Check if the type implements INotifyPropertyChanged or INotifyCollectionChanged
        bool implementsInpc = type.AllInterfaces.Any(i =>
            i.Name == "INotifyPropertyChanged"
            && i.ContainingNamespace?.ToDisplayString() == "System.ComponentModel");

        bool implementsInlc = type.AllInterfaces.Any(i =>
            i.Name == "INotifyCollectionChanged"
            && i.ContainingNamespace?.ToDisplayString() == "System.Collections.Specialized");

        foreach (var (propName, propDesc) in typeDesc.Properties)
        {
            if (!propDesc.IsWritable) continue;

            var mapping = propDesc.Mapping;

            // INotifyPropertyChanged: all writable properties are observable
            if (implementsInpc)
            {
                mapping.IsObservableProperty = true;
                mapping.ObservabilitySource = ObservabilitySource.NotifyPropertyChanged;
                mapping.CanBindPropertyToData = true;
                mapping.CanBindTwoWay = true;
                continue;
            }

            // INotifyCollectionChanged: all writable properties are observable
            if (implementsInlc)
            {
                mapping.IsObservableProperty = true;
                mapping.ObservabilitySource = ObservabilitySource.Custom;
                mapping.CanBindPropertyToData = true;
                mapping.CanBindTwoWay = true;
                continue;
            }

            // Check GodotApiCatalog first, then plugin for known property → signal mappings
            bool isObservable = GodotCatalog?.IsObservable(typeDesc.Name, propName) == true
                || _plugin.IsObservableProperty(typeDesc.Name, propName);
            if (isObservable)
            {
                mapping.IsObservableProperty = true;
                mapping.ObservabilitySource = ObservabilitySource.Signal;
                mapping.CanBindPropertyToData = true;
                mapping.CanBindTwoWay = true;
            }
        }
    }



    private static void ScanControllers(Compilation compilation, ApiDocument doc, string? projectDir)
    {
        doc.Controllers.Clear();
        var attrType = compilation.GetTypeByMetadataName(GumlControllerAttributeFullName);
        if (attrType == null) return;

        foreach (var type in GetAllNamedTypes(compilation.GlobalNamespace))
        {
            if (type.DeclaredAccessibility != Accessibility.Public) continue;

            foreach (var attr in type.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attrType)) continue;
                if (attr.ConstructorArguments.Length == 0) continue;
                if (attr.ConstructorArguments[0].Value is not string gumlPath) continue;

                // Resolve the guml file path — handle Godot res:// resource paths
                string? sourceFile = attr.ApplicationSyntaxReference?.SyntaxTree.FilePath;
                if (string.IsNullOrEmpty(sourceFile)) continue;

                string absoluteGumlPath;
                try
                {
                    if (gumlPath.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
                    {
                        // res:// paths are relative to the Godot project root (csproj directory)
                        string relativePath = gumlPath["res://".Length..];
                        string baseDir = projectDir ?? Path.GetDirectoryName(sourceFile) ?? "";
                        absoluteGumlPath = Path.GetFullPath(Path.Combine(baseDir, relativePath));
                    }
                    else
                    {
                        string sourceDir = Path.GetDirectoryName(sourceFile) ?? "";
                        absoluteGumlPath = Path.GetFullPath(Path.Combine(sourceDir, gumlPath));
                    }
                }
                catch
                {
                    continue;
                }

                // Collect properties and methods
                var properties = new List<ParameterDescriptor>();
                var methods = new List<MethodDescriptor>();
                var memberSourceLines = new Dictionary<string, SourcePosition>();
                var memberDescriptions = new Dictionary<string, string>();

                foreach (var member in type.GetMembers())
                {
                    if (member.DeclaredAccessibility != Accessibility.Public) continue;
                    if (member is IPropertySymbol prop)
                    {
                        // Check if the property type is a delegate (Action, Func, etc.)
                        if (IsDelegateType(prop.Type))
                        {
                            var md = BuildDelegateDescriptor(prop.Name, prop.Type, prop);
                            md.IsDelegate = true;
                            methods.Add(md);
                        }
                        else
                        {
                            properties.Add(new ParameterDescriptor
                            {
                                Name = prop.Name,
                                Type = prop.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                                Description = ExtractDocSummary(prop)
                            });
                        }

                        RecordMemberLine(memberSourceLines, prop.Name, prop);
                        if (ExtractDocSummary(prop) is { } propDoc)
                            memberDescriptions.TryAdd(prop.Name, propDoc);
                    }
                    else if (member is IFieldSymbol { IsStatic: false, IsConst: false } field)
                    {
                        if (IsDelegateType(field.Type))
                        {
                            var md = BuildDelegateDescriptor(field.Name, field.Type, field);
                            md.IsDelegate = true;
                            methods.Add(md);
                        }
                        else
                        {
                            properties.Add(new ParameterDescriptor
                            {
                                Name = field.Name,
                                Type = field.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                                Description = ExtractDocSummary(field)
                            });
                        }

                        RecordMemberLine(memberSourceLines, field.Name, field);
                        if (ExtractDocSummary(field) is { } fieldDoc)
                            memberDescriptions.TryAdd(field.Name, fieldDoc);
                    }
                    else if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary, IsStatic: false } method)
                    {
                        var md = new MethodDescriptor
                        {
                            Name = method.Name,
                            ReturnType = method.ReturnType.ToDisplayString(
                                SymbolDisplayFormat.MinimallyQualifiedFormat),
                            Description = ExtractDocSummary(method)
                        };
                        foreach (var p in method.Parameters)
                        {
                            md.Parameters.Add(new ParameterDescriptor
                            {
                                Name = p.Name,
                                Type = p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                            });
                        }

                        methods.Add(md);
                        RecordMemberLine(memberSourceLines, method.Name, method);
                        if (md.Description is { } methodDoc)
                            memberDescriptions.TryAdd(method.Name, methodDoc);
                    }
                    else if (member is IEventSymbol evt)
                    {
                        var md = BuildDelegateDescriptor(evt.Name, evt.Type, evt);
                        md.IsDelegate = true;
                        methods.Add(md);
                        RecordMemberLine(memberSourceLines, evt.Name, evt);
                        if (md.Description is { } evtDoc)
                            memberDescriptions.TryAdd(evt.Name, evtDoc);
                    }
                }

                string normalizedPath = absoluteGumlPath.Replace('\\', '/').ToLowerInvariant();

                bool isReactive = type.AllInterfaces.Any(i =>
                    i.Name == "INotifyPropertyChanged"
                    && i.ContainingNamespace?.ToDisplayString() == "System.ComponentModel");

                // Resolve the class declaration source location
                string? controllerSourceFile = null;
                int controllerSourceLine = 0;
                var declRef = type.DeclaringSyntaxReferences.FirstOrDefault();
                if (declRef != null)
                {
                    controllerSourceFile = declRef.SyntaxTree.FilePath;
                    var lineSpan = declRef.SyntaxTree.GetLineSpan(declRef.Span);
                    controllerSourceLine = lineSpan.StartLinePosition.Line;
                }

                doc.Controllers[normalizedPath] = new ControllerDescriptor
                {
                    FullName = type.ToDisplayString(),
                    SimpleName = type.Name,
                    GumlPath = absoluteGumlPath,
                    Properties = properties,
                    Methods = methods,
                    IsReactive = isReactive,
                    SourceFile = controllerSourceFile,
                    SourceLine = controllerSourceLine,
                    MemberSourceLines = memberSourceLines,
                    Description = ExtractDocSummary(type),
                    MemberDescriptions = memberDescriptions
                };
            }
        }
    }

    private static void RecordMemberLine(Dictionary<string, SourcePosition> dict, string name, ISymbol symbol)
    {
        var syntaxRef = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null) return;
        var lineSpan = syntaxRef.SyntaxTree.GetLineSpan(syntaxRef.Span);
        dict.TryAdd(name, new SourcePosition(
            lineSpan.StartLinePosition.Line,
            lineSpan.StartLinePosition.Character));
    }

    /// <summary>
    /// Extracts the &lt;summary&gt; text from a Roslyn symbol's XML documentation comment.
    /// Returns null when no documentation is available.
    /// </summary>
    private static string? ExtractDocSummary(ISymbol symbol)
    {
        string result = ExtractSummary(symbol.GetDocumentationCommentXml());
        return result.Length > 0 ? result : null;
    }

    private static bool IsDelegateType(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Delegate) return true;
        // Match Action, Action<T,...>, Func<T,...>
        string name = type.OriginalDefinition.ToDisplayString();
        return name.StartsWith("System.Action") || name.StartsWith("System.Func");
    }

    private static MethodDescriptor BuildDelegateDescriptor(string name, ITypeSymbol delegateType, ISymbol owner)
    {
        var md = new MethodDescriptor { Name = name, ReturnType = "void", Description = ExtractDocSummary(owner) };

        // Try to extract Invoke parameters from the delegate
        if (delegateType is INamedTypeSymbol namedType)
        {
            var invoke = namedType.DelegateInvokeMethod;
            if (invoke != null)
            {
                md.ReturnType = invoke.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                foreach (var p in invoke.Parameters)
                {
                    md.Parameters.Add(new ParameterDescriptor
                    {
                        Name = p.Name, Type = p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                    });
                }
            }
        }

        return md;
    }

    // ── Data model type scanning ──

    /// <summary>
    /// Scans non-Godot user-defined types (data model classes) that are transitively
    /// referenced from controller properties so that member-access chains like
    /// <c>$controller.data.name</c> and each-variable chains like <c>row.items</c>
    /// can resolve intermediate types.
    /// </summary>
    private static void ScanDataModelTypes(Compilation compilation, ApiDocument doc)
    {
        var queue = new Queue<INamedTypeSymbol>();
        var visited = new HashSet<string>(doc.Types.Keys);

        // Seed: collect types referenced by controller properties
        foreach (var ctrl in doc.Controllers.Values)
        {
            foreach (var prop in ctrl.Properties)
            {
                TryEnqueueTypeByName(compilation, prop.Type, queue, visited);
            }
        }

        // BFS: scan each type and enqueue its transitive references
        while (queue.Count > 0)
        {
            var type = queue.Dequeue();
            var typeDesc = BuildDataTypeDescriptor(type, compilation, queue, visited);
            doc.Types.TryAdd(type.Name, typeDesc);
        }
    }

    /// <summary>
    /// Resolves a type name (possibly generic like <c>List&lt;RowData&gt;</c>) to its
    /// underlying data-model type symbol and enqueues it for scanning.
    /// </summary>
    private static void TryEnqueueTypeByName(
        Compilation compilation, string typeName, Queue<INamedTypeSymbol> queue, HashSet<string> visited)
    {
        // Strip generic wrapper: "List<RowData>" → "RowData", "RowData[]" → "RowData"
        string coreName = ExtractCoreTypeName(typeName);
        if (visited.Contains(coreName)) return;

        var symbol = FindNamedTypeInCompilation(compilation, coreName);
        if (symbol == null) return;
        if (!IsUserDataModelType(symbol)) return;

        visited.Add(coreName);
        queue.Enqueue(symbol);
    }

    /// <summary>
    /// Builds a lightweight <see cref="TypeDescriptor"/> for a user data-model type.
    /// Unlike Godot Control types, no signal or observability scanning is performed.
    /// All public readable properties are included regardless of <see cref="IsUserDataModelType"/>.
    /// </summary>
    private static TypeDescriptor BuildDataTypeDescriptor(
        INamedTypeSymbol type, Compilation compilation,
        Queue<INamedTypeSymbol> queue, HashSet<string> visited)
    {
        var typeDesc = new TypeDescriptor
        {
            Name = type.Name,
            QualifiedName = type.ToDisplayString(),
            Kind = type.IsValueType ? GumlTypeKind.Struct : GumlTypeKind.Class,
            BaseType = type.BaseType?.Name,
            Description = ExtractSummary(type.GetDocumentationCommentXml())
        };

        foreach (var member in type.GetMembers())
        {
            if (member is not IPropertySymbol
                {
                    DeclaredAccessibility: Accessibility.Public, GetMethod: not null
                } prop)
                continue;

            string snakeName = StringUtils.ToSnakeCase(prop.Name);
            if (typeDesc.Properties.ContainsKey(snakeName)) continue;

            string propTypeName = prop.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            bool isWritable = prop.SetMethod is { DeclaredAccessibility: Accessibility.Public };

            typeDesc.Properties[snakeName] = new PropertyDescriptor
            {
                Name = snakeName,
                Type = propTypeName,
                Description = ExtractSummary(prop.GetDocumentationCommentXml()),
                IsReadable = true,
                IsWritable = isWritable,
                Mapping = new MappingConstraintDescriptor()
            };

            // Enqueue referenced user types for transitive scanning
            TryEnqueueTypeByName(compilation, propTypeName, queue, visited);
        }

        return typeDesc;
    }

    /// <summary>
    /// Determines whether a type is a user-defined data model (non-Godot, non-System class/struct).
    /// </summary>
    private static bool IsUserDataModelType(INamedTypeSymbol type)
    {
        if (type.DeclaredAccessibility != Accessibility.Public) return false;
        if (type.TypeKind is TypeKind.Interface or TypeKind.Enum or TypeKind.Delegate) return false;
        if (type.SpecialType != SpecialType.None) return false;

        string? ns = type.ContainingNamespace?.ToDisplayString();
        if (ns != null && (ns.StartsWith("Godot") || ns.StartsWith("System"))) return false;

        return true;
    }

    /// <summary>
    /// Extracts the innermost type name from generic or array wrappers.
    /// E.g. <c>"List&lt;RowData&gt;"</c> → <c>"RowData"</c>,
    /// <c>"RowData[]"</c> → <c>"RowData"</c>, <c>"PlayerData"</c> → <c>"PlayerData"</c>.
    /// </summary>
    internal static string ExtractCoreTypeName(string typeName)
    {
        // Handle arrays: "RowData[]" → "RowData"
        if (typeName.EndsWith("[]"))
            return ExtractCoreTypeName(typeName[..^2]);

        // Handle generics: "List<RowData>" → "RowData"
        int openAngle = typeName.IndexOf('<');
        if (openAngle >= 0)
        {
            int closeAngle = typeName.LastIndexOf('>');
            if (closeAngle > openAngle)
                return ExtractCoreTypeName(typeName[(openAngle + 1)..closeAngle].Trim());
        }

        return typeName;
    }

    /// <summary>
    /// Searches the compilation for a named type by its simple name.
    /// </summary>
    private static INamedTypeSymbol? FindNamedTypeInCompilation(Compilation compilation, string simpleName)
    {
        foreach (var type in GetAllNamedTypes(compilation.GlobalNamespace))
        {
            if (type.Name == simpleName) return type;
        }

        return null;
    }

    // ── Incremental rebuild helpers ──

    /// <summary>
    /// Extracts all named types defined in the given set of changed file paths.
    /// </summary>
    private static IReadOnlyList<INamedTypeSymbol> ExtractTypesFromFiles(
        Compilation compilation, IReadOnlySet<string> changedFiles)
    {
        var result = new List<INamedTypeSymbol>();
        var normalizedPaths = new HashSet<string>(
            changedFiles.Select(Path.GetFullPath),
            StringComparer.OrdinalIgnoreCase);

        foreach (var tree in compilation.SyntaxTrees)
        {
            if (!normalizedPaths.Contains(Path.GetFullPath(tree.FilePath)))
                continue;

            var semanticModel = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            foreach (var typeDecl in root.DescendantNodes()
                         .Where(n => n is Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax))
            {
                if (semanticModel.GetDeclaredSymbol(typeDecl) is INamedTypeSymbol typeSymbol)
                    result.Add(typeSymbol);
            }
        }

        return result;
    }

    /// <summary>
    /// Applies incremental updates for the given changed types and returns a diff
    /// describing what was added, updated, or removed.
    /// </summary>
    private ApiCacheDiff ApplyIncrementalUpdate(
        Compilation compilation, ApiDocument doc,
        IReadOnlyList<INamedTypeSymbol> changedTypes, string? projectDir)
    {
        var diff = new ApiCacheDiff();
        var controlBaseType = compilation.GetTypeByMetadataName("Godot.Control");
        var attrType = compilation.GetTypeByMetadataName(GumlControllerAttributeFullName);

        // Track which old controller paths belong to the changed types so we can detect removals
        var oldControllerPaths = new HashSet<string>();
        foreach (var (path, ctrl) in doc.Controllers)
        {
            foreach (var changedType in changedTypes)
            {
                if (ctrl.FullName == changedType.ToDisplayString())
                    oldControllerPaths.Add(path);
            }
        }

        foreach (var type in changedTypes)
        {
            // Case 1: Controller type (has [GumlController] attribute)
            if (attrType != null)
            {
                foreach (var attr in type.GetAttributes())
                {
                    if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attrType)) continue;
                    if (attr.ConstructorArguments.Length == 0) continue;
                    if (attr.ConstructorArguments[0].Value is not string gumlPath) continue;

                    // Re-scan this single controller
                    string? sourceFile = attr.ApplicationSyntaxReference?.SyntaxTree.FilePath;
                    if (string.IsNullOrEmpty(sourceFile)) continue;

                    string absoluteGumlPath;
                    try
                    {
                        absoluteGumlPath = gumlPath.StartsWith("res://", StringComparison.OrdinalIgnoreCase)
                            ? Path.GetFullPath(Path.Combine(
                                projectDir ?? Path.GetDirectoryName(sourceFile) ?? "",
                                gumlPath["res://".Length..]))
                            : Path.GetFullPath(Path.Combine(
                                Path.GetDirectoryName(sourceFile) ?? "", gumlPath));
                    }
                    catch { continue; }

                    string normalizedPath = absoluteGumlPath.Replace('\\', '/').ToLowerInvariant();
                    oldControllerPaths.Remove(normalizedPath);

                    // Rebuild controller via full scan (reuses ScanControllers for single type)
                    RebuildSingleController(compilation, doc, type, projectDir);
                    diff.UpdatedControllers.Add(normalizedPath);
                }
            }

            // Case 2: Control-derived type
            if (controlBaseType != null && InheritsFrom(type, controlBaseType))
            {
                var typeDesc = BuildTypeDescriptor(type);
                doc.Types[type.Name] = typeDesc;
                _mergedPropertiesCache.TryRemove(type.Name, out _);
                _mergedEventsCache.TryRemove(type.Name, out _);
                diff.UpdatedTypes.Add(type.Name);
                continue;
            }

            // Case 3: User data model type
            if (IsUserDataModelType(type))
            {
                var queue = new Queue<INamedTypeSymbol>();
                var visited = new HashSet<string>(doc.Types.Keys);
                visited.Remove(type.Name); // Allow re-scan of this type

                queue.Enqueue(type);
                while (queue.Count > 0)
                {
                    var t = queue.Dequeue();
                    var typeDesc = BuildDataTypeDescriptor(t, compilation, queue, visited);
                    doc.Types[t.Name] = typeDesc;
                    _mergedPropertiesCache.TryRemove(t.Name, out _);
                    _mergedEventsCache.TryRemove(t.Name, out _);
                    diff.UpdatedTypes.Add(t.Name);
                }
            }
        }

        // Handle removed controllers (types that were controllers but no longer are)
        foreach (string removedPath in oldControllerPaths)
        {
            doc.Controllers.Remove(removedPath);
            diff.RemovedControllers.Add(removedPath);
        }

        // Re-scan data model types referenced by updated controllers
        foreach (string ctrlPath in diff.UpdatedControllers)
        {
            if (doc.Controllers.TryGetValue(ctrlPath, out var ctrl))
            {
                foreach (var prop in ctrl.Properties)
                    TryEnqueueAndScanDataType(compilation, doc, prop.Type, diff);
            }
        }

        return diff;
    }

    /// <summary>
    /// Rebuilds a single controller descriptor and updates the API document.
    /// </summary>
    private static void RebuildSingleController(
        Compilation compilation, ApiDocument doc,
        INamedTypeSymbol type, string? projectDir)
    {
        // Delegate to the existing ScanControllers logic but for this single type.
        // We temporarily create a mini-doc, scan, and merge back.
        var tempDoc = new ApiDocument { SchemaVersion = doc.SchemaVersion };
        ScanControllers(compilation, tempDoc, projectDir);

        foreach (var (path, ctrl) in tempDoc.Controllers)
        {
            if (ctrl.FullName == type.ToDisplayString())
                doc.Controllers[path] = ctrl;
        }
    }

    /// <summary>
    /// Checks if a property type references an unregistered data model type and scans it.
    /// </summary>
    private void TryEnqueueAndScanDataType(
        Compilation compilation, ApiDocument doc, string typeName, ApiCacheDiff diff)
    {
        string coreName = ExtractCoreTypeName(typeName);
        if (doc.Types.ContainsKey(coreName)) return;

        var symbol = FindNamedTypeInCompilation(compilation, coreName);
        if (symbol == null || !IsUserDataModelType(symbol)) return;

        var queue = new Queue<INamedTypeSymbol>();
        var visited = new HashSet<string>(doc.Types.Keys);
        queue.Enqueue(symbol);
        visited.Add(coreName);

        while (queue.Count > 0)
        {
            var t = queue.Dequeue();
            var typeDesc = BuildDataTypeDescriptor(t, compilation, queue, visited);
            doc.Types[t.Name] = typeDesc;
            _mergedPropertiesCache.TryRemove(t.Name, out _);
            _mergedEventsCache.TryRemove(t.Name, out _);
            diff.UpdatedTypes.Add(t.Name);
        }
    }

    // ── File watching ──

    private void StartFileWatcher(string rootPath)
    {
        try
        {
            _watcher = new FileSystemWatcher(rootPath, "*.cs")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime
            };

            _watcher.Changed += OnCsFileChanged;
            _watcher.Created += OnCsFileChanged;
            _watcher.Deleted += OnCsFileChanged;
            _watcher.Renamed += (_, e) => OnCsFileChanged(null, e);

            _watcher.EnableRaisingEvents = true;
            Log.Logger.Information("Started watching C# files in {Root}", rootPath);
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Failed to start file watcher");
        }
    }

    private void OnCsFileChanged(object? sender, FileSystemEventArgs e)
    {
        // Ignore obj/bin directories
        if (e.FullPath.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) ||
            e.FullPath.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            return;

        lock (_debounceLock)
        {
            _pendingChangedFiles.Add(e.FullPath);
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(DebounceDelayMs, token);
                    if (token.IsCancellationRequested) return;

                    // Snapshot and clear pending files
                    HashSet<string> changedFiles;
                    lock (_debounceLock)
                    {
                        changedFiles = [.. _pendingChangedFiles];
                        _pendingChangedFiles.Clear();
                    }

                    Log.Logger.Information(
                        "C# file change detected ({Count} files), rebuilding API cache...",
                        changedFiles.Count);

                    // Reload the project to pick up changes
                    if (_workspace != null && _project != null)
                    {
                        string csprojPath = WorkProject;

                        // Dispose old workspace — MSBuildWorkspace caches evaluation data
                        // internally, so CloseSolution() alone is not enough to see new content.
                        _workspace.Dispose();
                        _workspace = CreateWorkspace();

                        _project = await OpenProjectUnlockedAsync(csprojPath, token);
                        await RebuildIncrementalAsync(changedFiles);
                        Log.Logger.Information("API cache rebuilt. {Count} types.", _apiDoc.Types.Count);
                    }
                }
                catch (TaskCanceledException)
                {
                    // Debounce: a newer change came in
                }
                catch (Exception ex)
                {
                    Log.Logger.Error(ex, "Error rebuilding cache after file change");
                }
            }, CancellationToken.None);
        }
    }

    // ── Utility helpers ──

    private static bool InheritsFrom(ITypeSymbol type, INamedTypeSymbol baseType)
    {
        var current = type;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
            current = current.BaseType;
        }

        return false;
    }

    private static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
            yield return type;
        foreach (var child in ns.GetNamespaceMembers())
        foreach (var type in GetAllNamedTypes(child))
            yield return type;
    }

    /// <summary>
    /// Extracts a plain-text summary from XML documentation comment string.
    /// </summary>
    private static string ExtractSummary(string? xmlDoc)
    {
        if (string.IsNullOrWhiteSpace(xmlDoc)) return "";
        try
        {
            // Simple extraction: find <summary>...</summary> content
            const string startTag = "<summary>";
            const string endTag = "</summary>";
            int start = xmlDoc.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
            int end = xmlDoc.IndexOf(endTag, StringComparison.OrdinalIgnoreCase);
            if (start >= 0 && end > start)
            {
                string content = xmlDoc.Substring(start + startTag.Length, end - start - startTag.Length);
                // Strip remaining XML tags and normalize whitespace
                content = System.Text.RegularExpressions.Regex.Replace(content, "<[^>]+>", "");
                content = System.Text.RegularExpressions.Regex.Replace(content.Trim(), @"\s+", " ");
                return content;
            }
        }
        catch
        {
            // Fall through
        }

        return "";
    }

    /// <summary>
    /// Selects the best .csproj from the workspace. When multiple candidates
    /// share the same highest score and a <paramref name="projectSelector"/>
    /// is provided, prompts the user to choose.
    /// </summary>
    private static async Task<string?> SelectCsprojAsync(
        string rootPath,
        Func<List<string>, Task<string?>>? projectSelector)
    {
        var candidates = FindAllCsprojs(rootPath);
        if (candidates.Count == 0)
        {
            Log.Logger.Warning("No .csproj files found under {Root}", rootPath);
            return null;
        }

        Log.Logger.Information("Found {Count} .csproj files, top score={Score}",
            candidates.Count, candidates[0].Score);

        // Group by the highest score — only prompt when multiple projects tie
        int topScore = candidates[0].Score;
        var topGroup = candidates.Where(c => c.Score == topScore).ToList();

        if (topGroup.Count == 1)
        {
            Log.Logger.Information("Auto-selected top-scored project: {Csproj} (score={Score})",
                topGroup[0].Path, topGroup[0].Score);
            return topGroup[0].Path;
        }

        // Multiple projects at the same top score — prompt the user to choose
        if (projectSelector != null)
        {
            Log.Logger.Information(
                "Found {Count} projects tied at score={Score}, prompting user to select",
                topGroup.Count, topScore);

            string? selected = await projectSelector(topGroup.Select(c => c.Path).ToList());
            if (selected != null) return selected;

            // User dismissed the prompt — fall back to first candidate
            Log.Logger.Information("User dismissed project selection, using first candidate: {Csproj}",
                topGroup[0].Path);
        }

        return topGroup[0].Path;
    }

    /// <summary>
    /// Discovers all .csproj files under <paramref name="rootPath"/> and returns
    /// them sorted by relevance score (highest first).
    /// <list type="bullet">
    ///   <item>100 — Uses <c>Godot.NET.Sdk</c> as the project SDK (real Godot application)</item>
    ///   <item>50 — References <c>GodotSharp</c> AND has <c>.guml</c> files nearby</item>
    ///   <item>30 — Has <c>.guml</c> files in the same directory tree</item>
    ///   <item>10 — References <c>GodotSharp</c> (library, not an application)</item>
    ///   <item>0 — Other .csproj files</item>
    /// </list>
    /// </summary>
    internal static List<(string Path, int Score)> FindAllCsprojs(string rootPath)
    {
        try
        {
            string[] allCsprojs = Directory.GetFiles(rootPath, "*.csproj", SearchOption.AllDirectories);
            if (allCsprojs.Length == 0) return [];

            var scored = new List<(string Path, int Score)>();

            foreach (string csproj in allCsprojs)
            {
                int score = 0;
                bool hasGodotSdk = false;
                bool hasGodotSharp = false;
                bool hasGumlFiles = false;

                try
                {
                    string content = File.ReadAllText(csproj);
                    hasGodotSdk = content.Contains("Godot.NET.Sdk", StringComparison.OrdinalIgnoreCase);
                    hasGodotSharp = content.Contains("GodotSharp", StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    // Skip unreadable files
                }

                string? dir = Path.GetDirectoryName(csproj);
                if (dir != null)
                {
                    try
                    {
                        hasGumlFiles = Directory.GetFiles(dir, "*.guml", SearchOption.AllDirectories).Length > 0;
                    }
                    catch
                    {
                        // Skip inaccessible directories
                    }
                }

                if (hasGodotSdk)
                    score = 100;
                else if (hasGodotSharp && hasGumlFiles)
                    score = 50;
                else if (hasGumlFiles)
                    score = 30;
                else if (hasGodotSharp)
                    score = 10;

                // project scoring debug removed
                scored.Add((csproj, score));
            }

            // Sort descending by score, then by path length ascending (prefer shallower paths)
            scored.Sort((a, b) =>
            {
                int cmp = b.Score.CompareTo(a.Score);
                return cmp != 0 ? cmp : a.Path.Length.CompareTo(b.Path.Length);
            });

            return scored;
        }
        catch
        {
            return [];
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _watcher?.Dispose();
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _workspace?.Dispose();
    }
}
