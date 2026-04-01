using GUML.Analyzer.Utils;
using GUML.Analyzer.Workspace;
using GUML.Shared.Api;
using GUML.Shared.Converter;
using GUML.Shared.Syntax;
using GUML.Shared.Syntax.Nodes;
using GUML.Shared.Syntax.Nodes.Expressions;

namespace GUML.Analyzer.Handlers;

/// <summary>
/// Provides hover information for tokens in a GUML document.
/// </summary>
public static class HoverHandler
{
    /// <summary>
    /// Returns hover information for the token at the given position, or null if none.
    /// </summary>
    public static HoverResult? GetHover(
        GumlDocument document, SemanticModel? semanticModel, LspPosition position,
        ProjectAnalyzer analyzer)
    {
        var mapper = new PositionMapper(document.Text);
        int offset = mapper.GetOffset(position);

        var token = document.Root.FindToken(offset);
        if (token == null || token.IsMissing) return null;

        return token.Kind switch
        {
            SyntaxKind.ComponentNameToken => HoverComponentName(token, semanticModel, analyzer, mapper),
            SyntaxKind.IdentifierToken => HoverIdentifier(token, analyzer, semanticModel, mapper),
            SyntaxKind.GlobalRefToken => HoverGlobalRef(token, semanticModel, mapper),
            SyntaxKind.AliasRefToken => HoverAliasRef(token, semanticModel, mapper),
            SyntaxKind.EventRefToken => HoverEventRef(token, analyzer, semanticModel, mapper),
            SyntaxKind.EnumValueToken => HoverEnumValue(token, analyzer, mapper),
            SyntaxKind.MapToPropertyToken => HoverOperator(":=", "Bind data source to property (one-way data→UI).",
                token, mapper),
            SyntaxKind.MapToDataToken => HoverOperator("=:", "Bind property to data source (one-way UI→data).", token,
                mapper),
            SyntaxKind.MapTwoWayToken => HoverOperator("<=>", "Two-way binding between property and data source.",
                token, mapper),
            SyntaxKind.ImportKeyword => HoverKeyword("import", "Import a GUML component from another file.", token,
                mapper),
            SyntaxKind.ParamKeyword => HoverKeyword("param", "Declare a parameter for this component.", token, mapper),
            SyntaxKind.EventKeyword => HoverKeyword("event", "Declare a custom event for this component.", token,
                mapper),
            SyntaxKind.EachKeyword => HoverKeyword("each", "Iterate over a collection and instantiate children.", token,
                mapper),
            SyntaxKind.ImageKeyword => HoverKeyword("image", "Load an image resource: `image(\"res://path\")`", token,
                mapper),
            SyntaxKind.FontKeyword => HoverKeyword("font", "Load a font resource: `font(\"res://path\")`", token,
                mapper),
            SyntaxKind.AudioKeyword => HoverKeyword("audio", "Load an audio resource: `audio(\"res://path\")`", token,
                mapper),
            SyntaxKind.VideoKeyword => HoverKeyword("video", "Load a video resource: `video(\"res://path\")`", token,
                mapper),
            _ => null
        };
    }

