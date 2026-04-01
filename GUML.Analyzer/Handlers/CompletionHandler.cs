using GUML.Analyzer.Utils;
using GUML.Analyzer.Workspace;
using GUML.Shared.Api;
using GUML.Shared.Converter;
using GUML.Shared.Syntax;
using GUML.Shared.Syntax.Nodes;
using GUML.Shared.Syntax.Nodes.Expressions;

namespace GUML.Analyzer.Handlers;

/// <summary>
/// Provides completion items based on the cursor position within a GUML document.
/// </summary>
public static class CompletionHandler
{
    /// <summary>
    /// Returns completion items for the given document position.
    /// </summary>
    public static List<CompletionItem> GetCompletions(
        GumlDocument document, SemanticModel? semanticModel, LspPosition position,
        ProjectAnalyzer analyzer)
    {
        var items = new List<CompletionItem>();
        var mapper = new PositionMapper(document.Text);
        int offset = mapper.GetOffset(position);

        var token = document.Root.FindToken(offset);
        if (token == null) return items;

        var node = token.Parent;
        if (node == null) return items;

        // Determine completion context by walking up to the nearest meaningful parent
        var context = DetermineContext(node, token, offset);

        switch (context)
        {
            case CompletionContext.ComponentBody:
                AddComponentBodyCompletions(items, node, analyzer, semanticModel);
                break;
            case CompletionContext.PropertyValue:
                AddPropertyValueCompletions(items, node, analyzer, semanticModel);
                break;
            case CompletionContext.ObjectLiteralBody:
                AddObjectLiteralBodyCompletions(items, node, analyzer, semanticModel);
                break;
            case CompletionContext.ComponentName:
                AddComponentNameCompletions(items, document, analyzer);
                break;
            case CompletionContext.MemberAccess:
                AddMemberAccessCompletions(items, node, analyzer, semanticModel);
                break;
            case CompletionContext.EventRef:
                AddEventRefCompletions(items, node, analyzer, semanticModel);
                break;
            case CompletionContext.ImportPath:
                // File path completion is context-dependent on workspace; omitted for now
                break;
        }

        return items;
    }

    private enum CompletionContext
    {
        ComponentBody,
        PropertyValue,
        ComponentName,
        MemberAccess,
        EventRef,
        ImportPath,
        ObjectLiteralBody,
    }

    private static CompletionContext DetermineContext(SyntaxNode node, SyntaxToken token, int offset)
    {
        // Walk up to find the most informative ancestor
        for (var current = node; current != null; current = current.Parent)
        {
            switch (current)
            {
                // Inside an object literal body => offer keys from the target type
                case ObjectLiteralExpressionSyntax objLit:
                    if (offset > objLit.OpenBrace.Span.End && offset <= objLit.CloseBrace.Span.Start)
                        return CompletionContext.ObjectLiteralBody;
                    break;

                // Inside a component body => offer properties, signals, child components, keywords
                case ComponentDeclarationSyntax comp:
                    // If cursor is right at the component name position, offer component names
                    if (token == comp.TypeName || token.Kind == SyntaxKind.ComponentNameToken)
                        return CompletionContext.ComponentName;
                    // If inside the body braces
                    if (offset > comp.OpenBrace.Span.End && offset <= comp.CloseBrace.Span.Start)
                        return CompletionContext.ComponentBody;
                    break;

                case PropertyAssignmentSyntax:
                case MappingAssignmentSyntax:
                    return CompletionContext.PropertyValue;

                case MemberAccessExpressionSyntax:
                    return CompletionContext.MemberAccess;

                case EventSubscriptionSyntax:
                    if (token.Kind == SyntaxKind.EventRefToken)
                        return CompletionContext.EventRef;
                    return CompletionContext.PropertyValue;

                case ImportDirectiveSyntax:
                    return CompletionContext.ImportPath;
            }
        }

        // Default: if at top level, offer component names
        return CompletionContext.ComponentName;
    }

