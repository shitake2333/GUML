using PathUtils = GUML.Analyzer.Utils.PathUtils;
using GUML.Shared.Api;
using GUML.Shared.Converter;
using GUML.Shared.Syntax;
using GUML.Shared.Syntax.Nodes;
using GUML.Shared.Syntax.Nodes.Expressions;
using Serilog;

namespace GUML.Analyzer.Workspace;

/// <summary>
/// Provides semantic analysis for a single GUML document:
/// resolves types, validates bindings, produces diagnostics (GUML1xxx / GUML2xxx / GUML3xxx).
/// </summary>
public sealed class SemanticModel
{
    private readonly GumlDocument _document;
    private readonly ProjectAnalyzer _analyzer;
    private List<Diagnostic>? _diagnostics;
    private HashSet<string>? _importedNames;
    private Dictionary<string, TypeDescriptor>? _importedTypes;
    private Dictionary<string, AliasInfo>? _aliasTable;

    public SemanticModel(GumlDocument document, ProjectAnalyzer analyzer)
    {
        _document = document;
        _analyzer = analyzer;
    }

    /// <summary>
    /// Returns all diagnostics (syntax + semantic) for this document.
    /// </summary>
    public IReadOnlyList<Diagnostic> GetDiagnostics()
    {
        if (_diagnostics != null) return _diagnostics;

        _diagnostics = new List<Diagnostic>(_document.SyntaxDiagnostics);
        AnalyzeDocument(_document.Root);
        return _diagnostics;
    }

    /// <summary>
    /// Resolves a component name to its <see cref="TypeDescriptor"/>.
    /// Checks API cache first, then imported component types.
    /// </summary>
    public TypeDescriptor? ResolveComponentType(string name)
    {
        var desc = _analyzer.GetTypeInfo(name);
        if (desc != null) return desc;

        // Ensure import types are built (GetDiagnostics may not have been called yet)
        EnsureImportTypesBuilt();
        _importedTypes?.TryGetValue(name, out desc);
        return desc;
    }

    /// <summary>
    /// Returns the imported type descriptor for a component name, or null if not an import.
    /// </summary>
    public TypeDescriptor? GetImportedType(string name)
    {
        EnsureImportTypesBuilt();
        if (_importedTypes != null && _importedTypes.TryGetValue(name, out var desc))
            return desc;
        return null;
    }

    /// <summary>
    /// Returns the import source path for a given component name, or null if not an import.
    /// </summary>
    public string? GetImportSourcePath(string name)
    {
        foreach (var import in _document.Root.Imports)
        {
            string? alias = import.Alias?.Name.Text;
            string pathText = import.Path.Text.Trim('"');
            string derived = Path.GetFileNameWithoutExtension(pathText);
            if (name == alias || name == derived)
                return pathText;
        }

        return null;
    }

    /// <summary>
    /// Resolves a property on a given component type (walks inheritance chain).
    /// Also checks imported component parameter declarations.
    /// </summary>
    public PropertyDescriptor? ResolveProperty(string componentType, string propertyName)
    {
        var desc = _analyzer.GetPropertyInfo(componentType, propertyName);
        if (desc != null) return desc;

        EnsureImportTypesBuilt();
        if (_importedTypes != null && _importedTypes.TryGetValue(componentType, out var importedType))
        {
            if (importedType.Properties.TryGetValue(propertyName, out desc))
                return desc;

            // Fall back to base type inheritance chain
            if (!string.IsNullOrEmpty(importedType.BaseType))
                return _analyzer.GetPropertyInfo(importedType.BaseType, propertyName);
        }

        return null;
    }

    /// <summary>
    /// Returns all events for an imported component type, or null if not found.
    /// </summary>
    public IReadOnlyDictionary<string, EventDescriptor>? GetImportedEvents(string componentType)
    {
        EnsureImportTypesBuilt();
        if (_importedTypes != null && _importedTypes.TryGetValue(componentType, out var importedType))
            return importedType.Events;
        return null;
    }

    /// <summary>
    /// Gets the controller associated with this document via its URI.
    /// </summary>
    public ControllerDescriptor? GetController()
    {
        string path = PathUtils.UriToFilePath(_document.Uri);
        return string.IsNullOrEmpty(path) ? null : _analyzer.GetControllerForGuml(path);
    }

    /// <summary>
    /// Public entry point for resolving the type of an expression.
    /// Used by handlers (e.g. Hover) to determine intermediate types in chained access.
    /// </summary>
    public string? ResolveExpressionTypePublic(ExpressionSyntax expression)
    {
        return ResolveExpressionType(expression);
    }

    /// <summary>
    /// Finds the named node alias info for the given name, or null if not declared.
    /// </summary>
    public AliasInfo? FindAlias(string aliasName)
    {
        EnsureAliasTableBuilt();
        return _aliasTable!.GetValueOrDefault(aliasName);
    }

    /// <summary>
    /// Returns all named node aliases declared in this document.
    /// </summary>
    public IReadOnlyDictionary<string, AliasInfo> GetAllAliases()
    {
        EnsureAliasTableBuilt();
        return _aliasTable!;
    }

    /// <summary>
    /// Lazily builds the alias table by scanning all component declarations.
    /// </summary>
    private void EnsureAliasTableBuilt()
    {
        if (_aliasTable != null) return;
        _aliasTable = new Dictionary<string, AliasInfo>();
        CollectAliases(_document.Root);
    }

    private void CollectAliases(SyntaxNode node)
    {
        foreach (var descendant in node.DescendantNodes())
        {
            if (descendant is ComponentDeclarationSyntax { AliasPrefix: { } prefix } comp)
            {
                _aliasTable![prefix.AliasRef.Text] = new AliasInfo(
                    prefix.AliasRef.Text,
                    comp.TypeName.Text,
                    prefix.AliasRef,
                    comp.DocumentationComment);
            }
        }
    }

    /// <summary>
    /// Finds the each-block variable that the given identifier refers to, if any.
    /// Walks up the AST from the token's parent to find the nearest enclosing
    /// <see cref="EachBlockSyntax"/> whose IndexName or ValueName matches.
    /// </summary>
    public EachVariableInfo? FindEachVariable(SyntaxToken identifier)
    {
        string name = identifier.Text;
        if (string.IsNullOrEmpty(name)) return null;

        for (var current = identifier.Parent; current != null; current = current.Parent)
        {
            if (current is not EachBlockSyntax each) continue;

            if (each.IndexName != null && each.IndexName.Text == name)
                return new EachVariableInfo(name, true, each, "int");

            if (each.ValueName != null && each.ValueName.Text == name)
            {
                string? elementType = ResolveEachElementType(each);
                return new EachVariableInfo(name, false, each, elementType);
            }
        }

        return null;
    }

    /// <summary>
    /// Returns all each-block variables visible at the given AST node (innermost first).
    /// Used by completion to offer in-scope variables.
    /// </summary>
    public List<EachVariableInfo> FindEachVariablesInScope(SyntaxNode node)
    {
        var result = new List<EachVariableInfo>();
        var seen = new HashSet<string>();

        for (var current = node; current != null; current = current.Parent)
        {
            if (current is not EachBlockSyntax each) continue;

            if (each.IndexName != null && seen.Add(each.IndexName.Text))
                result.Add(new EachVariableInfo(each.IndexName.Text, true, each, "int"));

            if (each.ValueName != null && seen.Add(each.ValueName.Text))
            {
                string? elementType = ResolveEachElementType(each);
                result.Add(new EachVariableInfo(each.ValueName.Text, false, each, elementType));
            }
        }

        return result;
    }

