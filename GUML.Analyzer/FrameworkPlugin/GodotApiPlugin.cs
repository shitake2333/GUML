using Microsoft.CodeAnalysis;
using GUML.Analyzer.Utils;
using GUML.Shared;
using GUML.Shared.Api;
using Serilog;
using TypeKind = Microsoft.CodeAnalysis.TypeKind;

namespace GUML.Analyzer.FrameworkPlugin;

/// <summary>
/// Godot-specific implementation of <see cref="IGumlApiPlugin"/>.
/// Encapsulates all Godot SDK scanning logic: type support checks, signal scanning,
/// and property observability resolution via <see cref="PropertySignalCatalog"/>.
/// </summary>
public sealed class GodotApiPlugin : IGumlApiPlugin
{
    /// <summary>Shared singleton instance.</summary>
    public static readonly GodotApiPlugin Instance = new();

    private GodotApiPlugin() { }

    /// <summary>
    /// The Godot API catalog used for enriching the API document.
    /// Set by <see cref="ProjectAnalyzer"/> during cache rebuild.
    /// </summary>
    internal GodotApiCatalog? Catalog { get; set; }

    /// <inheritdoc/>
    public string RootBaseTypeName => "Godot.Control";

    /// <inheritdoc/>
    public string FrameworkAssemblyName => "GodotSharp";

    // ── Supported primitive CLR types ──

    private static readonly HashSet<string> s_primitiveClrTypeNames = new(StringComparer.Ordinal)
    {
        nameof(Boolean),
        nameof(Int32),
        nameof(Int64),
        nameof(Single),
        nameof(Double),
        nameof(String)
    };

    // ──────────────────────────────────────────────────────
    // IGumlApiPlugin.IsTypeSupported
    // ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public bool IsTypeSupported(ITypeSymbol type)
    {
        if (type.SpecialType != SpecialType.None)
            return s_primitiveClrTypeNames.Contains(type.Name);

        if (type.IsValueType && GodotTypeConstants.StructTypeNames.Contains(type.Name))
            return true;

        string fullName = type.ContainingNamespace?.ToDisplayString() + "." + type.Name;
        if (GodotTypeConstants.ReferenceTypeFullNames.Contains(fullName))
            return true;

        if (type.TypeKind == TypeKind.Enum)
        {
            string? ns = type.ContainingNamespace?.ToDisplayString();
            if (ns != null && ns.StartsWith("Godot", StringComparison.Ordinal))
                return true;
        }

        if (type.IsValueType)
        {
            string? ns = type.ContainingNamespace?.ToDisplayString();
            if (ns != null && ns.StartsWith("Godot", StringComparison.Ordinal))
                return true;
        }

        foreach (string supportedBase in GodotTypeConstants.SupportedBaseTypes)
        {
            if (InheritsFrom(type, supportedBase))
                return true;
        }

        return false;
    }