    private static void AddComponentBodyCompletions(
        List<CompletionItem> items, SyntaxNode node, ProjectAnalyzer analyzer, SemanticModel? semanticModel)
    {
        // Find the enclosing ComponentDeclarationSyntax
        string? componentType = HandlerUtils.FindEnclosingComponentType(node);

        // Add property/signal completions from the component type
        if (componentType != null)
        {
            var props = analyzer.GetMergedProperties(componentType);
            if (props != null)
            {
                foreach (var (name, prop) in props)
                {
                    items.Add(new CompletionItem
                    {
                        Label = name,
                        Kind = CompletionItemKind.Property,
                        Detail = prop.Type,
                        Documentation = prop.Description,
                        InsertText = $"{name}: ",
                        SortText = $"1_{name}"
                    });
                }
            }

            var events = analyzer.GetMergedEvents(componentType);
            if (events != null)
            {
                foreach (var (name, evt) in events)
                {
                    items.Add(new CompletionItem
                    {
                        Label = $"#{name}",
                        Kind = CompletionItemKind.Event,
                        Detail = FormatEventSignature(evt),
                        Documentation = evt.Description,
                        InsertText = $"#{name}: ",
                        SortText = $"3_{name}"
                    });
                }
            }

            // Add imported type properties and events
            var importedType = semanticModel?.GetImportedType(componentType);
            if (importedType != null)
            {
                foreach (var (name, prop) in importedType.Properties)
                {
                    items.Add(new CompletionItem
                    {
                        Label = name,
                        Kind = CompletionItemKind.Property,
                        Detail = $"{prop.Type} (imported param)",
                        InsertText = $"{name}: ",
                        SortText = $"1_{name}"
                    });
                }

                foreach (var (name, evt) in importedType.Events)
                {
                    items.Add(new CompletionItem
                    {
                        Label = $"#{name}",
                        Kind = CompletionItemKind.Event,
                        Detail = $"event (imported) {FormatEventSignature(evt)}",
                        InsertText = $"#{name}: ",
                        SortText = $"3_{name}"
                    });
                }
            }
        }

        // Add child component names
        foreach (string className in analyzer.GetAllClassNames())
        {
            items.Add(new CompletionItem
            {
                Label = className,
                Kind = CompletionItemKind.Class,
                InsertText = $"{className} {{\n    \n}}",
                SortText = $"5_{className}"
            });
        }

        // Add keywords
        AddKeywordCompletion(items, "param", "Declare a parameter");
        AddKeywordCompletion(items, "event", "Declare an event");
        AddKeywordCompletion(items, "each", "Iterate over a collection");
    }

    /// <summary>
    /// Provides completions inside an object literal body (<c>{ | }</c>).
    /// When the object literal is the value of a typed pseudo-property (e.g.
    /// <c>theme_overrides</c>), the synthetic type's properties are offered as keys.
    /// </summary>
    private static void AddObjectLiteralBodyCompletions(
        List<CompletionItem> items, SyntaxNode node, ProjectAnalyzer analyzer, SemanticModel? semanticModel)
    {
        _ = semanticModel; // reserved for future use
        // Find the enclosing ObjectLiteralExpressionSyntax
        ObjectLiteralExpressionSyntax? objLit = null;
        for (var current = node; current != null; current = current.Parent)
        {
            if (current is ObjectLiteralExpressionSyntax o) { objLit = o; break; }
        }
        if (objLit == null) return;

        // The ObjectLiteral should be the value of a PropertyAssignment
        if (objLit.Parent is not PropertyAssignmentSyntax assignment) return;

        string propName = assignment.Name.Text;
        string? componentType = HandlerUtils.FindEnclosingComponentType(assignment);
        if (componentType == null) return;

        // Resolve the property type
        var propDesc = analyzer.GetPropertyInfo(componentType, propName);
        if (propDesc == null) return;

        // Look up the synthetic type
        var targetType = analyzer.GetTypeInfo(propDesc.Type);
        if (targetType == null) return;

        // Collect already-used keys to avoid duplicates
        var usedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var existing in objLit.Properties)
            usedKeys.Add(existing.Name.Text);