    /// <summary>
    /// Resolves the element type of an each-block's data source.
    /// For example, if the data source type is <c>NotifyList&lt;ItemData&gt;</c>,
    /// returns <c>"ItemData"</c>.
    /// </summary>
    public string? ResolveEachElementType(EachBlockSyntax each)
    {
        string? sourceType = ResolveExpressionType(each.DataSource);
        if (sourceType == null) return null;
        return ExtractGenericElementType(sourceType);
    }

    /// <summary>
    /// Extracts the first generic type argument from a type string.
    /// E.g. <c>"NotifyList&lt;ItemData&gt;"</c> → <c>"ItemData"</c>.
    /// </summary>
    private static string? ExtractGenericElementType(string typeName)
    {
        int openAngle = typeName.IndexOf('<');
        if (openAngle < 0) return null;
        int closeAngle = typeName.LastIndexOf('>');
        if (closeAngle <= openAngle) return null;
        return typeName[(openAngle + 1)..closeAngle].Trim();
    }

    // ── Semantic analysis ──

    private void AnalyzeDocument(GumlDocumentSyntax doc)
    {
        EnsureImportTypesBuilt();
        AnalyzeComponent(doc.RootComponent);
    }

    /// <summary>
    /// Builds the imported type descriptors from import directives (lazy, called once).
    /// </summary>
    private void EnsureImportTypesBuilt()
    {
        if (_importedTypes != null) return;

        _importedTypes = new Dictionary<string, TypeDescriptor>();
        _importedNames = new HashSet<string>();

        string docPath = PathUtils.UriToFilePath(_document.Uri);
        string? docDir = Path.GetDirectoryName(docPath);

        foreach (var import in _document.Root.Imports)
        {
            string? alias = import.Alias?.Name.Text;
            string pathText = import.Path.Text.Trim('"');
            string derivedName = alias ?? Path.GetFileNameWithoutExtension(pathText);

            if (string.IsNullOrEmpty(derivedName)) continue;
            _importedNames.Add(derivedName);

            // Resolve the absolute path and parse the imported file
            if (docDir == null) continue;
            string resolvedPath = Path.GetFullPath(Path.Combine(docDir, pathText));
            if (!File.Exists(resolvedPath)) continue;

            try
            {
                string importedText = File.ReadAllText(resolvedPath);
                var parseResult = GumlSyntaxTree.Parse(importedText);
                var rootComp = parseResult.Root.RootComponent;

                var typeDesc = BuildImportedTypeDescriptor(derivedName, pathText, rootComp);
                _importedTypes[derivedName] = typeDesc;
            }
            catch (Exception ex)
            {
                // If file read or parse fails, just skip — the name is still in _importedNames
                // so GUML1001 won't fire for it.
                Log.Logger.Debug(ex, "Failed to read/parse imported .guml file: {Path}", resolvedPath);
            }
        }
    }

    /// <summary>
    /// Builds a <see cref="TypeDescriptor"/> from a parsed imported .guml file's root component.
    /// Extracts param declarations as properties and event declarations as events.
    /// </summary>
    private static TypeDescriptor BuildImportedTypeDescriptor(
        string name, string sourcePath, ComponentDeclarationSyntax rootComp)
    {
        var typeDesc = new TypeDescriptor
        {
            Name = name,
            QualifiedName = $"guml:{sourcePath}",
            Kind = GumlTypeKind.Class,
            BaseType = rootComp.TypeName.Text,
            Description = $"Imported GUML component from `{sourcePath}`."
        };

        // Extract doc comment if available
        if (rootComp.DocumentationComment != null)
        {
            string docText = rootComp.DocumentationComment.GetDocumentationText();
            if (!string.IsNullOrWhiteSpace(docText))
                typeDesc.Description = docText;
        }

        foreach (var member in rootComp.Members)
        {
            switch (member)
            {
                case ParameterDeclarationSyntax param:
                    {
                        string description = $"Template parameter (from `{sourcePath}`).";
                        if (param.DocumentationComment != null)
                        {
                            string docText = param.DocumentationComment.GetDocumentationText();
                            if (!string.IsNullOrWhiteSpace(docText))
                                description = docText;
                        }

                        var propDesc = new PropertyDescriptor
                        {
                            Name = param.Name.Text,
                            Type = param.TypeName.Text,
                            Description = description,
                            IsReadable = true,
                            IsWritable = true,
                            Mapping = new MappingConstraintDescriptor
                            {
                                CanStaticMap = true,
                                CanBindDataToProperty = true,
                                CanBindPropertyToData = false,
                                CanBindTwoWay = false
                            }
                        };
                        typeDesc.Properties.TryAdd(param.Name.Text, propDesc);
                        break;
                    }
                case EventDeclarationSyntax eventDecl:
                    {
                        string description = $"Custom event (from `{sourcePath}`).";
                        if (eventDecl.DocumentationComment != null)
                        {
                            string docText = eventDecl.DocumentationComment.GetDocumentationText();
                            if (!string.IsNullOrWhiteSpace(docText))
                                description = docText;
                        }

                        var evtDesc = new EventDescriptor { Name = eventDecl.Name.Text, Description = description };

                        if (eventDecl.Arguments != null)
                        {
                            foreach (var arg in eventDecl.Arguments)
                            {
                                evtDesc.Parameters.Add(new ParameterDescriptor
                                {
                                    Name = arg.Name?.Text ?? "", Type = arg.TypeName.Text
                                });
                            }
                        }

                        typeDesc.Events.TryAdd(eventDecl.Name.Text, evtDesc);
                        break;
                    }
            }
        }

        // Resolve observability: if the template declares a "{param}_changed" event,
        // the corresponding parameter supports =: and <=> bindings.
        foreach (var (propName, propDesc) in typeDesc.Properties)
        {
            if (typeDesc.Events.ContainsKey(propName + "_changed"))
            {
                propDesc.Mapping.IsObservableProperty = true;
                propDesc.Mapping.ObservabilitySource = ObservabilitySource.Signal;
                propDesc.Mapping.CanBindPropertyToData = true;
                propDesc.Mapping.CanBindTwoWay = true;
            }
        }

        return typeDesc;
    }

    private void AnalyzeComponent(ComponentDeclarationSyntax comp)
    {
        string typeName = comp.TypeName.Text;

        // GUML1001: Type not found
        if (!string.IsNullOrEmpty(typeName) && !comp.TypeName.IsMissing)
        {
            // Check if this is an imported component
            bool isImported = _importedNames != null && _importedNames.Contains(typeName);

            if (!isImported)
            {
                var typeDesc = _analyzer.GetTypeInfo(typeName);
                if (typeDesc == null && _analyzer.IsReady)
                {
                    _diagnostics!.Add(new Diagnostic(
                        "GUML1001",
                        $"Type '{typeName}' not found in API metadata.",
                        DiagnosticSeverity.Error,
                        comp.TypeName.Span));
                }
            }
        }

        foreach (var member in comp.Members)
        {
            switch (member)
            {
                case PropertyAssignmentSyntax prop:
                    AnalyzePropertyAssignment(typeName, prop);
                    break;
                case MappingAssignmentSyntax mapping:
                    AnalyzeMappingAssignment(typeName, mapping);
                    break;
                case EventSubscriptionSyntax evt:
                    AnalyzeEventSubscription(typeName, evt);
                    break;
                case ComponentDeclarationSyntax child:
                    AnalyzeComponent(child);
                    break;
                case EachBlockSyntax each:
                    AnalyzeEachBlock(each);
                    break;
                case ParameterDeclarationSyntax param:
                    CheckSnakeCase(param.Name, "GUML3002", "Parameter name");
                    ValidateParamDefault(param);
                    break;
                case EventDeclarationSyntax eventDecl:
                    CheckSnakeCase(eventDecl.Name, "GUML3002", "Event name");
                    break;
                case TemplateParamAssignmentSyntax templateParam:
                    AnalyzeComponent(templateParam.Component);
                    break;
            }
        }
    }