    // ──────────────────────────────────────────────────────
    // IGumlApiPlugin.ScanSignalsOnType
    // ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void ScanSignalsOnType(INamedTypeSymbol type, TypeDescriptor typeDesc)
    {
        bool isGodotType = type.ContainingNamespace?.ToDisplayString()
            .StartsWith("Godot", StringComparison.Ordinal) == true;

        foreach (var member in type.GetMembers())
        {
            // Nested delegate type — [Signal] attribute OR Godot-namespace EventHandler convention
            if (member is INamedTypeSymbol { TypeKind: TypeKind.Delegate } nestedType)
            {
                bool hasSignal = HasSignalAttribute(nestedType);
                bool isGodotConvention = isGodotType && nestedType.Name.EndsWith("EventHandler",
                    StringComparison.Ordinal);

                if (hasSignal || isGodotConvention)
                {
                    string rawName = nestedType.Name;
                    if (rawName.EndsWith("EventHandler", StringComparison.Ordinal))
                        rawName = rawName.Substring(0, rawName.Length - "EventHandler".Length);
                    string signalName = StringUtils.ToSnakeCase(rawName);
                    if (typeDesc.Events.ContainsKey(signalName)) continue;

                    var invokeMethod = nestedType.DelegateInvokeMethod;
                    typeDesc.Events[signalName] = new EventDescriptor
                    {
                        Name = signalName,
                        Description = ExtractSummary(nestedType.GetDocumentationCommentXml()),
                        Parameters = invokeMethod?.Parameters.Select(p => new ParameterDescriptor
                        {
                            Name = p.Name,
                            Type = p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                        }).ToList() ?? new List<ParameterDescriptor>()
                    };
                }
            }

            // Event with [Signal] attribute or Godot-namespace event with EventHandler delegate
            if (member is IEventSymbol evt)
            {
                bool hasSignal = HasSignalAttribute(evt);
                bool isGodotConvention = isGodotType
                    && evt.Type is INamedTypeSymbol evtDelegateType
                    && evtDelegateType.Name.EndsWith("EventHandler", StringComparison.Ordinal);

                if (hasSignal || isGodotConvention)
                {
                    string signalName = StringUtils.ToSnakeCase(evt.Name);
                    if (typeDesc.Events.ContainsKey(signalName)) continue;

                    var delegateType = evt.Type as INamedTypeSymbol;
                    var invokeMethod = delegateType?.DelegateInvokeMethod;
                    typeDesc.Events[signalName] = new EventDescriptor
                    {
                        Name = signalName,
                        Description = ExtractSummary(evt.GetDocumentationCommentXml()),
                        Parameters = invokeMethod?.Parameters.Select(p => new ParameterDescriptor
                        {
                            Name = p.Name,
                            Type = p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                        }).ToList() ?? new List<ParameterDescriptor>()
                    };
                }
            }

            // Nested static class containing signal name constants (SignalName / SignalNames)
            if (member is INamedTypeSymbol { TypeKind: TypeKind.Class } nestedConstType)
            {
                string nestedName = nestedConstType.Name;
                if (string.Equals(nestedName, "SignalName", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(nestedName, "SignalNames", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(nestedName, "Signals", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var field in nestedConstType.GetMembers().OfType<IFieldSymbol>())
                    {
                        if (field is { HasConstantValue: true, Type.SpecialType: SpecialType.System_String })
                        {
                            string raw = field.ConstantValue as string ?? field.Name;
                            string signalName = StringUtils.ToSnakeCase(raw);
                            if (typeDesc.Events.ContainsKey(signalName)) continue;

                            typeDesc.Events[signalName] = new EventDescriptor
                            {
                                Name = signalName,
                                Description = ExtractSummary(field.GetDocumentationCommentXml())
                            };
                            continue;
                        }

                        if (field is { IsStatic: true, IsReadOnly: true })
                        {
                            string signalName = StringUtils.ToSnakeCase(field.Name);
                            if (typeDesc.Events.ContainsKey(signalName)) continue;

                            typeDesc.Events[signalName] = new EventDescriptor
                            {
                                Name = signalName,
                                Description = ExtractSummary(field.GetDocumentationCommentXml())
                            };
                        }
                    }
                }
            }
        }
    }

    // ──────────────────────────────────────────────────────
    // IGumlApiPlugin.IsObservableProperty
    // ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public bool IsObservableProperty(string typeName, string propertyName)
        => PropertySignalCatalog.IsObservable(typeName, propertyName);

    /// <inheritdoc/>
    public IReadOnlySet<string> PseudoPropertyNames { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "theme_overrides" };

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> PseudoPropertyDescriptions { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["theme_overrides"] = "Theme override values for this control. Keys are override names; values are typed according to the override category."
        };

    /// <inheritdoc/>
    public void EnrichApiDocument(ApiDocument doc)
    {
        if (Catalog == null) return;
        BuildThemeOverridesSyntheticTypes(doc, Catalog);
    }

    // ── Private helpers ──

    /// <summary>
    /// For each component type that has Godot theme overrides, creates a synthetic
    /// <see cref="TypeDescriptor"/> (e.g. <c>Button$ThemeOverrides</c>) whose properties
    /// correspond to the valid override entries. The <c>theme_overrides</c> pseudo-property
    /// on the component type is then updated to reference this synthetic type, enabling
    /// per-component validation and completion inside <c>theme_overrides: { ... }</c>.
    /// Additionally, each override entry is also injected as a flat property on the
    /// component type itself, so that <c>font_color: ...</c> is also valid without
    /// requiring special-case logic in semantic analysis.
    /// </summary>
    private void BuildThemeOverridesSyntheticTypes(ApiDocument doc, GodotApiCatalog catalog)
    {
        int created = 0;
        // Snapshot the current types to avoid modifying the dictionary during enumeration
        var existingTypes = doc.Types.ToList();
        foreach (var (typeName, typeDesc) in existingTypes)
        {
            var overrides = catalog.GetMergedThemeOverrides(typeName);
            if (overrides.Count == 0) continue;

            string syntheticName = $"{typeName}$ThemeOverrides";
            var syntheticType = new TypeDescriptor
            {
                Name = syntheticName,
                QualifiedName = syntheticName,
                Kind = GumlTypeKind.Struct,
                Description = $"Theme override entries for {typeName}.",
            };

            foreach (var (ovName, ovDataType) in overrides)
            {
                string clrType = GodotApiCatalog.ThemeOverrideDataTypeToClrType(ovDataType);
                string? desc = catalog.GetThemeOverrideDescription(typeName, ovName);
                var propDesc = new PropertyDescriptor
                {
                    Name = ovName,
                    Type = clrType,
                    Description = desc ?? "",
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
                };

                syntheticType.Properties[ovName] = propDesc;

                // Also inject as a flat property on the component type so that
                // direct usage (e.g. "font_color: Color(1,0,0,1)") is valid
                // without special-case handling in SemanticModel/handlers.
                typeDesc.Properties.TryAdd(ovName, propDesc);
            }

            doc.Types[syntheticName] = syntheticType;

            // Update the theme_overrides pseudo-property to reference the synthetic type
            foreach (string pseudoName in PseudoPropertyNames)
            {
                if (typeDesc.Properties.TryGetValue(pseudoName, out var themeProp))
                {
                    themeProp.Type = syntheticName;
                }
            }

            created++;
        }

        if (created > 0)
            Log.Logger.Information(
                "Created {Count} synthetic ThemeOverrides types", created);
    }

    private static bool HasSignalAttribute(ISymbol symbol)
        => symbol.GetAttributes().Any(a =>
            a.AttributeClass?.Name == "SignalAttribute" ||
            a.AttributeClass?.ToDisplayString() == "Godot.SignalAttribute");

    private static bool InheritsFrom(ITypeSymbol type, string baseTypeFullName)
    {
        var current = type as INamedTypeSymbol;
        while (current != null)
        {
            string full = current.ContainingNamespace?.ToDisplayString() + "." + current.Name;
            if (full == baseTypeFullName)
                return true;
            current = current.BaseType;
        }
        return false;
    }

    private static string ExtractSummary(string? xml)
    {
        if (string.IsNullOrEmpty(xml)) return string.Empty;
        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(xml);
            return doc.Descendants("summary").FirstOrDefault()?.Value.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