    private static HoverResult? HoverComponentName(SyntaxToken token,
        SemanticModel? semanticModel, ProjectAnalyzer analyzer,
        PositionMapper mapper)
    {
        string typeName = token.Text;
        var typeDesc = analyzer.GetTypeInfo(typeName);
        if (typeDesc != null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"**{typeDesc.Name}**");
            if (!string.IsNullOrEmpty(typeDesc.BaseType))
                sb.AppendLine($"*extends* `{typeDesc.BaseType}`");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(typeDesc.Description))
                sb.AppendLine(typeDesc.Description);
            return MakeHover(sb.ToString(), token, mapper);
        }

        // Check if it's an imported component via SemanticModel
        var importedType = semanticModel?.GetImportedType(typeName);
        if (importedType != null)
        {
            string? sourcePath = semanticModel!.GetImportSourcePath(typeName);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"**{typeName}** *(imported component)*");
            if (sourcePath != null)
                sb.AppendLine($"Source: `{sourcePath}`");
            sb.AppendLine();

            if (importedType.Properties.Count > 0)
            {
                sb.AppendLine("**Parameters:**");
                foreach (var prop in importedType.Properties.Values)
                {
                    sb.Append($"- `{prop.Type}` {prop.Name}");
                    if (!string.IsNullOrEmpty(prop.Description))
                        sb.Append($" — {prop.Description}");
                    sb.AppendLine();
                }
            }

            if (importedType.Events.Count > 0)
            {
                sb.AppendLine("**Events:**");
                foreach (var evt in importedType.Events.Values)
                {
                    if (evt.Parameters.Count > 0)
                    {
                        var paramStrs = evt.Parameters.Select(p => $"`{p.Type}` {p.Name}");
                        sb.Append($"- {evt.Name}({string.Join(", ", paramStrs)})");
                    }
                    else
                    {
                        sb.Append($"- {evt.Name}");
                    }

                    if (!string.IsNullOrEmpty(evt.Description))
                        sb.Append($" — {evt.Description}");
                    sb.AppendLine();
                }
            }

            return MakeHover(sb.ToString(), token, mapper);
        }

        return null;
    }

    private static HoverResult? HoverIdentifier(
        SyntaxToken token, ProjectAnalyzer analyzer, SemanticModel? semanticModel, PositionMapper mapper)
    {
        var parent = token.Parent;

        // Property assignment: name: value
        if (parent is PropertyAssignmentSyntax prop && token == prop.Name)
        {
            return HoverPropertyName(token, prop, analyzer, semanticModel, mapper);
        }

        // Mapping assignment: name := value
        if (parent is MappingAssignmentSyntax mapping && token == mapping.Name)
        {
            return HoverMappingName(token, mapping, analyzer, mapper);
        }

        // Parameter declaration: param Type name
        if (parent is ParameterDeclarationSyntax paramDecl)
        {
            if (token == paramDecl.Name)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"**param** `{paramDecl.TypeName.Text}` `{paramDecl.Name.Text}`");
                AppendDocComment(sb, paramDecl.DocumentationComment);
                if (paramDecl.DefaultValue != null)
                    sb.AppendLine($"Default: `{paramDecl.DefaultValue}`");
                return MakeHover(sb.ToString(), token, mapper);
            }

            if (token == paramDecl.TypeName)
            {
                return MakeHover($"**Type** `{paramDecl.TypeName.Text}`", token, mapper);
            }
        }

        // Member access: expr.name — the name part
        if (parent is MemberAccessExpressionSyntax memberAccess && token == memberAccess.Name)
        {
            return HoverMemberAccess(token, memberAccess, analyzer, semanticModel, mapper);
        }

        // Check each-block local variable (both declaration and usage sites)
        if (semanticModel != null)
        {
            var eachVar = semanticModel.FindEachVariable(token);
            if (eachVar != null)
            {
                string kind = eachVar.IsIndex ? "index" : "value";
                string type = eachVar.ResolvedType ?? "unknown";
                return MakeHover($"**each {kind} variable** `{eachVar.Name}`: `{type}`", token, mapper);
            }
        }

        return null;
    }

    private static HoverResult? HoverPropertyName(
        SyntaxToken token, PropertyAssignmentSyntax prop, ProjectAnalyzer analyzer, SemanticModel? semanticModel,
        PositionMapper mapper)
    {
        string propName = token.Text;

        // Check if this property is inside an ObjectLiteral (e.g. theme_overrides: { key: val })
        if (prop.Parent is ObjectLiteralExpressionSyntax objLit)
        {
            return HoverObjectLiteralKey(token, propName, objLit, analyzer, mapper);
        }

        string? componentType = HandlerUtils.FindEnclosingComponentType(token.Parent);
        if (componentType == null) return null;

        var propDesc = analyzer.GetPropertyInfo(componentType, propName);
        if (propDesc != null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"**{propName}**: `{propDesc.Type}`");
            if (!string.IsNullOrEmpty(propDesc.Description))
                sb.AppendLine(propDesc.Description);
            AppendEnumValues(sb, propDesc);
            AppendCurrentMappingInfo(sb, ":", propDesc.Mapping);
            return MakeHover(sb.ToString(), token, mapper);
        }

        // Check imported type properties
        var importedProp = semanticModel?.ResolveProperty(componentType, propName);
        if (importedProp != null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"**{propName}**: `{importedProp.Type}` *(imported param)*");
            if (!string.IsNullOrEmpty(importedProp.Description))
                sb.AppendLine(importedProp.Description);
            return MakeHover(sb.ToString(), token, mapper);
        }

        return null;
    }

    private static HoverResult? HoverMappingName(
        SyntaxToken token, MappingAssignmentSyntax mapping, ProjectAnalyzer analyzer, PositionMapper mapper)
    {
        string propName = token.Text;
        string? componentType = HandlerUtils.FindEnclosingComponentType(token.Parent);
        if (componentType == null) return null;

        var propDesc = analyzer.GetPropertyInfo(componentType, propName);
        if (propDesc == null) return null;

        string opText = mapping.Operator.Text;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"**{propName}** `{opText}` : `{propDesc.Type}`");
        if (!string.IsNullOrEmpty(propDesc.Description))
            sb.AppendLine(propDesc.Description);
        AppendEnumValues(sb, propDesc);
        AppendCurrentMappingInfo(sb, opText, propDesc.Mapping);
        return MakeHover(sb.ToString(), token, mapper);
    }

    private static HoverResult? HoverGlobalRef(SyntaxToken token, SemanticModel? semanticModel, PositionMapper mapper)
    {
        string text = token.Text;
        if (text == "$controller")
        {
            var controller = semanticModel?.GetController();
            if (controller != null)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"**$controller** → `{controller.FullName}`");
                if (!string.IsNullOrEmpty(controller.Description))
                {
                    sb.AppendLine();
                    sb.AppendLine(controller.Description);
                }

                sb.AppendLine();
                sb.AppendLine($"Properties: {controller.Properties.Count}, Methods: {controller.Methods.Count}");
                return MakeHover(sb.ToString(), token, mapper);
            }

            return MakeHover("**$controller** — Reference to the associated controller class.", token, mapper);
        }

        if (text == "$root")
        {
            var root = HandlerUtils.GetDocumentRoot(token.Parent);
            if (root != null)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("**$root** — Reference to the root component's parameters.");
                sb.AppendLine();
                var paramList = new List<string>();
                var eventList = new List<string>();
                foreach (var member in root.RootComponent.Members)
                {
                    if (member is ParameterDeclarationSyntax param)
                        paramList.Add($"`{param.TypeName.Text}` {param.Name.Text}");
                    else if (member is EventDeclarationSyntax evt)
                        eventList.Add(evt.Name.Text);
                }

                if (paramList.Count > 0)
                    sb.AppendLine($"Parameters: {string.Join(", ", paramList)}");
                if (eventList.Count > 0)
                    sb.AppendLine($"Events: {string.Join(", ", eventList)}");
                return MakeHover(sb.ToString(), token, mapper);
            }

            return MakeHover("**$root** — Reference to the root component's parameters.", token, mapper);
        }

        return null;
    }

    private static HoverResult? HoverAliasRef(SyntaxToken token, SemanticModel? semanticModel, PositionMapper mapper)
    {
        if (semanticModel == null) return null;

        var alias = semanticModel.FindAlias(token.Text);
        if (alias == null)
            return MakeHover($"**{token.Text}** — *(undefined named node)*", token, mapper);

        var sb = new System.Text.StringBuilder();
        bool isDeclaration = token.Parent is AliasPrefixSyntax;
        string label = isDeclaration ? "named node declaration" : "named node";
        sb.AppendLine($"**{alias.Name}** → `{alias.TypeName}` *({label})*");
        AppendDocComment(sb, alias.DocumentationComment);
        return MakeHover(sb.ToString(), token, mapper);
    }

    private static HoverResult? HoverEventRef(SyntaxToken token, ProjectAnalyzer analyzer, SemanticModel? semanticModel,
        PositionMapper mapper)
    {
        string text = token.Text;
        string eventName = text.StartsWith("#") ? text[1..] : text;

        string? componentType = HandlerUtils.FindEnclosingComponentType(token.Parent);
        if (componentType == null) return null;

        var events = analyzer.GetMergedEvents(componentType);
        if (events != null && events.TryGetValue(eventName, out var evt))
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"**signal** `{eventName}`");
            if (evt.Parameters.Count > 0)
            {
                var paramStrs = evt.Parameters.Select(p => $"`{p.Type}` {p.Name}");
                sb.AppendLine($"Parameters: {string.Join(", ", paramStrs)}");
            }

            if (!string.IsNullOrEmpty(evt.Description))
                sb.AppendLine(evt.Description);

            return MakeHover(sb.ToString(), token, mapper);
        }

        // Check imported events
        var importedEvents = semanticModel?.GetImportedEvents(componentType);
        if (importedEvents != null && importedEvents.TryGetValue(eventName, out var importedEvt))
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"**event** `{eventName}` *(imported)*");
            if (importedEvt.Parameters.Count > 0)
            {
                var paramStrs = importedEvt.Parameters.Select(p => $"`{p.Type}` {p.Name}");
                sb.AppendLine($"Parameters: {string.Join(", ", paramStrs)}");
            }

            if (!string.IsNullOrEmpty(importedEvt.Description))
                sb.AppendLine(importedEvt.Description);

            return MakeHover(sb.ToString(), token, mapper);
        }

        return null;
    }

    private static HoverResult? HoverEnumValue(SyntaxToken token, ProjectAnalyzer analyzer, PositionMapper mapper)
    {
        string text = token.Text;
        string valueName = text.StartsWith(".") ? text[1..] : text;

        // Find the enclosing property to determine the enum type
        string? componentType = HandlerUtils.FindEnclosingComponentType(token.Parent);
        string? propName = HandlerUtils.FindPropertyName(token.Parent);
        if (componentType == null || propName == null) return null;

        var propDesc = analyzer.GetPropertyInfo(componentType, propName);
        if (propDesc?.EnumValues == null) return null;

        var enumVal = propDesc.EnumValues.FirstOrDefault(e => e.Name == valueName);
        if (enumVal == null) return null;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"**.{enumVal.Name}** = `{enumVal.Value}`");
        if (!string.IsNullOrEmpty(enumVal.Description))
            sb.AppendLine(enumVal.Description);

        return MakeHover(sb.ToString(), token, mapper);
    }

    private static HoverResult? HoverMemberAccess(
        SyntaxToken token, MemberAccessExpressionSyntax memberAccess,
        ProjectAnalyzer analyzer, SemanticModel? semanticModel, PositionMapper mapper)
    {
        // Check if the chain is rooted in $controller
        var controllerRoot = HandlerUtils.GetControllerRoot(memberAccess);
        if (controllerRoot != null)
        {
            // Resolve the type of the parent expression to determine which type this member belongs to
            if (memberAccess.Expression is ReferenceExpressionSyntax { Identifier.Text: "$controller" })
            {
                // Direct $controller.xxx — hover with controller metadata
                return HoverControllerMember(token, semanticModel, mapper);
            }

            // Chained: $controller.a.b — resolve the type of the parent, then look up property
            if (memberAccess.Expression is MemberAccessExpressionSyntax parentAccess)
            {
                string? parentType = semanticModel?.ResolveExpressionTypePublic(parentAccess);
                if (parentType != null)
                {
                    return HoverTypeMember(token, parentType, analyzer, mapper);
                }
            }
        }

        // Each-block variable member access: item.xxx
        if (semanticModel != null)
        {
            var eachVarRoot = GetEachVariableRoot(memberAccess, semanticModel);
            if (eachVarRoot != null)
            {
                // Resolve parent type for the member
                string? parentType = semanticModel.ResolveExpressionTypePublic(memberAccess.Expression);
                if (parentType != null)
                    return HoverTypeMember(token, parentType, analyzer, mapper);

                // If parent is the each variable itself, show the type member directly
                if (memberAccess.Expression is ReferenceExpressionSyntax refRoot)
                {
                    var eachVar = semanticModel.FindEachVariable(refRoot.Identifier);
                    if (eachVar?.ResolvedType != null)
                        return HoverTypeMember(token, eachVar.ResolvedType, analyzer, mapper);
                }
            }
        }

        // $root.param or $root.event
        if (memberAccess.Expression is ReferenceExpressionSyntax { Identifier.Text: "$root" })
        {
            string memberName = token.Text;
            var root = HandlerUtils.GetDocumentRoot(token.Parent);
            if (root != null)
            {
                foreach (var member in root.RootComponent.Members)
                {
                    if (member is ParameterDeclarationSyntax param && param.Name.Text == memberName)
                    {
                        var sb = new System.Text.StringBuilder();
                        sb.AppendLine($"**param** `{param.TypeName.Text}` `{param.Name.Text}`");
                        AppendDocComment(sb, param.DocumentationComment);
                        if (param.DefaultValue != null)
                            sb.AppendLine($"Default: `{param.DefaultValue}`");
                        return MakeHover(sb.ToString(), token, mapper);
                    }

                    if (member is EventDeclarationSyntax evt && evt.Name.Text == memberName)
                    {
                        var sb = new System.Text.StringBuilder();
                        sb.AppendLine($"**event** `{evt.Name.Text}`");
                        if (evt.Arguments is { Count: > 0 })
                        {
                            var argParts = new List<string>();
                            foreach (var a in evt.Arguments)
                                argParts.Add($"`{a.TypeName.Text}` {a.Name?.Text}");
                            sb.AppendLine($"Parameters: {string.Join(", ", argParts)}");
                        }

                        AppendDocComment(sb, evt.DocumentationComment);
                        return MakeHover(sb.ToString(), token, mapper);
                    }
                }
            }
        }

        // Named node alias member access: @my_label.xxx or @my_label.a.b
        if (semanticModel != null)
        {
            var aliasRoot = GetAliasRoot(memberAccess);
            if (aliasRoot != null)
            {
                string? parentType = semanticModel.ResolveExpressionTypePublic(memberAccess.Expression);
                if (parentType != null)
                    return HoverTypeMember(token, parentType, analyzer, mapper);

                // If parent is the alias itself, resolve from alias type
                if (memberAccess.Expression is ReferenceExpressionSyntax
                    { Identifier.Kind: SyntaxKind.AliasRefToken } aliasRef)
                {
                    var alias = semanticModel.FindAlias(aliasRef.Identifier.Text);
                    if (alias != null)
                        return HoverTypeMember(token, alias.TypeName, analyzer, mapper);
                }
            }
        }

        return null;
    }

    private static HoverResult HoverOperator(string op, string description, SyntaxToken token, PositionMapper mapper)
    {
        return MakeHover($"**`{op}`** \u2014 {description}", token, mapper);
    }

    private static HoverResult HoverKeyword(string keyword, string description, SyntaxToken token,
        PositionMapper mapper)
    {
        return MakeHover($"**`{keyword}`** — {description}", token, mapper);
    }

    /// <summary>
    /// Hover for a direct <c>$controller.xxx</c> member (property or method).
    /// </summary>
    private static HoverResult? HoverControllerMember(
        SyntaxToken token, SemanticModel? semanticModel, PositionMapper mapper)
    {
        var controller = semanticModel?.GetController();
        if (controller == null) return null;

        string pascalName = KeyConverter.ToPascalCase(token.Text);
        controller.MemberDescriptions.TryGetValue(pascalName, out string? memberDoc);

        var prop = controller.Properties.FirstOrDefault(p => p.Name == pascalName);
        if (prop != null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"**{prop.Name}**: `{prop.Type}`");
            if (!string.IsNullOrEmpty(memberDoc))
            {
                sb.AppendLine();
                sb.AppendLine(memberDoc);
            }

            return MakeHover(sb.ToString(), token, mapper);
        }

        var method = controller.Methods.FirstOrDefault(m => m.Name == pascalName);
        if (method != null)
        {
            var sb = new System.Text.StringBuilder();
            string paramStr = string.Join(", ", method.Parameters.Select(p => $"`{p.Type}` {p.Name}"));
            if (method.IsDelegate)
                sb.AppendLine($"**{method.Name}**({paramStr}) — delegate");
            else
                sb.AppendLine($"**{method.Name}**({paramStr}): `{method.ReturnType}`");
            if (!string.IsNullOrEmpty(method.Description))
            {
                sb.AppendLine();
                sb.AppendLine(method.Description);
            }

            return MakeHover(sb.ToString(), token, mapper);
        }

        return null;
    }

    /// <summary>
    /// Hover for a member on a resolved Godot/API type (e.g. the <c>b</c> in <c>$controller.a.b</c>).
    /// </summary>
    private static HoverResult? HoverTypeMember(
        SyntaxToken token, string typeName, ProjectAnalyzer analyzer, PositionMapper mapper)
    {
        string memberName = token.Text;
        var propDesc = analyzer.GetPropertyInfo(typeName, memberName);

        // Fallback: GUML uses snake_case, but C# types store PascalCase
        if (propDesc == null)
        {
            string pascalName = KeyConverter.ToPascalCase(memberName);
            if (pascalName != memberName)
                propDesc = analyzer.GetPropertyInfo(typeName, pascalName);
        }

        if (propDesc != null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"**{memberName}**: `{propDesc.Type}` *(on {typeName})*");
            if (!string.IsNullOrEmpty(propDesc.Description))
                sb.AppendLine(propDesc.Description);
            return MakeHover(sb.ToString(), token, mapper);
        }

        return null;
    }

    /// <summary>
    /// Walks up nested <see cref="MemberAccessExpressionSyntax"/> nodes to find
    /// the innermost each-block variable reference, or null if not rooted in an each variable.
    /// </summary>
    private static EachVariableInfo? GetEachVariableRoot(
        MemberAccessExpressionSyntax memberAccess, SemanticModel semanticModel)
    {
        ExpressionSyntax expr = memberAccess.Expression;
        while (expr is MemberAccessExpressionSyntax inner)
            expr = inner.Expression;

        if (expr is not ReferenceExpressionSyntax refExpr) return null;
        return semanticModel.FindEachVariable(refExpr.Identifier);
    }

    /// <summary>
    /// Walks up nested <see cref="MemberAccessExpressionSyntax"/> nodes to check if
    /// the chain is rooted in a named node alias (@xxx), returning the root token or null.
    /// </summary>
    private static SyntaxToken? GetAliasRoot(MemberAccessExpressionSyntax memberAccess)
    {
        ExpressionSyntax expr = memberAccess.Expression;
        while (expr is MemberAccessExpressionSyntax inner)
            expr = inner.Expression;

        if (expr is ReferenceExpressionSyntax { Identifier.Kind: SyntaxKind.AliasRefToken } refExpr)
            return refExpr.Identifier;
        return null;
    }

    // ── Helpers ──

    private static HoverResult MakeHover(string markdown, SyntaxToken token, PositionMapper mapper)
    {
        return new HoverResult
        {
            Contents = new MarkupContent { Kind = "markdown", Value = markdown },
            Range = mapper.GetRange(token.Span)
        };
    }

    /// <summary>
    /// Appends mapping info for the current operator only, showing whether
    /// the specific mapping being used is supported by this property.
    /// </summary>
    private static void AppendCurrentMappingInfo(
        System.Text.StringBuilder sb, string currentOperator, MappingConstraintDescriptor mapping)
    {
        bool isSupported = currentOperator switch
        {
            ":" => mapping.CanStaticMap,
            ":=" => mapping.CanBindDataToProperty,
            "=:" => mapping.CanBindPropertyToData,
            "<=>" => mapping.CanBindTwoWay,
            _ => false
        };

        string label = currentOperator switch
        {
            ":" => "`:` static",
            ":=" => "`:=` data→property",
            "=:" => "`=:` property→data",
            "<=>" => "`<=>` two-way",
            _ => $"`{currentOperator}`"
        };

        sb.AppendLine();
        sb.AppendLine(isSupported
            ? $"Mapping: {label}"
            : $"⚠️ Mapping `{currentOperator}` is not supported by this property");
    }

    /// <summary>
    /// Appends all enum values with descriptions when the property has enum constraints.
    /// </summary>
    private static void AppendEnumValues(System.Text.StringBuilder sb, PropertyDescriptor propDesc)
    {
        if (propDesc.EnumValues is not { Count: > 0 }) return;

        sb.AppendLine();
        sb.AppendLine("**Values:**");
        foreach (var ev in propDesc.EnumValues)
        {
            sb.Append($"- `.{ev.Name}` = `{ev.Value}`");
            if (!string.IsNullOrEmpty(ev.Description))
                sb.Append($" — {ev.Description}");
            sb.AppendLine();
        }
    }

    /// <summary>
    /// Appends the documentation comment text (if any) to the hover content.
    /// </summary>
    private static void AppendDocComment(System.Text.StringBuilder sb, DocumentationCommentSyntax? doc)
    {
        if (doc == null) return;
        string text = doc.GetDocumentationText();
        if (!string.IsNullOrWhiteSpace(text))
        {
            sb.AppendLine();
            sb.AppendLine(text);
        }
    }

    /// <summary>
    /// Provides hover info for a key inside an object literal (e.g. inside
    /// <c>theme_overrides: { font_color: ... }</c>) by resolving the target
    /// type from the enclosing property assignment.
    /// </summary>
    private static HoverResult? HoverObjectLiteralKey(
        SyntaxToken token, string keyName,
        ObjectLiteralExpressionSyntax objLit,
        ProjectAnalyzer analyzer, PositionMapper mapper)
    {
        // The ObjectLiteral should be the value of a PropertyAssignment
        if (objLit.Parent is not PropertyAssignmentSyntax outerProp) return null;

        string outerPropName = outerProp.Name.Text;
        string? componentType = HandlerUtils.FindEnclosingComponentType(outerProp);
        if (componentType == null) return null;

        var outerPropDesc = analyzer.GetPropertyInfo(componentType, outerPropName);
        if (outerPropDesc == null) return null;

        var targetType = analyzer.GetTypeInfo(outerPropDesc.Type);
        if (targetType == null) return null;

        if (targetType.Properties.TryGetValue(keyName, out var keyProp))
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"**{keyName}**: `{keyProp.Type}`");
            if (!string.IsNullOrEmpty(keyProp.Description))
                sb.AppendLine(keyProp.Description);
            return MakeHover(sb.ToString(), token, mapper);
        }

        return null;
    }
}