    private void AnalyzePropertyAssignment(string componentType, PropertyAssignmentSyntax prop)
    {
        string propName = prop.Name.Text;

        // GUML3002: snake_case check
        CheckSnakeCase(prop.Name, "GUML3002", "Property name");

        // GUML1002: Property not found
        if (!string.IsNullOrEmpty(propName) && !prop.Name.IsMissing && _analyzer.IsReady)
        {
            // For imported types, validate against imported type descriptor
            var importedType = GetImportedType(componentType);
            if (importedType != null)
            {
                if (!importedType.Properties.ContainsKey(propName))
                {
                    // Fall back to base type inheritance chain
                    var baseDesc = !string.IsNullOrEmpty(importedType.BaseType)
                        ? _analyzer.GetPropertyInfo(importedType.BaseType, propName)
                        : null;
                    if (baseDesc == null)
                    {
                        _diagnostics!.Add(new Diagnostic(
                            "GUML1002",
                            $"Property '{propName}' not found on imported type '{componentType}'.",
                            DiagnosticSeverity.Error,
                            prop.Name.Span));
                    }
                }
            }
            else
            {
                var propDesc = _analyzer.GetPropertyInfo(componentType, propName);
                if (propDesc == null)
                {
                    _diagnostics!.Add(new Diagnostic(
                        "GUML1002",
                        $"Property '{propName}' not found on type '{componentType}'.",
                        DiagnosticSeverity.Error,
                        prop.Name.Span));
                }
            }
        }

        AnalyzeExpression(prop.Value);

        // GUML1010: Assignment type compatibility
        if (_analyzer.IsReady && !string.IsNullOrEmpty(propName) && !prop.Name.IsMissing)
        {
            string? expectedType = null;
            var importedType = GetImportedType(componentType);
            if (importedType != null)
            {
                if (importedType.Properties.TryGetValue(propName, out var iProp))
                    expectedType = iProp.Type;
                else if (!string.IsNullOrEmpty(importedType.BaseType))
                    expectedType = _analyzer.GetPropertyInfo(importedType.BaseType, propName)?.Type;
            }
            else
            {
                expectedType = _analyzer.GetPropertyInfo(componentType, propName)?.Type;
            }

            ValidateAssignmentType(expectedType, prop.Value, prop.Name.Span);
        }
    }

    private void AnalyzeMappingAssignment(string componentType, MappingAssignmentSyntax mapping)
    {
        string propName = mapping.Name.Text;
        CheckSnakeCase(mapping.Name, "GUML3002", "Property name");

        if (!string.IsNullOrEmpty(propName) && !mapping.Name.IsMissing && _analyzer.IsReady)
        {
            // For imported types, validate property exists (mapping constraints not available)
            var importedType = GetImportedType(componentType);
            if (importedType != null)
            {
                if (!importedType.Properties.ContainsKey(propName))
                {
                    // Fall back to base type inheritance chain
                    var baseDesc = !string.IsNullOrEmpty(importedType.BaseType)
                        ? _analyzer.GetPropertyInfo(importedType.BaseType, propName)
                        : null;
                    if (baseDesc == null)
                    {
                        _diagnostics!.Add(new Diagnostic(
                            "GUML1002",
                            $"Property '{propName}' not found on imported type '{componentType}'.",
                            DiagnosticSeverity.Error,
                            mapping.Name.Span));
                    }
                }
            }
            else
            {
                var propDesc = _analyzer.GetPropertyInfo(componentType, propName);
                if (propDesc == null)
                {
                    _diagnostics!.Add(new Diagnostic(
                        "GUML1002",
                        $"Property '{propName}' not found on type '{componentType}'.",
                        DiagnosticSeverity.Error,
                        mapping.Name.Span));
                }
                else
                {
                    // GUML1003: Mapping direction unsupported
                    var constraint = propDesc.Mapping;
                    var opKind = mapping.Operator.Kind;

                    bool supported = opKind switch
                    {
                        SyntaxKind.MapToPropertyToken => constraint.CanBindDataToProperty,
                        SyntaxKind.MapToDataToken => constraint.CanBindPropertyToData,
                        SyntaxKind.MapTwoWayToken => constraint.CanBindTwoWay,
                        _ => true
                    };

                    if (!supported)
                    {
                        string opText = mapping.Operator.Text;
                        _diagnostics!.Add(new Diagnostic(
                            "GUML1003",
                            $"Mapping direction '{opText}' is not supported for property '{propName}' on type '{componentType}'.",
                            DiagnosticSeverity.Error,
                            mapping.Operator.Span));
                    }

                    // GUML1003: For <=> the data source must also be reactive
                    if (opKind == SyntaxKind.MapTwoWayToken && supported)
                    {
                        CheckDataSourceReactivity(mapping);
                    }
                }
            }
        }

        AnalyzeExpression(mapping.Value);

        // GUML1010: Mapping value type compatibility
        if (_analyzer.IsReady && !string.IsNullOrEmpty(propName) && !mapping.Name.IsMissing)
        {
            string? expectedType = null;
            var importedType2 = GetImportedType(componentType);
            if (importedType2 != null)
            {
                if (importedType2.Properties.TryGetValue(propName, out var iProp))
                    expectedType = iProp.Type;
                else if (!string.IsNullOrEmpty(importedType2.BaseType))
                    expectedType = _analyzer.GetPropertyInfo(importedType2.BaseType, propName)?.Type;
            }
            else
            {
                expectedType = _analyzer.GetPropertyInfo(componentType, propName)?.Type;
            }

            ValidateAssignmentType(expectedType, mapping.Value, mapping.Name.Span);
        }
    }

    private void AnalyzeEventSubscription(string componentType, EventSubscriptionSyntax evt)
    {
        string eventName = evt.EventRef.Text;
        if (eventName.StartsWith("#"))
            eventName = eventName[1..];

        // GUML3002: snake_case check for event reference
        if (!string.IsNullOrEmpty(eventName) && !IsSnakeCase(eventName))
        {
            _diagnostics!.Add(new Diagnostic(
                "GUML3002",
                $"Event reference '{eventName}' is not snake_case.",
                DiagnosticSeverity.Warning,
                evt.EventRef.Span));
        }

        // Validate event exists on the component type
        if (!string.IsNullOrEmpty(eventName) && _analyzer.IsReady)
        {
            var importedEvents = GetImportedEvents(componentType);
            if (importedEvents != null)
            {
                if (!importedEvents.ContainsKey(eventName))
                {
                    // Fall back to base type inheritance chain
                    var importedType = GetImportedType(componentType);
                    bool foundInBase = false;
                    if (importedType != null && !string.IsNullOrEmpty(importedType.BaseType))
                    {
                        var baseEvents = _analyzer.GetMergedEvents(importedType.BaseType);
                        if (baseEvents != null && baseEvents.ContainsKey(eventName))
                            foundInBase = true;
                    }
                    if (!foundInBase)
                    {
                        _diagnostics!.Add(new Diagnostic(
                            "GUML1004",
                            $"Event '{eventName}' not found on imported type '{componentType}'.",
                            DiagnosticSeverity.Error,
                            evt.EventRef.Span));
                    }
                }
            }
            else
            {
                var mergedEvents = _analyzer.GetMergedEvents(componentType);
                if (mergedEvents != null && !mergedEvents.ContainsKey(eventName))
                {
                    _diagnostics!.Add(new Diagnostic(
                        "GUML1004",
                        $"Event '{eventName}' not found on type '{componentType}'.",
                        DiagnosticSeverity.Error,
                        evt.EventRef.Span));
                }
            }
        }

        AnalyzeExpression(evt.Handler);
        ValidateEventHandler(componentType, eventName, evt.Handler);
    }