        foreach (var (name, prop) in targetType.Properties)
        {
            if (usedKeys.Contains(name)) continue;
            items.Add(new CompletionItem
            {
                Label = name,
                Kind = CompletionItemKind.Property,
                Detail = prop.Type,
                Documentation = prop.Description,
                InsertText = $"{name}: ",
                SortText = $"1_{name}"
            });
        }
    }

    // ── Struct constructor mapping: expectedType → (label, snippet, argCount) ──

    private static readonly Dictionary<string, (string Label, string Snippet, int ArgCount)> s_structConstructors =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Vector2"] = ("Vector2", "Vector2(${1:0}, ${2:0})", 2),
            ["Vector2I"] = ("Vector2I", "Vector2I(${1:0}, ${2:0})", 2),
            ["Vector3"] = ("Vector3", "Vector3(${1:0}, ${2:0}, ${3:0})", 3),
            ["Vector3I"] = ("Vector3I", "Vector3I(${1:0}, ${2:0}, ${3:0})", 3),
            ["Vector4"] = ("Vector4", "Vector4(${1:0}, ${2:0}, ${3:0}, ${4:0})", 4),
            ["Vector4I"] = ("Vector4I", "Vector4I(${1:0}, ${2:0}, ${3:0}, ${4:0})", 4),
            ["Rect2"] = ("Rect2", "Rect2(${1:0}, ${2:0}, ${3:0}, ${4:0})", 4),
            ["Rect2I"] = ("Rect2I", "Rect2I(${1:0}, ${2:0}, ${3:0}, ${4:0})", 4),
            ["Color"] = ("Color", "Color(${1:1}, ${2:1}, ${3:1}, ${4:1})", 4),
            ["Transform2D"] = ("Transform2D", "Transform2D(${1:0}, ${2:0}, ${3:0})", 3),
            ["Transform3D"] = ("Transform3D", "Transform3D(${1:0}, ${2:0}, ${3:0}, ${4:0})", 4),
            ["Plane"] = ("Plane", "Plane(${1:0}, ${2:0}, ${3:0}, ${4:0})", 4),
            ["Quaternion"] = ("Quaternion", "Quaternion(${1:0}, ${2:0}, ${3:0}, ${4:0})", 4),
            ["Basis"] = ("Basis", "Basis(${1:0}, ${2:0}, ${3:0})", 3),
            ["Aabb"] = ("Aabb", "Aabb(${1:0}, ${2:0}, ${3:0}, ${4:0}, ${5:0}, ${6:0})", 6),
            ["Projection"] = ("Projection", "Projection(${1:0}, ${2:0}, ${3:0}, ${4:0})", 4),
        };

    // ── Resource keyword → expected type mapping ──

    private static readonly Dictionary<string, string> s_resourceTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Texture2D"] = "image",
        ["Texture"] = "image",
        ["Font"] = "font",
        ["FontFile"] = "font",
        ["AudioStream"] = "audio",
        ["VideoStream"] = "video",
    };

    // ── Value-type set (null is not compatible) ──

    private static readonly HashSet<string> s_valueTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "bool", "int", "float", "double", "long",
        "Vector2", "Vector2I", "Vector3", "Vector3I", "Vector4", "Vector4I",
        "Rect2", "Rect2I", "Transform2D", "Transform3D",
        "Color", "Plane", "Quaternion", "Basis", "Rid", "Aabb", "Projection", "Margins"
    };

    private static void AddPropertyValueCompletions(
        List<CompletionItem> items, SyntaxNode node, ProjectAnalyzer analyzer,
        SemanticModel? semanticModel)
    {
        string? expectedType = ResolveExpectedType(node, analyzer, semanticModel);
        string? normalized = expectedType != null ? SemanticModel.NormalizeTypeName(expectedType) : null;
        bool isKnownType = normalized != null
                           && !string.Equals(normalized, "Variant", StringComparison.OrdinalIgnoreCase);

        AddEnumCompletions(items, node, analyzer, semanticModel);
        AddStructConstructorCompletions(items, normalized);
        AddLiteralsAndKeywords(items, normalized, isKnownType);
        AddResourceKeywords(items, normalized, isKnownType);
        AddControllerCompletions(items, semanticModel, normalized, isKnownType);
        AddRootParamCompletions(items, node, normalized, isKnownType);
        AddScopeVariables(items, node, semanticModel, normalized, isKnownType);
    }

    /// <summary>
    /// Resolves the expected type for a property value at the given AST position.
    /// </summary>
    private static string? ResolveExpectedType(
        SyntaxNode node, ProjectAnalyzer analyzer, SemanticModel? semanticModel)
    {
        string? componentType = HandlerUtils.FindEnclosingComponentType(node);
        string? propName = HandlerUtils.FindPropertyName(node);
        if (componentType == null || propName == null) return null;

        // If inside an ObjectLiteral, resolve the key type from the synthetic target type
        var objLitType = ResolveObjectLiteralTargetType(node, analyzer);
        if (objLitType != null)
        {
            return objLitType.Properties.TryGetValue(propName, out var objProp) ? objProp.Type : null;
        }

        // Check imported type first (user-defined params)
        var importedType = semanticModel?.GetImportedType(componentType);
        if (importedType != null &&
            importedType.Properties.TryGetValue(propName, out var iProp))
        {
            return iProp.Type;
        }

        // Fall back to Godot API
        return analyzer.GetPropertyInfo(componentType, propName)?.Type;
    }

    /// <summary>
    /// If the given node is inside an <see cref="ObjectLiteralExpressionSyntax"/> that resolves
    /// to a known synthetic type, returns that type; otherwise <c>null</c>.
    /// </summary>
    private static TypeDescriptor? ResolveObjectLiteralTargetType(SyntaxNode node, ProjectAnalyzer analyzer)
    {
        // Walk up and find the nearest ObjectLiteral → PropertyAssignment chain
        for (var current = node; current != null; current = current.Parent)
        {
            if (current is ObjectLiteralExpressionSyntax { Parent: PropertyAssignmentSyntax outerProp })
            {
                string outerPropName = outerProp.Name.Text;
                string? compType = HandlerUtils.FindEnclosingComponentType(outerProp);
                if (compType == null) return null;

                var outerPropDesc = analyzer.GetPropertyInfo(compType, outerPropName);
                if (outerPropDesc == null) return null;

                return analyzer.GetTypeInfo(outerPropDesc.Type);
            }
        }

        return null;
    }

    private static void AddEnumCompletions(
        List<CompletionItem> items, SyntaxNode node, ProjectAnalyzer analyzer,
        SemanticModel? semanticModel)
    {
        string? componentType = HandlerUtils.FindEnclosingComponentType(node);
        string? propName = HandlerUtils.FindPropertyName(node);
        if (componentType == null || propName == null) return;

        // Check imported type first
        var importedType = semanticModel?.GetImportedType(componentType);
        PropertyDescriptor? propInfo = null;
        if (importedType != null)
            importedType.Properties.TryGetValue(propName, out propInfo);
        propInfo ??= analyzer.GetPropertyInfo(componentType, propName);

        if (propInfo?.EnumValues == null) return;
        foreach (var enumVal in propInfo.EnumValues)
        {
            items.Add(new CompletionItem
            {
                Label = $".{enumVal.Name}",
                Kind = CompletionItemKind.EnumMember,
                Detail = enumVal.Value,
                Documentation = enumVal.Description,
                InsertText = enumVal.Name,
                FilterText = $".{enumVal.Name}",
                SortText = $"0_{enumVal.Name}"
            });
        }
    }

    private static void AddStructConstructorCompletions(List<CompletionItem> items, string? normalizedType)
    {
        if (normalizedType == null) return;
        if (!s_structConstructors.TryGetValue(normalizedType, out var ctor)) return;

        items.Add(new CompletionItem
        {
            Label = $"{ctor.Label}()",
            Kind = CompletionItemKind.Constructor,
            Detail = $"{ctor.Label} constructor ({ctor.ArgCount} args)",
            InsertText = ctor.Snippet,
            InsertTextFormat = 2, // Snippet
            SortText = $"1_{ctor.Label}"
        });
    }

    private static void AddLiteralsAndKeywords(
        List<CompletionItem> items, string? normalizedType, bool isKnownType)
    {
        // true/false: only when bool or unknown
        if (!isKnownType || string.Equals(normalizedType, "bool", StringComparison.OrdinalIgnoreCase))
        {
            items.Add(new CompletionItem
            {
                Label = "true", Kind = CompletionItemKind.Keyword, SortText = "4_true"
            });
            items.Add(new CompletionItem
            {
                Label = "false", Kind = CompletionItemKind.Keyword, SortText = "4_false"
            });
        }

        // null: only when not a value type or unknown
        if (!isKnownType || !s_valueTypes.Contains(normalizedType!))
        {
            items.Add(new CompletionItem
            {
                Label = "null", Kind = CompletionItemKind.Keyword, SortText = "4_null"
            });
        }
    }

    private static void AddResourceKeywords(
        List<CompletionItem> items, string? normalizedType, bool isKnownType)
    {
        // When type is known, only add matching resource keywords
        if (isKnownType)
        {
            if (s_resourceTypeMap.TryGetValue(normalizedType!, out string? keyword))
            {
                AddSingleResourceKeyword(items, keyword);
            }

            return;
        }

        // Unknown type: add all resource keywords
        AddSingleResourceKeyword(items, "image");
        AddSingleResourceKeyword(items, "font");
        AddSingleResourceKeyword(items, "audio");
        AddSingleResourceKeyword(items, "video");
    }

    private static void AddSingleResourceKeyword(List<CompletionItem> items, string keyword)
    {
        string detail = keyword switch
        {
            "image" => "Load an image resource (Texture2D)",
            "font" => "Load a font resource (Font)",
            "audio" => "Load an audio resource (AudioStream)",
            "video" => "Load a video resource (VideoStream)",
            _ => "Load a resource"
        };
        items.Add(new CompletionItem
        {
            Label = keyword,
            Kind = CompletionItemKind.Function,
            Detail = detail,
            InsertText = $"{keyword}(\"\")",
            SortText = $"5_{keyword}"
        });
    }

    private static void AddControllerCompletions(
        List<CompletionItem> items, SemanticModel? semanticModel,
        string? normalizedType, bool isKnownType)
    {
        var controller = semanticModel?.GetController();
        if (controller == null)
        {
            // No controller info — add generic $controller reference
            items.Add(new CompletionItem
            {
                Label = "$controller",
                Kind = CompletionItemKind.Variable,
                Detail = "Controller reference",
                SortText = "3_controller"
            });
            return;
        }

        // When type is known, only add compatible controller members directly
        if (isKnownType)
        {
            foreach (var prop in controller.Properties)
            {
                if (!SemanticModel.IsTypeCompatible(prop.Type, normalizedType!)) continue;
                string snakeName = KeyConverter.FromCamelCase(prop.Name);
                items.Add(new CompletionItem
                {
                    Label = $"$controller.{snakeName}",
                    Kind = CompletionItemKind.Property,
                    Detail = prop.Type,
                    InsertText = $"$controller.{snakeName}",
                    SortText = $"3_{snakeName}"
                });
            }

            foreach (var method in controller.Methods)
            {
                string? returnType = method.IsDelegate ? null : method.ReturnType;
                if (returnType == null || !SemanticModel.IsTypeCompatible(returnType, normalizedType!))
                    continue;
                string snakeName = KeyConverter.FromCamelCase(method.Name);
                string paramStr = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
                items.Add(new CompletionItem
                {
                    Label = $"$controller.{snakeName}",
                    Kind = CompletionItemKind.Method,
                    Detail = $"{method.ReturnType} ({paramStr})",
                    InsertText = $"$controller.{snakeName}()",
                    Documentation = method.Description,
                    SortText = $"3_{snakeName}"
                });
            }
        }
        else
        {
            // Unknown type: add generic $controller reference
            items.Add(new CompletionItem
            {
                Label = "$controller",
                Kind = CompletionItemKind.Variable,
                Detail = "Controller reference",
                Documentation = "Reference to the associated controller class.",
                SortText = "3_controller"
            });
        }
    }

    private static void AddRootParamCompletions(
        List<CompletionItem> items, SyntaxNode node, string? normalizedType, bool isKnownType)
    {
        var root = HandlerUtils.GetDocumentRoot(node);
        if (root == null)
        {
            items.Add(new CompletionItem
            {
                Label = "$root",
                Kind = CompletionItemKind.Variable,
                Detail = "Root component reference",
                SortText = "3_root"
            });
            return;
        }

        if (isKnownType)
        {
            foreach (var member in root.RootComponent.Members)
            {
                if (member is not ParameterDeclarationSyntax param) continue;
                string paramType = param.TypeName.Text;
                if (!SemanticModel.IsTypeCompatible(paramType, normalizedType!)) continue;
                items.Add(new CompletionItem
                {
                    Label = $"$root.{param.Name.Text}",
                    Kind = CompletionItemKind.Variable,
                    Detail = paramType,
                    InsertText = $"$root.{param.Name.Text}",
                    SortText = $"3_{param.Name.Text}"
                });
            }
        }
        else
        {
            items.Add(new CompletionItem
            {
                Label = "$root",
                Kind = CompletionItemKind.Variable,
                Detail = "Root component reference",
                Documentation = "Reference to the root component's parameters.",
                SortText = "3_root"
            });
        }
    }

    private static void AddScopeVariables(
        List<CompletionItem> items, SyntaxNode node, SemanticModel? semanticModel,
        string? normalizedType, bool isKnownType)
    {
        if (semanticModel == null) return;

        // Each-block local variables
        var eachVars = semanticModel.FindEachVariablesInScope(node);
        foreach (var ev in eachVars)
        {
            if (isKnownType && ev.ResolvedType != null
                            && !SemanticModel.IsTypeCompatible(ev.ResolvedType, normalizedType!))
                continue;

            string kind = ev.IsIndex ? "each index" : "each value";
            items.Add(new CompletionItem
            {
                Label = ev.Name,
                Kind = CompletionItemKind.Variable,
                Detail = $"{ev.ResolvedType ?? "unknown"} ({kind})",
                SortText = $"2_{ev.Name}"
            });
        }

        // Named node aliases (@xxx)
        foreach (var alias in semanticModel.GetAllAliases().Values)
        {
            if (isKnownType && !SemanticModel.IsTypeCompatible(alias.TypeName, normalizedType!))
                continue;

            items.Add(new CompletionItem
            {
                Label = alias.Name,
                Kind = CompletionItemKind.Variable,
                Detail = $"{alias.TypeName} (named node)",
                SortText = $"2_{alias.Name}"
            });
        }
    }

    private static void AddComponentNameCompletions(
        List<CompletionItem> items, GumlDocument document, ProjectAnalyzer analyzer)
    {
        // Offer all known control types
        foreach (string className in analyzer.GetAllClassNames())
        {
            var typeInfo = analyzer.GetTypeInfo(className);
            items.Add(new CompletionItem
            {
                Label = className,
                Kind = CompletionItemKind.Class,
                Detail = typeInfo?.BaseType,
                Documentation = typeInfo?.Description,
                InsertText = $"{className} {{\n    \n}}",
            });
        }

        // Offer imported component names
        foreach (var import in document.Root.Imports)
        {
            if (import.Alias != null)
            {
                items.Add(new CompletionItem
                {
                    Label = import.Alias.Name.Text,
                    Kind = CompletionItemKind.Class,
                    Detail = $"import alias for {import.Path.Text}",
                });
            }
        }
    }

    private static void AddMemberAccessCompletions(
        List<CompletionItem> items, SyntaxNode node, ProjectAnalyzer analyzer,
        SemanticModel? semanticModel)
    {
        // Find the receiver of the member access
        if (node is not MemberAccessExpressionSyntax memberAccess) return;

        // $controller.xxx
        if (memberAccess.Expression is ReferenceExpressionSyntax { Identifier.Text: "$controller" })
        {
            var controller = semanticModel?.GetController();
            if (controller != null)
            {
                foreach (var prop in controller.Properties)
                {
                    items.Add(new CompletionItem
                    {
                        Label = KeyConverter.FromCamelCase(prop.Name),
                        Kind = CompletionItemKind.Property,
                        Detail = prop.Type,
                    });
                }

                foreach (var method in controller.Methods)
                {
                    string paramStr = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
                    string detail = method.IsDelegate
                        ? $"delegate({paramStr})"
                        : $"{method.ReturnType} ({paramStr})";
                    items.Add(new CompletionItem
                    {
                        Label = KeyConverter.FromCamelCase(method.Name),
                        Kind = method.IsDelegate ? CompletionItemKind.Event : CompletionItemKind.Method,
                        Detail = detail,
                        Documentation = method.Description
                    });
                }
            }
        }
        // Chained $controller.a.xxx — resolve the parent type and offer its members
        else if (memberAccess.Expression is MemberAccessExpressionSyntax parentAccess
                 && HandlerUtils.GetControllerRoot(memberAccess) != null)
        {
            string? parentType = semanticModel?.ResolveExpressionTypePublic(parentAccess);
            if (parentType != null)
            {
                var props = analyzer.GetMergedProperties(parentType);
                if (props != null)
                {
                    foreach (var (name, prop) in props)
                    {
                        items.Add(new CompletionItem
                        {
                            Label = name,
                            Kind = CompletionItemKind.Property,
                            Detail = prop.Type,
                            Documentation = prop.Description,
                        });
                    }
                }

                var events = analyzer.GetMergedEvents(parentType);
                if (events != null)
                {
                    foreach (var (name, evt) in events)
                    {
                        items.Add(new CompletionItem
                        {
                            Label = name,
                            Kind = CompletionItemKind.Event,
                            Detail = FormatEventSignature(evt),
                            Documentation = evt.Description,
                        });
                    }
                }
            }
        }
        // $root.xxx — offer param and event names
        else if (memberAccess.Expression is ReferenceExpressionSyntax { Identifier.Text: "$root" })
        {
            // Search the root component for param and event declarations
            var root = HandlerUtils.GetDocumentRoot(node);
            if (root != null)
            {
                foreach (var member in root.RootComponent.Members)
                {
                    if (member is ParameterDeclarationSyntax param)
                    {
                        items.Add(new CompletionItem
                        {
                            Label = param.Name.Text,
                            Kind = CompletionItemKind.Variable,
                            Detail = param.TypeName.Text,
                        });
                    }
                    else if (member is EventDeclarationSyntax evt)
                    {
                        items.Add(new CompletionItem
                        {
                            Label = evt.Name.Text, Kind = CompletionItemKind.Event, Detail = "event",
                        });
                    }
                }
            }
        }
        // Each-variable member access: item.xxx or item.a.xxx
        // Named node alias member access: @alias.xxx or @alias.a.xxx
        else if (semanticModel != null)
        {
            AddEachVariableMemberCompletions(items, memberAccess, analyzer, semanticModel);
            AddAliasMemberCompletions(items, memberAccess, analyzer, semanticModel);
        }
    }

    private static void AddAliasMemberCompletions(
        List<CompletionItem> items, MemberAccessExpressionSyntax memberAccess,
        ProjectAnalyzer analyzer, SemanticModel semanticModel)
    {
        string? parentType = semanticModel.ResolveExpressionTypePublic(memberAccess.Expression);
        if (parentType == null) return;

        // Verify the root is actually an alias reference
        ExpressionSyntax rootExpr = memberAccess.Expression;
        while (rootExpr is MemberAccessExpressionSyntax inner)
            rootExpr = inner.Expression;

        if (rootExpr is not ReferenceExpressionSyntax { Identifier.Kind: SyntaxKind.AliasRefToken })
            return;

        var props = analyzer.GetMergedProperties(parentType);
        if (props != null)
        {
            foreach (var (name, prop) in props)
            {
                items.Add(new CompletionItem
                {
                    Label = name,
                    Kind = CompletionItemKind.Property,
                    Detail = prop.Type,
                    Documentation = prop.Description,
                });
            }
        }

        var events = analyzer.GetMergedEvents(parentType);
        if (events != null)
        {
            foreach (var (name, evt) in events)
            {
                items.Add(new CompletionItem
                {
                    Label = name,
                    Kind = CompletionItemKind.Event,
                    Detail = FormatEventSignature(evt),
                    Documentation = evt.Description,
                });
            }
        }
    }

    private static void AddEachVariableMemberCompletions(
        List<CompletionItem> items, MemberAccessExpressionSyntax memberAccess,
        ProjectAnalyzer analyzer, SemanticModel semanticModel)
    {
        string? parentType = semanticModel.ResolveExpressionTypePublic(memberAccess.Expression);
        if (parentType == null) return;

        // Verify the root is actually an each variable
        ExpressionSyntax rootExpr = memberAccess.Expression;
        while (rootExpr is MemberAccessExpressionSyntax inner)
            rootExpr = inner.Expression;

        if (rootExpr is not ReferenceExpressionSyntax refExpr) return;
        if (semanticModel.FindEachVariable(refExpr.Identifier) == null) return;

        var props = analyzer.GetMergedProperties(parentType);
        if (props != null)
        {
            foreach (var (name, prop) in props)
            {
                items.Add(new CompletionItem
                {
                    Label = name,
                    Kind = CompletionItemKind.Property,
                    Detail = prop.Type,
                    Documentation = prop.Description,
                });
            }
        }

        var events = analyzer.GetMergedEvents(parentType);
        if (events != null)
        {
            foreach (var (name, evt) in events)
            {
                items.Add(new CompletionItem
                {
                    Label = name,
                    Kind = CompletionItemKind.Event,
                    Detail = FormatEventSignature(evt),
                    Documentation = evt.Description,
                });
            }
        }
    }

    private static void AddEventRefCompletions(
        List<CompletionItem> items, SyntaxNode node, ProjectAnalyzer analyzer, SemanticModel? semanticModel)
    {
        string? componentType = HandlerUtils.FindEnclosingComponentType(node);
        if (componentType == null) return;

        var events = analyzer.GetMergedEvents(componentType);
        if (events != null)
        {
            foreach (var (name, evt) in events)
            {
                items.Add(new CompletionItem
                {
                    Label = $"#{name}",
                    Kind = CompletionItemKind.Event,
                    Detail = FormatEventSignature(evt),
                    Documentation = evt.Description,
                    InsertText = $"#{name}: ",
                });
            }
        }

        // Add imported events
        var importedEvents = semanticModel?.GetImportedEvents(componentType);
        if (importedEvents != null)
        {
            foreach (var (name, evt) in importedEvents)
            {
                items.Add(new CompletionItem
                {
                    Label = $"#{name}",
                    Kind = CompletionItemKind.Event,
                    Detail = $"(imported) {FormatEventSignature(evt)}",
                    InsertText = $"#{name}: ",
                });
            }
        }
    }

    // ── Helpers ──

    private static void AddKeywordCompletion(List<CompletionItem> items, string keyword, string detail)
    {
        items.Add(new CompletionItem
        {
            Label = keyword, Kind = CompletionItemKind.Keyword, Detail = detail, SortText = $"9_{keyword}"
        });
    }

    private static string FormatEventSignature(EventDescriptor evt)
    {
        if (evt.Parameters.Count == 0)
            return "signal()";
        var paramStrs = evt.Parameters.Select(p => $"{p.Type} {p.Name}");
        return $"signal({string.Join(", ", paramStrs)})";
    }
}