    private void AnalyzeEachBlock(EachBlockSyntax each)
    {
        AnalyzeExpression(each.DataSource);

        if (each.Body != null)
        {
            foreach (var member in each.Body)
            {
                if (member is ComponentDeclarationSyntax child)
                    AnalyzeComponent(child);
            }
        }
    }

    /// <summary>
    /// For <c>&lt;=&gt;</c> bindings, checks that the data source expression also
    /// supports reactive change notifications. The right-hand side must reference a
    /// reactive data source (e.g. a controller implementing INotifyPropertyChanged).
    /// </summary>
    private void CheckDataSourceReactivity(MappingAssignmentSyntax mapping)
    {
        // Only check simple $controller.property patterns
        if (mapping.Value is not MemberAccessExpressionSyntax
            {
                Expression: ReferenceExpressionSyntax refExpr
            })
        {
            return;
        }

        string refName = refExpr.Identifier.Text;
        if (refName != "$controller") return;

        var controller = GetController();
        if (controller == null) return;

        if (!controller.IsReactive)
        {
            _diagnostics!.Add(new Diagnostic(
                "GUML1003",
                $"Two-way binding '<==>' requires the controller '{controller.SimpleName}' to implement INotifyPropertyChanged.",
                DiagnosticSeverity.Warning,
                mapping.Operator.Span));
        }
    }

    private void AnalyzeExpression(ExpressionSyntax expr)
    {
        switch (expr)
        {
            case MemberAccessExpressionSyntax memberAccess:
                CheckSnakeCase(memberAccess.Name, "GUML3001", "Member access name");
                ValidateMemberAccess(memberAccess);
                AnalyzeExpression(memberAccess.Expression);
                break;

            case ReferenceExpressionSyntax refExpr:
                // GUML2001: Undefined named node
                if (refExpr.Identifier.Kind == SyntaxKind.AliasRefToken)
                {
                    var alias = FindAlias(refExpr.Identifier.Text);
                    if (alias == null && _analyzer.IsReady)
                    {
                        _diagnostics!.Add(new Diagnostic(
                            "GUML2001",
                            $"Undefined named node '{refExpr.Identifier.Text}'.",
                            DiagnosticSeverity.Error,
                            refExpr.Identifier.Span));
                    }
                }

                break;
            case LiteralExpressionSyntax:
            case EnumValueExpressionSyntax:
            case ResourceExpressionSyntax:
                // Leaf expressions — no deeper analysis needed for now
                break;

            case BinaryExpressionSyntax binary:
                AnalyzeExpression(binary.Left);
                AnalyzeExpression(binary.Right);
                ValidateBinaryOperator(binary);
                break;

            case PrefixUnaryExpressionSyntax unary:
                AnalyzeExpression(unary.Operand);
                ValidateUnaryOperator(unary);
                break;

            case ConditionalExpressionSyntax cond:
                AnalyzeExpression(cond.Condition);
                AnalyzeExpression(cond.WhenTrue);
                AnalyzeExpression(cond.WhenFalse);
                break;

            case CallExpressionSyntax call:
                AnalyzeExpression(call.Expression);
                foreach (var arg in call.Arguments)
                    AnalyzeExpression(arg);
                ValidateCallExpression(call);
                break;

            case ParenthesizedExpressionSyntax parens:
                AnalyzeExpression(parens.Expression);
                break;

            case ObjectCreationExpressionSyntax objCreate:
                ValidateObjectCreation(objCreate);
                break;

            case ObjectLiteralExpressionSyntax objLit:
                ValidateObjectLiteral(objLit);
                break;

            case ArrayLiteralExpressionSyntax arrLit:
                foreach (var elem in arrLit.Elements)
                    AnalyzeExpression(elem);
                break;

            case DictionaryLiteralExpressionSyntax dictLit:
                foreach (var entry in dictLit.Entries)
                {
                    AnalyzeExpression(entry.Key);
                    AnalyzeExpression(entry.Value);
                }

                break;

            case StructExpressionSyntax structExpr:
                if (structExpr.PositionalArgs != null)
                    foreach (var arg in structExpr.PositionalArgs)
                        AnalyzeExpression(arg);
                if (structExpr.NamedArgs != null)
                    foreach (var prop in structExpr.NamedArgs.Properties)
                        AnalyzeExpression(prop.Value);
                break;

            case TemplateStringExpressionSyntax templateStr:
                foreach (var part in templateStr.Parts)
                    if (part is TemplateStringInterpolationSyntax interp)
                        AnalyzeExpression(interp.Expression);
                break;
        }
    }

    // ── Expression type checking ──

    // ── Assignment & operator type validation ──

    /// <summary>
    /// Validates that the expression type is compatible with the expected property type.
    /// Reports GUML1010 when types are incompatible.
    /// </summary>
    private void ValidateAssignmentType(string? expectedType, ExpressionSyntax value, TextSpan reportSpan)
    {
        if (expectedType == null || !_analyzer.IsReady) return;

        string? exprType = ResolveExpressionType(value);
        if (exprType == null) return;

        if (!IsTypeCompatible(exprType, expectedType))
        {
            _diagnostics!.Add(new Diagnostic(
                "GUML1010",
                $"Expression type '{exprType}' is not compatible with property type '{expectedType}'.",
                DiagnosticSeverity.Error,
                reportSpan));
        }
    }

    /// <summary>
    /// Validates that an event handler is a callable method and its parameter count matches
    /// the event's signal parameters.  Reports GUML1011 / GUML1012.
    /// </summary>
    private void ValidateEventHandler(
        string componentType, string eventName, ExpressionSyntax handler)
    {
        var controller = GetController();
        if (controller == null) return;

        // Only validate $controller.method form
        if (handler is not MemberAccessExpressionSyntax memberAccess) return;
        if (memberAccess.Expression is not ReferenceExpressionSyntax refExpr) return;
        if (refExpr.Identifier.Kind != SyntaxKind.GlobalRefToken
            || refExpr.Identifier.Text != "$controller") return;

        string handlerName = memberAccess.Name.Text;
        if (string.IsNullOrEmpty(handlerName) || memberAccess.Name.IsMissing) return;

        string pascalName = KeyConverter.ToPascalCase(handlerName);

        // GUML1011: handler should be a method
        var method = controller.Methods.Find(m => m.Name == pascalName);
        if (method == null)
        {
            var prop = controller.Properties.Find(p => p.Name == pascalName);
            if (prop != null)
            {
                _diagnostics!.Add(new Diagnostic(
                    "GUML1011",
                    $"Event '{eventName}' handler '{handlerName}' is a property, not a callable method.",
                    DiagnosticSeverity.Error,
                    memberAccess.Name.Span));
            }

            return;
        }

        // GUML1012: handler parameter count vs event parameter count
        EventDescriptor? eventDesc = null;
        var importedEvents = GetImportedEvents(componentType);
        if (importedEvents != null)
            importedEvents.TryGetValue(eventName, out eventDesc);
        else
        {
            var mergedEvents = _analyzer.GetMergedEvents(componentType);
            mergedEvents?.TryGetValue(eventName, out eventDesc);
        }

        if (eventDesc != null && method.Parameters.Count != eventDesc.Parameters.Count)
        {
            _diagnostics!.Add(new Diagnostic(
                "GUML1012",
                $"Event '{eventName}' expects {eventDesc.Parameters.Count} parameter(s), but handler '{handlerName}' has {method.Parameters.Count}.",
                DiagnosticSeverity.Error,
                memberAccess.Name.Span));
        }
    }

    /// <summary>
    /// Validates that the unary operator is applicable to the operand type.
    /// Reports GUML1013.
    /// </summary>
    private void ValidateUnaryOperator(PrefixUnaryExpressionSyntax unary)
    {
        string? operandType = ResolveExpressionType(unary.Operand);
        if (operandType == null) return;

        string norm = NormalizeTypeName(operandType);
        bool valid = unary.OperatorToken.Kind switch
        {
            SyntaxKind.BangToken => norm == "bool",
            SyntaxKind.PlusToken or SyntaxKind.MinusToken => IsNumeric(norm),
            _ => true
        };

        if (!valid)
        {
            _diagnostics!.Add(new Diagnostic(
                "GUML1013",
                $"Operator '{unary.OperatorToken.Text}' cannot be applied to operand of type '{operandType}'.",
                DiagnosticSeverity.Error,
                unary.OperatorToken.Span));
        }
    }

    /// <summary>
    /// Validates that the binary operator is applicable to both operand types.
    /// Reports GUML1014.
    /// </summary>
    private void ValidateBinaryOperator(BinaryExpressionSyntax binary)
    {
        string? leftType = ResolveExpressionType(binary.Left);
        string? rightType = ResolveExpressionType(binary.Right);
        if (leftType == null || rightType == null) return;

        string left = NormalizeTypeName(leftType);
        string right = NormalizeTypeName(rightType);
        var op = binary.OperatorToken.Kind;

        bool valid = true;
        if (op is SyntaxKind.AmpersandAmpersandToken or SyntaxKind.BarBarToken)
        {
            valid = left == "bool" && right == "bool";
        }
        else if (op is SyntaxKind.LessThanToken or SyntaxKind.GreaterThanToken
                 or SyntaxKind.LessThanEqualsToken or SyntaxKind.GreaterThanEqualsToken)
        {
            valid = IsNumeric(left) && IsNumeric(right);
        }
        else if (op == SyntaxKind.PlusToken)
        {
            valid = (IsNumeric(left) && IsNumeric(right))
                    || left == "string" || right == "string";
        }
        else if (op is SyntaxKind.MinusToken or SyntaxKind.AsteriskToken
                 or SyntaxKind.SlashToken or SyntaxKind.PercentToken)
        {
            valid = IsNumeric(left) && IsNumeric(right);
        }

        if (!valid)
        {
            _diagnostics!.Add(new Diagnostic(
                "GUML1014",
                $"Operator '{binary.OperatorToken.Text}' cannot be applied to operands of type '{leftType}' and '{rightType}'.",
                DiagnosticSeverity.Error,
                binary.OperatorToken.Span));
        }
    }

    /// <summary>
    /// Validates that a param's default value type is compatible with the declared type.
    /// Reports GUML1015.
    /// </summary>
    private void ValidateParamDefault(ParameterDeclarationSyntax param)
    {
        if (param.DefaultValue == null) return;

        AnalyzeExpression(param.DefaultValue);

        string declaredType = param.TypeName.Text;
        if (string.IsNullOrEmpty(declaredType)) return;

        string? exprType = ResolveExpressionType(param.DefaultValue);
        if (exprType == null) return;

        if (!IsTypeCompatible(exprType, declaredType))
        {
            _diagnostics!.Add(new Diagnostic(
                "GUML1015",
                $"Default value type '{exprType}' is not compatible with param type '{declaredType}'.",
                DiagnosticSeverity.Error,
                param.DefaultValue.Span));
        }
    }

    /// <summary>
    /// Validates an object literal expression.
    /// When the literal is the value of a property whose type resolves to a known
    /// <see cref="TypeDescriptor"/> (e.g. a synthetic <c>Button$ThemeOverrides</c> type),
    /// inner keys are validated against that type's properties and value types are checked.
    /// </summary>
    private void ValidateObjectLiteral(ObjectLiteralExpressionSyntax objLit)
    {
        // Resolve the expected type of this object literal from the parent assignment
        TypeDescriptor? targetType = ResolveObjectLiteralTargetType(objLit);

        foreach (var prop in objLit.Properties)
        {
            AnalyzeExpression(prop.Value);

            if (targetType != null && _analyzer.IsReady && !prop.Name.IsMissing)
            {
                string key = prop.Name.Text;
                if (targetType.Properties.TryGetValue(key, out var propDesc))
                {
                    ValidateAssignmentType(propDesc.Type, prop.Value, prop.Name.Span);
                }
                else
                {
                    _diagnostics!.Add(new Diagnostic(
                        "GUML1002",
                        $"Key '{key}' is not a valid entry in '{targetType.Name}'.",
                        DiagnosticSeverity.Error,
                        prop.Name.Span));
                }
            }
        }
    }

    /// <summary>
    /// Resolves the <see cref="TypeDescriptor"/> that an object literal's keys should be
    /// validated against. Returns <c>null</c> when the context cannot be determined or
    /// the target type is not registered in the API document.
    /// </summary>
    private TypeDescriptor? ResolveObjectLiteralTargetType(ObjectLiteralExpressionSyntax objLit)
    {
        // Walk up to the parent PropertyAssignment
        if (objLit.Parent is not PropertyAssignmentSyntax assignment) return null;

        string propName = assignment.Name.Text;

        // Find the enclosing component type
        string? componentType = null;
        for (var current = assignment.Parent; current != null; current = current.Parent)
        {
            if (current is ComponentDeclarationSyntax comp)
            {
                componentType = comp.TypeName.Text;
                break;
            }
        }

        if (componentType == null) return null;

        // Resolve the property type from the API
        var propDesc = _analyzer.GetPropertyInfo(componentType, propName);
        if (propDesc == null) return null;

        // Look up the target type in the API document
        return _analyzer.GetTypeInfo(propDesc.Type);
    }

    /// <summary>
    /// Validates property assignments inside an object creation expression
    /// against the target type's property descriptors.
    /// </summary>
    private void ValidateObjectCreation(ObjectCreationExpressionSyntax objCreate)
    {
        string typeName = objCreate.TypeName.Text;
        var typeDesc = !string.IsNullOrEmpty(typeName) ? _analyzer.GetTypeInfo(typeName) : null;

        foreach (var prop in objCreate.Properties)
        {
            AnalyzeExpression(prop.Value);

            if (typeDesc != null
                && typeDesc.Properties.TryGetValue(prop.Name.Text, out var propDesc))
            {
                ValidateAssignmentType(propDesc.Type, prop.Value, prop.Name.Span);
            }
        }
    }

    /// <summary>
    /// Returns true if the (already-normalized) type name is a numeric type.
    /// </summary>
    private static bool IsNumeric(string normalizedType) =>
        normalizedType is "int" or "float" or "double" or "long";

    /// <summary>
    /// Resolves the inferred type of an expression, or null if the type cannot be determined.
    /// </summary>
    private string? ResolveExpressionType(ExpressionSyntax expression)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax literal:
                return literal.Token.Kind switch
                {
                    SyntaxKind.StringLiteralToken => "string",
                    SyntaxKind.IntegerLiteralToken => "int",
                    SyntaxKind.FloatLiteralToken => "float",
                    SyntaxKind.TrueLiteralToken => "bool",
                    SyntaxKind.FalseLiteralToken => "bool",
                    SyntaxKind.NullLiteralToken => "null",
                    _ => null
                };

            case ReferenceExpressionSyntax refExpr:
                if (refExpr.Identifier.Text == "$controller")
                {
                    var ctrl = GetController();
                    return ctrl?.FullName;
                }

                // $root — returns the root component's base type
                if (refExpr.Identifier.Text == "$root")
                    return _document.Root.RootComponent.TypeName.Text;

                // Check named node aliases (@xxx)
                if (refExpr.Identifier.Kind == SyntaxKind.AliasRefToken)
                {
                    var alias = FindAlias(refExpr.Identifier.Text);
                    return alias?.TypeName;
                }

                // Check each-block local variables
                var eachVar = FindEachVariable(refExpr.Identifier);
                if (eachVar != null) return eachVar.ResolvedType;

                return null;

            case MemberAccessExpressionSyntax memberAccess:
                return ResolveMemberAccessType(memberAccess);

            case CallExpressionSyntax call:
                return ResolveCallReturnType(call);

            case BinaryExpressionSyntax binary:
                return ResolveBinaryExpressionType(binary);

            case PrefixUnaryExpressionSyntax unary:
                return unary.OperatorToken.Kind switch
                {
                    SyntaxKind.BangToken => "bool",
                    SyntaxKind.PlusToken or SyntaxKind.MinusToken => ResolveExpressionType(unary.Operand),
                    _ => null
                };

            case ConditionalExpressionSyntax cond:
                {
                    string? trueType = ResolveExpressionType(cond.WhenTrue);
                    string? falseType = ResolveExpressionType(cond.WhenFalse);
                    if (trueType == null) return falseType;
                    if (falseType == null) return trueType;
                    if (IsTypeCompatible(trueType, falseType)) return falseType;
                    return trueType;
                }

            case ParenthesizedExpressionSyntax parens:
                return ResolveExpressionType(parens.Expression);

            case StructExpressionSyntax structExpr:
                return structExpr.TypeName.Text;

            case ResourceExpressionSyntax resource:
                return resource.Keyword.Kind switch
                {
                    SyntaxKind.ImageKeyword => "Texture2D",
                    SyntaxKind.FontKeyword => "Font",
                    SyntaxKind.AudioKeyword => "AudioStream",
                    SyntaxKind.VideoKeyword => "VideoStream",
                    _ => null
                };

            case TemplateStringExpressionSyntax:
                return "string";

            case EnumValueExpressionSyntax:
                // Enum type is determined by context (property type), not the expression itself
                return null;

            default:
                return null;
        }
    }

    /// <summary>
    /// Resolves the type of a member access expression, supporting chained access
    /// (e.g. <c>$controller.a.b.c</c> or <c>item.name</c>).
    /// </summary>
    private string? ResolveMemberAccessType(MemberAccessExpressionSyntax memberAccess)
    {
        var chain = TryResolveControllerChain(memberAccess);
        if (chain != null) return ResolveChainType(chain);

        string? rootResult = ResolveRootMemberAccess(memberAccess);
        if (rootResult != null) return rootResult;

        string? aliasResult = ResolveAliasMemberAccess(memberAccess);
        if (aliasResult != null) return aliasResult;

        return ResolveEachVariableMemberAccess(memberAccess);
    }

    /// <summary>
    /// Resolves type of a member access rooted in <c>$root</c>
    /// (e.g. <c>$root.text</c> or <c>$root.some_prop.sub</c>).
    /// First checks param/event declarations, then falls back to the root component's base type properties.
    /// </summary>
    private string? ResolveRootMemberAccess(MemberAccessExpressionSyntax memberAccess)
    {
        var segments = new List<string>();
        ExpressionSyntax current = memberAccess;
        while (current is MemberAccessExpressionSyntax ma)
        {
            segments.Add(ma.Name.Text);
            current = ma.Expression;
        }

        if (current is not ReferenceExpressionSyntax { Identifier.Text: "$root" })
            return null;

        segments.Reverse();
        if (segments.Count == 0) return null;

        // First segment: look up param declarations on the root component
        string firstName = segments[0];
        string? currentType = null;

        var rootComp = _document.Root.RootComponent;
        foreach (var member in rootComp.Members)
        {
            if (member is ParameterDeclarationSyntax param && param.Name.Text == firstName)
            {
                currentType = param.TypeName.Text;
                break;
            }
        }

        // Fallback: check the root component's base type properties
        if (currentType == null)
        {
            string rootType = rootComp.TypeName.Text;
            currentType = ResolvePropertyType(rootType, firstName);
        }

        if (currentType == null) return null;

        // Resolve subsequent segments via type system
        for (int i = 1; i < segments.Count; i++)
        {
            currentType = ResolvePropertyType(currentType, segments[i]);
            if (currentType == null) return null;
        }

        return currentType;
    }

    /// <summary>
    /// Resolves type of a member access rooted in a named node alias
    /// (e.g. <c>@my_label.text</c> or <c>@my_node.a.b</c>).
    /// </summary>
    private string? ResolveAliasMemberAccess(MemberAccessExpressionSyntax memberAccess)
    {
        var segments = new List<string>();
        ExpressionSyntax current = memberAccess;
        while (current is MemberAccessExpressionSyntax ma)
        {
            segments.Add(ma.Name.Text);
            current = ma.Expression;
        }

        if (current is not ReferenceExpressionSyntax { Identifier.Kind: SyntaxKind.AliasRefToken } refExpr)
            return null;

        var alias = FindAlias(refExpr.Identifier.Text);
        if (alias == null) return null;

        string? currentType = alias.TypeName;
        segments.Reverse();
        foreach (string segName in segments)
        {
            currentType = ResolvePropertyType(currentType, segName);
            if (currentType == null) return null;
        }

        return currentType;
    }

    /// <summary>
    /// Resolves type of a member access rooted in an each-block variable
    /// (e.g. <c>item.name</c> or <c>item.sub.field</c>).
    /// </summary>
    private string? ResolveEachVariableMemberAccess(MemberAccessExpressionSyntax memberAccess)
    {
        var segments = new List<string>();
        ExpressionSyntax current = memberAccess;
        while (current is MemberAccessExpressionSyntax ma)
        {
            segments.Add(ma.Name.Text);
            current = ma.Expression;
        }

        if (current is not ReferenceExpressionSyntax refExpr) return null;

        var eachVar = FindEachVariable(refExpr.Identifier);
        if (eachVar == null) return null;

        string? currentType = eachVar.ResolvedType;
        if (currentType == null) return null;

        segments.Reverse();
        foreach (string segName in segments)
        {
            currentType = ResolvePropertyType(currentType, segName);
            if (currentType == null) return null;
        }

        return currentType;
    }

    /// <summary>
    /// Resolves the return type of a call expression by looking up the target method.
    /// </summary>
    private string? ResolveCallReturnType(CallExpressionSyntax call)
    {
        var method = ResolveCallTarget(call);
        return method?.ReturnType;
    }

    /// <summary>
    /// Resolves the <see cref="MethodDescriptor"/> targeted by a call expression,
    /// or null if the target cannot be determined.
    /// Supports chained access (e.g. <c>$controller.a.method()</c>).
    /// </summary>
    private MethodDescriptor? ResolveCallTarget(CallExpressionSyntax call)
    {
        if (call.Expression is not MemberAccessExpressionSyntax memberAccess)
            return null;

        var chain = TryResolveControllerChain(memberAccess);
        if (chain == null || chain.Count == 0) return null;

        // Single segment: $controller.method()
        if (chain.Count == 1)
        {
            var controller = GetController();
            if (controller == null) return null;
            string pascalName = KeyConverter.ToPascalCase(chain[0].Name);
            return controller.Methods.Find(m => m.Name == pascalName);
        }

        // Multi-segment: resolve intermediate types, last segment is the method
        string? currentType = null;
        var ctrl = GetController();
        if (ctrl == null) return null;

        for (int i = 0; i < chain.Count - 1; i++)
        {
            string segmentPascal = i == 0
                ? KeyConverter.ToPascalCase(chain[i].Name)
                : chain[i].Name;

            if (i == 0)
            {
                var prop = ctrl.Properties.Find(p => p.Name == segmentPascal);
                currentType = prop?.Type;
            }
            else
            {
                currentType = ResolvePropertyType(currentType, segmentPascal);
            }

            if (currentType == null) return null;
        }

        // Last segment: look up method on resolved type
        var typeDesc = _analyzer.GetTypeInfo(currentType!);
        if (typeDesc == null) return null;

        // Godot types don't have methods in our API, so this returns null for now
        return null;
    }

    /// <summary>
    /// Validates a named node alias member access chain (e.g. <c>@my_label.text</c>).
    /// Reports GUML1009 when a member is not found on the resolved type, and
    /// GUML2001 when the alias itself is undefined.
    /// </summary>
    private void ValidateAliasMemberAccess(MemberAccessExpressionSyntax memberAccess)
    {
        // Only validate the leaf (this node's Name token).
        // Parent segments are validated when their own MemberAccessExpressionSyntax is visited.

        // Walk to find the root of the chain
        ExpressionSyntax root = memberAccess.Expression;
        while (root is MemberAccessExpressionSyntax inner)
            root = inner.Expression;

        if (root is not ReferenceExpressionSyntax { Identifier.Kind: SyntaxKind.AliasRefToken } refExpr)
            return;

        var alias = FindAlias(refExpr.Identifier.Text);
        if (alias == null) return; // GUML2001 for undefined alias is reported elsewhere

        // Resolve the type of the parent expression
        string? parentType = ResolveExpressionType(memberAccess.Expression);
        if (parentType == null) return;

        string memberName = memberAccess.Name.Text;
        if (string.IsNullOrEmpty(memberName) || memberAccess.Name.IsMissing) return;

        // Skip if the API cache is not ready or the type is not a known SDK/project type.
        // This prevents false GUML1009 diagnostics when:
        //   - The Roslyn project analysis has not completed (IsReady = false).
        //   - The aliased node's type is an imported GUML component, which is not
        //     registered in the API document and therefore has no property metadata.
        if (!_analyzer.IsReady || !_analyzer.IsTypeKnown(parentType)) return;

        if (!HasMember(parentType, memberName))
        {
            _diagnostics!.Add(new Diagnostic(
                "GUML1009",
                $"Member '{memberName}' not found on type '{parentType}'.",
                DiagnosticSeverity.Error,
                memberAccess.Name.Span));
        }
    }

    /// <summary>
    /// Resolves the result type of a binary expression following §5.5 numeric promotion rules.
    /// </summary>
    private string? ResolveBinaryExpressionType(BinaryExpressionSyntax binary)
    {
        var opKind = binary.OperatorToken.Kind;

        // Comparison and logical operators always produce bool
        if (opKind is SyntaxKind.EqualsEqualsToken or SyntaxKind.BangEqualsToken
            or SyntaxKind.LessThanToken or SyntaxKind.GreaterThanToken
            or SyntaxKind.LessThanEqualsToken or SyntaxKind.GreaterThanEqualsToken
            or SyntaxKind.AmpersandAmpersandToken or SyntaxKind.BarBarToken)
        {
            return "bool";
        }

        // Arithmetic operators: numeric type promotion
        string? leftType = ResolveExpressionType(binary.Left);
        string? rightType = ResolveExpressionType(binary.Right);

        if (leftType == null || rightType == null) return leftType ?? rightType;

        // If either side is float/double, promote to float
        if (IsFloatingPoint(leftType) || IsFloatingPoint(rightType))
            return "float";

        // int + int = int
        if (NormalizeTypeName(leftType) == "int" && NormalizeTypeName(rightType) == "int")
            return "int";

        // string + anything = string (concatenation)
        if (opKind == SyntaxKind.PlusToken &&
            (NormalizeTypeName(leftType) == "string" || NormalizeTypeName(rightType) == "string"))
            return "string";

        return leftType;
    }

    /// <summary>
    /// Validates a <c>$controller.member</c> or chained <c>$controller.a.b</c> access.
    /// Reports GUML1009 when a member name is not found at any level.
    /// </summary>
    private void ValidateMemberAccess(MemberAccessExpressionSyntax memberAccess)
    {
        // Validate @alias.member chains
        ValidateAliasMemberAccess(memberAccess);

        var chain = TryResolveControllerChain(memberAccess);
        if (chain == null) return;

        var controller = GetController();
        if (controller == null) return;

        // Only validate the final segment of the chain (the one belonging to this memberAccess node).
        // Parent segments are validated when their own MemberAccessExpressionSyntax is visited.
        var (memberName, memberToken) = chain[^1];
        if (string.IsNullOrEmpty(memberName) || memberToken.IsMissing) return;

        if (chain.Count == 1)
        {
            // Direct controller member: $controller.xxx
            string pascalName = KeyConverter.ToPascalCase(memberName);
            bool found = controller.Properties.Exists(p => p.Name == pascalName)
                         || controller.Methods.Exists(m => m.Name == pascalName);

            if (!found)
            {
                _diagnostics!.Add(new Diagnostic(
                    "GUML1009",
                    $"Member '{memberName}' not found on controller '{controller.SimpleName}'.",
                    DiagnosticSeverity.Error,
                    memberToken.Span));
            }
        }
        else
        {
            // Chained access: resolve intermediate types up to chain[^2], then check chain[^1]
            string? currentType = null;
            for (int i = 0; i < chain.Count - 1; i++)
            {
                string segName = chain[i].Name;
                string segPascal = i == 0 ? KeyConverter.ToPascalCase(segName) : segName;

                if (i == 0)
                {
                    var prop = controller.Properties.Find(p => p.Name == segPascal);
                    currentType = prop?.Type;
                }
                else
                {
                    currentType = ResolvePropertyType(currentType, segPascal);
                }

                if (currentType == null) return; // Can't resolve intermediate — skip validation
            }

            // Check the final member on the resolved type
            string finalPropName = memberName; // Already in Godot snake_case for non-controller types
            if (!HasMember(currentType, finalPropName))
            {
                _diagnostics!.Add(new Diagnostic(
                    "GUML1009",
                    $"Member '{memberName}' not found on type '{currentType}'.",
                    DiagnosticSeverity.Error,
                    memberToken.Span));
            }
        }
    }

    /// <summary>
    /// Validates a method call expression against the controller's method descriptors.
    /// Reports diagnostics for unknown methods (GUML1006), wrong arity (GUML1007),
    /// or type mismatches (GUML1008). Supports chained access.
    /// </summary>
    private void ValidateCallExpression(CallExpressionSyntax call)
    {
        // Only validate $controller.method(...) or $controller.a.method(...) calls
        if (call.Expression is not MemberAccessExpressionSyntax memberAccess)
            return;

        var chain = TryResolveControllerChain(memberAccess);
        if (chain == null || chain.Count == 0) return;

        var controller = GetController();
        if (controller == null) return;

        string methodName = chain[^1].Name;
        if (string.IsNullOrEmpty(methodName) || chain[^1].Token.IsMissing) return;

        // Only validate direct $controller.method() for now (chained call targets
        // like $controller.a.method() can't be validated without method info on types)
        if (chain.Count != 1) return;

        string pascalName = KeyConverter.ToPascalCase(methodName);

        // GUML1006: Method not found
        var method = controller.Methods.Find(m => m.Name == pascalName);
        if (method == null)
        {
            // Report only when the member exists as a property (not callable).
            // The case where the member doesn't exist at all is handled by
            // ValidateMemberAccess (GUML1009).
            var prop = controller.Properties.Find(p => p.Name == pascalName);
            if (prop != null)
            {
                _diagnostics!.Add(new Diagnostic(
                    "GUML1006",
                    $"'{methodName}' is a property, not a callable method, on controller '{controller.SimpleName}'.",
                    DiagnosticSeverity.Error,
                    memberAccess.Name.Span));
            }

            return;
        }

        // GUML1007: Argument count mismatch
        int expected = method.Parameters.Count;
        int actual = call.Arguments.Count;
        if (expected != actual)
        {
            _diagnostics!.Add(new Diagnostic(
                "GUML1007",
                $"Method '{methodName}' expects {expected} argument(s), but {actual} were provided.",
                DiagnosticSeverity.Error,
                call.OpenParen.Span));
            return;
        }

        // GUML1008: Argument type mismatch (warning)
        for (int i = 0; i < actual; i++)
        {
            var argExpr = call.Arguments[i];
            string? argType = ResolveExpressionType(argExpr);
            if (argType == null) continue; // Cannot infer — skip

            string paramType = method.Parameters[i].Type;
            if (!IsTypeCompatible(argType, paramType))
            {
                _diagnostics!.Add(new Diagnostic(
                    "GUML1008",
                    $"Argument type '{argType}' may not be compatible with parameter type '{paramType}'.",
                    DiagnosticSeverity.Warning,
                    argExpr.Span));
            }
        }
    }

    // ── Controller chain resolution ──

    /// <summary>
    /// Walks up nested <see cref="MemberAccessExpressionSyntax"/> nodes until a
    /// <c>$controller</c> reference is found. Returns the full member chain in
    /// access order (e.g. ["a", "b", "c"] for <c>$controller.a.b.c</c>),
    /// or null if the root is not <c>$controller</c>.
    /// </summary>
    private static List<(string Name, SyntaxToken Token)>? TryResolveControllerChain(
        MemberAccessExpressionSyntax memberAccess)
    {
        var segments = new List<(string Name, SyntaxToken Token)>();
        ExpressionSyntax current = memberAccess;

        while (current is MemberAccessExpressionSyntax ma)
        {
            segments.Add((ma.Name.Text, ma.Name));
            current = ma.Expression;
        }

        if (current is not ReferenceExpressionSyntax { Identifier.Text: "$controller" })
            return null;

        segments.Reverse(); // from outermost to innermost → access order
        return segments;
    }

    /// <summary>
    /// Given a chain of member names starting from <c>$controller</c>, resolves the
    /// type at each step, returning the final resolved type name or null.
    /// </summary>
    private string? ResolveChainType(List<(string Name, SyntaxToken Token)> chain)
    {
        var controller = GetController();
        if (controller == null) return null;

        string? currentType = null;
        for (int i = 0; i < chain.Count; i++)
        {
            string segName = chain[i].Name;

            if (i == 0)
            {
                // First segment: look up on controller (PascalCase)
                string pascalName = KeyConverter.ToPascalCase(segName);
                var prop = controller.Properties.Find(p => p.Name == pascalName);
                if (prop != null)
                {
                    currentType = prop.Type;
                    continue;
                }

                var method = controller.Methods.Find(m => m.Name == pascalName);
                if (method != null) return i == chain.Count - 1 ? $"method:{method.ReturnType}" : null;

                return null;
            }

            // Subsequent segments: look up on the resolved type (Godot uses snake_case)
            if (currentType == null) return null;
            string? propType = ResolvePropertyType(currentType, segName);
            if (propType != null)
            {
                currentType = propType;
                continue;
            }

            return null; // Member not found on intermediate type
        }

        return currentType;
    }

    /// <summary>
    /// Resolves a property type on a known API type (walking the inheritance chain).
    /// Tries the original name first, then PascalCase fallback for C# types.
    /// </summary>
    private string? ResolvePropertyType(string? typeName, string propertyName)
    {
        if (typeName == null) return null;
        var propDesc = _analyzer.GetPropertyInfo(typeName, propertyName);
        if (propDesc != null) return propDesc.Type;

        // Fallback: GUML uses snake_case, but C# types store PascalCase
        string pascalName = KeyConverter.ToPascalCase(propertyName);
        if (pascalName != propertyName)
        {
            propDesc = _analyzer.GetPropertyInfo(typeName, pascalName);
            if (propDesc != null) return propDesc.Type;
        }

        return null;
    }

    /// <summary>
    /// Checks whether a type has a property or event with the given name.
    /// Tries the original name first, then PascalCase fallback for C# types.
    /// </summary>
    private bool HasMember(string? typeName, string memberName)
    {
        if (typeName == null) return false;
        var props = _analyzer.GetMergedProperties(typeName);
        var events = _analyzer.GetMergedEvents(typeName);

        if (props != null && props.ContainsKey(memberName)) return true;
        if (events != null && events.ContainsKey(memberName)) return true;

        // Fallback: GUML uses snake_case, but C# types store PascalCase
        string pascalName = KeyConverter.ToPascalCase(memberName);
        if (pascalName != memberName)
        {
            if (props != null && props.ContainsKey(pascalName)) return true;
            if (events != null && events.ContainsKey(pascalName)) return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether a source type is assignable to a target type
    /// following GUML's implicit conversion rules (§5.5).
    /// </summary>
    internal static bool IsTypeCompatible(string sourceType, string targetType)
    {
        string src = NormalizeTypeName(sourceType);
        string tgt = NormalizeTypeName(targetType);

        // Case-insensitive comparison after normalization
        if (string.Equals(src, tgt, StringComparison.OrdinalIgnoreCase)) return true;

        // null is compatible with any reference type
        if (src == "null") return true;

        // int → float / double promotion
        if (src == "int" && tgt is "float" or "double") return true;

        // float → double promotion
        if (src == "float" && tgt == "double") return true;

        // string ↔ StringName / NodePath interop
        if (src == "string" && tgt is "StringName" or "NodePath") return true;
        if (tgt == "string" && src is "StringName" or "NodePath") return true;

        // method:ReturnType sentinel — extract and compare return type
        if (src.StartsWith("method:"))
        {
            string returnType = src["method:".Length..];
            return IsTypeCompatible(returnType, targetType);
        }

        // Variant accepts anything
        if (tgt == "variant") return true;

        return false;
    }

    /// <summary>
    /// Normalizes CLR type name aliases to their short forms for comparison.
    /// </summary>
    internal static string NormalizeTypeName(string typeName)
    {
        return typeName switch
        {
            "Int32" or "System.Int32" or "integer" => "int",
            "Int64" or "System.Int64" => "long",
            "Single" or "System.Single" => "float",
            "Double" or "System.Double" => "double",
            "Boolean" or "System.Boolean" or "boolean" => "bool",
            "String" or "System.String" => "string",
            "Void" or "System.Void" => "void",
            "Object" or "System.Object" => "object",
            _ => typeName
        };
    }

    /// <summary>
    /// Returns true if the given type name represents a floating-point numeric type.
    /// </summary>
    private static bool IsFloatingPoint(string typeName)
    {
        string normalized = NormalizeTypeName(typeName);
        return normalized is "float" or "double";
    }

    // ── Naming checks ──

    private void CheckSnakeCase(SyntaxToken token, string diagnosticId, string description)
    {
        string name = token.Text;
        if (string.IsNullOrEmpty(name) || token.IsMissing) return;

        if (!IsSnakeCase(name))
        {
            _diagnostics!.Add(new Diagnostic(
                diagnosticId,
                $"{description} '{name}' is not snake_case.",
                DiagnosticSeverity.Warning,
                token.Span));
        }
    }

    private static bool IsSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return true;
        // snake_case: lowercase letters, digits, underscores; no leading/trailing underscores;
        // no consecutive underscores; must start with a letter
        if (!char.IsLower(name[0]) && name[0] != '_') return false;
        return name.All(c => c == '_' || char.IsLower(c) || char.IsDigit(c));
    }

}

