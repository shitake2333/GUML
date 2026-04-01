using GUML.Analyzer.Utils;
using GUML.Analyzer.Workspace;
using GUML.Shared.Syntax;
using GUML.Shared.Syntax.Nodes;
using GUML.Shared.Syntax.Nodes.Expressions;

namespace GUML.Analyzer.Handlers;

/// <summary>
/// Provides rename support for GUML documents.
/// Handles <c>textDocument/prepareRename</c> and <c>textDocument/rename</c>.
/// </summary>
public static class RenameHandler
{
    // ── Symbol category ──

    private enum SymbolKind
    {
        None,
        ImportAlias,
        Param,
        Event,
        ControllerMember,
        EachVariable,
        NamedNode,
    }

    /// <summary>
    /// Validates whether the token at the given position is renamable
    /// and returns its range and current name.
    /// </summary>
    public static PrepareRenameResult? PrepareRename(
        GumlDocument document, SemanticModel? semanticModel, LspPosition position)
    {
        var mapper = new PositionMapper(document.Text);
        int offset = mapper.GetOffset(position);

        var token = document.Root.FindToken(offset);
        if (token == null || token.IsMissing) return null;

        var (kind, name, span) = IdentifySymbol(token, semanticModel);
        if (kind == SymbolKind.None) return null;

        // For named nodes, strip the '@' prefix from the placeholder
        string placeholder = kind == SymbolKind.NamedNode ? name[1..] : name;
        return new PrepareRenameResult { Range = mapper.GetRange(span), Placeholder = placeholder };
    }

    /// <summary>
    /// Computes all text edits needed to rename the symbol at the given position.
    /// </summary>
    public static WorkspaceEdit? GetRenameEdits(
        GumlDocument document, SemanticModel? semanticModel,
        LspPosition position, string newName, GumlWorkspace workspace)
    {
        var mapper = new PositionMapper(document.Text);
        int offset = mapper.GetOffset(position);

        var token = document.Root.FindToken(offset);
        if (token == null || token.IsMissing) return null;

        var (kind, name, _) = IdentifySymbol(token, semanticModel);
        if (kind == SymbolKind.None) return null;

        var spans = kind switch
        {
            SymbolKind.ImportAlias => CollectAliasReferences(document.Root, name),
            SymbolKind.Param => CollectParamReferences(document.Root, name),
            SymbolKind.Event => CollectEventReferences(document.Root, name),
            SymbolKind.ControllerMember => CollectControllerMemberReferences(document.Root, name),
            SymbolKind.EachVariable => CollectEachVariableReferences(token, name, semanticModel),
            SymbolKind.NamedNode => CollectNamedNodeReferences(document.Root, name),
            _ => null,
        };

        if (spans == null || spans.Count == 0) return null;

        var edits = new List<TextEdit>();
        foreach (var span in spans)
        {
            edits.Add(new TextEdit { Range = mapper.GetRange(span), NewText = newName });
        }

        var result = new WorkspaceEdit { Changes = { [document.Uri] = edits } };

        // Cross-file rename
        if (kind == SymbolKind.Param)
        {
            CollectCrossFileParamEdits(result, document, workspace, name, newName);
        }
        else if (kind == SymbolKind.Event)
        {
            CollectCrossFileEventEdits(result, document, workspace, name, newName);
        }

        return result;
    }

    // ══════════════════════════════════════════════════════════
    //  Symbol identification
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Identifies the renamable symbol at the given token.
    /// Returns the symbol kind, the canonical name (without prefixes), and the span to highlight.
    /// </summary>
    private static (SymbolKind Kind, string Name, TextSpan Span) IdentifySymbol(
        SyntaxToken token, SemanticModel? semanticModel)
    {
        switch (token.Kind)
        {
            // import "path" as AliasName — cursor on alias name
            case SyntaxKind.ComponentNameToken when token.Parent is ImportAliasSyntax:
                return (SymbolKind.ImportAlias, token.Text, token.Span);

            // ComponentName { } — cursor on a component type name that matches an import alias
            case SyntaxKind.ComponentNameToken when token.Parent is ComponentDeclarationSyntax:
                if (IsImportAlias(token))
                    return (SymbolKind.ImportAlias, token.Text, token.Span);
                break;

            // param Type name — cursor on the param name
            case SyntaxKind.IdentifierToken when token.Parent is ParameterDeclarationSyntax paramDecl
                                                 && token == paramDecl.Name:
                return (SymbolKind.Param, token.Text, token.Span);

            // event name — cursor on the event name
            case SyntaxKind.IdentifierToken when token.Parent is EventDeclarationSyntax eventDecl
                                                 && token == eventDecl.Name:
                return (SymbolKind.Event, token.Text, token.Span);

            // #event_name: handler — cursor on the event ref (includes # prefix)
            case SyntaxKind.EventRefToken when token.Parent is EventSubscriptionSyntax:
                {
                    string eventName = token.Text.TrimStart('#');
                    // Compute span excluding the '#' prefix
                    var span = new TextSpan(token.Span.Start + 1, token.Span.Length - 1);
                    return (SymbolKind.Event, eventName, span);
                }

            // $controller.member — cursor on the member name after $controller
            case SyntaxKind.IdentifierToken when token.Parent is MemberAccessExpressionSyntax memberAccess
                                                 && token == memberAccess.Name
                                                 && IsControllerDirectMember(memberAccess):
                return (SymbolKind.ControllerMember, token.Text, token.Span);

            // $root.param — cursor on the member name after $root
            case SyntaxKind.IdentifierToken when token.Parent is MemberAccessExpressionSyntax rootAccess
                                                 && token == rootAccess.Name
                                                 && IsRootDirectMember(rootAccess):
                return (SymbolKind.Param, token.Text, token.Span);

            // paramName => Component { } — cursor on template param assignment name
            case SyntaxKind.IdentifierToken when token.Parent is TemplateParamAssignmentSyntax:
                return (SymbolKind.Param, token.Text, token.Span);

            // each variable declaration: |idx, item|
            case SyntaxKind.IdentifierToken when token.Parent is EachBlockSyntax each
                                                 && (token == each.IndexName || token == each.ValueName):
                return (SymbolKind.EachVariable, token.Text, token.Span);

            // each variable usage in body
            case SyntaxKind.IdentifierToken when token.Parent is ReferenceExpressionSyntax:
                {
                    var eachVar = semanticModel?.FindEachVariable(token);
                    if (eachVar != null)
                        return (SymbolKind.EachVariable, token.Text, token.Span);
                    break;
                }

            // each variable used as root of member access: item.xxx
            case SyntaxKind.IdentifierToken when token.Parent is MemberAccessExpressionSyntax ma
                                                 && IsEachVariableRoot(ma, token, semanticModel):
                return (SymbolKind.EachVariable, token.Text, token.Span);

            // @alias_name — named node declaration or reference
            case SyntaxKind.AliasRefToken:
            {
                string aliasName = token.Text;
                var span = new TextSpan(token.Span.Start + 1, token.Span.Length - 1);
                return (SymbolKind.NamedNode, aliasName, span);
            }
        }

        return (SymbolKind.None, "", default);
    }

    // ══════════════════════════════════════════════════════════
    //  Reference collection
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Collects all references to an import alias (declaration + component usage sites).
    /// </summary>
    private static List<TextSpan> CollectAliasReferences(GumlDocumentSyntax root, string aliasName)
    {
        var spans = new List<TextSpan>();

        foreach (var token in root.DescendantTokens())
        {
            if (token.IsMissing || token.Kind != SyntaxKind.ComponentNameToken) continue;
            if (token.Text != aliasName) continue;

            // Alias declaration in import
            if (token.Parent is ImportAliasSyntax)
            {
                spans.Add(token.Span);
                continue;
            }

            // Component usage
            if (token.Parent is ComponentDeclarationSyntax)
            {
                spans.Add(token.Span);
            }
        }

        return spans;
    }

    /// <summary>
    /// Collects all references to a param declaration (declaration + $root.xxx + template assignments).
    /// </summary>
    private static List<TextSpan> CollectParamReferences(GumlDocumentSyntax root, string paramName)
    {
        var spans = new List<TextSpan>();

        // Param declaration
        foreach (var node in root.DescendantNodes())
        {
            if (node is ParameterDeclarationSyntax paramDecl && paramDecl.Name.Text == paramName)
            {
                spans.Add(paramDecl.Name.Span);
            }
        }

        // $root.xxx member access and template param assignments
        foreach (var token in root.DescendantTokens())
        {
            if (token.IsMissing || token.Kind != SyntaxKind.IdentifierToken) continue;
            if (token.Text != paramName) continue;

            // $root.paramName
            if (token.Parent is MemberAccessExpressionSyntax memberAccess
                && token == memberAccess.Name
                && IsRootDirectMember(memberAccess))
            {
                spans.Add(token.Span);
                continue;
            }

            // paramName => Component { } (template param assignment)
            if (token.Parent is TemplateParamAssignmentSyntax)
            {
                spans.Add(token.Span);
            }
        }

        return spans;
    }

    /// <summary>
    /// Collects all references to an event (declaration + #event subscription sites).
    /// For event subscriptions, the span excludes the '#' prefix.
    /// </summary>
    private static List<TextSpan> CollectEventReferences(GumlDocumentSyntax root, string eventName)
    {
        var spans = new List<TextSpan>();

        // Event declaration
        foreach (var node in root.DescendantNodes())
        {
            if (node is EventDeclarationSyntax eventDecl && eventDecl.Name.Text == eventName)
            {
                spans.Add(eventDecl.Name.Span);
            }
        }

        // #event_name subscriptions
        foreach (var token in root.DescendantTokens())
        {
            if (token.IsMissing || token.Kind != SyntaxKind.EventRefToken) continue;
            if (token.Parent is not EventSubscriptionSyntax) continue;

            string refName = token.Text.TrimStart('#');
            if (refName != eventName) continue;

            // Span excluding '#' prefix
            spans.Add(new TextSpan(token.Span.Start + 1, token.Span.Length - 1));
        }

        return spans;
    }

    /// <summary>
    /// Collects all references to a $controller member (first-level member access only).
    /// </summary>
    private static List<TextSpan> CollectControllerMemberReferences(GumlDocumentSyntax root, string memberName)
    {
        var spans = new List<TextSpan>();

        foreach (var token in root.DescendantTokens())
        {
            if (token.IsMissing || token.Kind != SyntaxKind.IdentifierToken) continue;
            if (token.Text != memberName) continue;

            if (token.Parent is MemberAccessExpressionSyntax memberAccess
                && token == memberAccess.Name
                && IsControllerDirectMember(memberAccess))
            {
                spans.Add(token.Span);
            }
        }

        return spans;
    }

    /// <summary>
    /// Collects all references to a named node alias (declaration + all usage sites).
    /// Named nodes have document-wide scope.
    /// </summary>
    private static List<TextSpan> CollectNamedNodeReferences(GumlDocumentSyntax root, string aliasName)
    {
        var spans = new List<TextSpan>();

        foreach (var token in root.DescendantTokens())
        {
            if (token.IsMissing || token.Kind != SyntaxKind.AliasRefToken) continue;
            if (token.Text != aliasName) continue;

            // Replace only the name part after '@'
            spans.Add(new TextSpan(token.Span.Start + 1, token.Span.Length - 1));
        }

        return spans;
    }

    /// <summary>
    /// Collects all references to an each-block variable (declaration + body usage sites).
    /// Scope-aware: only collects references that resolve to the same <see cref="EachBlockSyntax"/>.
    /// </summary>
    private static List<TextSpan>? CollectEachVariableReferences(
        SyntaxToken originToken, string varName, SemanticModel? semanticModel)
    {
        if (semanticModel == null) return null;

        // Find the owning EachBlockSyntax
        EachBlockSyntax? ownerEach = FindOwnerEachBlock(originToken, varName, semanticModel);
        if (ownerEach == null) return null;

        var spans = new List<TextSpan>();

        // Declaration site
        if (ownerEach.IndexName != null && ownerEach.IndexName.Text == varName)
            spans.Add(ownerEach.IndexName.Span);
        else if (ownerEach.ValueName != null && ownerEach.ValueName.Text == varName)
            spans.Add(ownerEach.ValueName.Span);

        // Body usage sites — scan all tokens in the each block body
        if (ownerEach.Body != null)
        {
            foreach (var bodyNode in ownerEach.Body)
            {
                foreach (var token in bodyNode.DescendantTokens())
                {
                    if (token.IsMissing || token.Kind != SyntaxKind.IdentifierToken) continue;
                    if (token.Text != varName) continue;

                    // Verify this token resolves to the same EachBlockSyntax
                    var resolved = semanticModel.FindEachVariable(token);
                    if (resolved != null && ReferenceEquals(resolved.EachBlock, ownerEach))
                    {
                        spans.Add(token.Span);
                    }
                }
            }
        }

        return spans;
    }

    // ══════════════════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Checks whether a member access is a direct child of $controller (i.e. $controller.xxx).
    /// </summary>
    private static bool IsControllerDirectMember(MemberAccessExpressionSyntax memberAccess)
    {
        return memberAccess.Expression is ReferenceExpressionSyntax
               {
                   Identifier: { Kind: SyntaxKind.GlobalRefToken, Text: "$controller" }
               };
    }

    /// <summary>
    /// Checks whether a member access is a direct child of $root (i.e. $root.xxx).
    /// </summary>
    private static bool IsRootDirectMember(MemberAccessExpressionSyntax memberAccess)
    {
        return memberAccess.Expression is ReferenceExpressionSyntax
               {
                   Identifier: { Kind: SyntaxKind.GlobalRefToken, Text: "$root" }
               };
    }

    /// <summary>
    /// Checks whether the given token is used as an import alias name in the document.
    /// </summary>
    private static bool IsImportAlias(SyntaxToken token)
    {
        // Walk up to GumlDocumentSyntax to find imports
        var root = token.Parent;
        while (root != null && root is not GumlDocumentSyntax)
            root = root.Parent;

        if (root is not GumlDocumentSyntax doc) return false;

        foreach (var node in doc.DescendantNodes())
        {
            if (node is ImportDirectiveSyntax { Alias: not null } import
                && import.Alias.Name.Text == token.Text)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether the given token is the root of a member access chain
    /// and resolves to an each-block variable.
    /// </summary>
    private static bool IsEachVariableRoot(
        MemberAccessExpressionSyntax memberAccess, SyntaxToken token, SemanticModel? semanticModel)
    {
        if (semanticModel == null) return false;

        // The token must be the identifier of the root ReferenceExpression
        if (memberAccess.Expression is ReferenceExpressionSyntax refExpr
            && refExpr.Identifier == token)
        {
            return semanticModel.FindEachVariable(token) != null;
        }

        return false;
    }

    /// <summary>
    /// Finds the EachBlockSyntax that owns the given variable name,
    /// starting from the origin token.
    /// </summary>
    private static EachBlockSyntax? FindOwnerEachBlock(
        SyntaxToken token, string varName, SemanticModel semanticModel)
    {
        // If the token is directly on the declaration (EachBlockSyntax parent)
        if (token.Parent is EachBlockSyntax each)
        {
            if ((each.IndexName != null && each.IndexName.Text == varName)
                || (each.ValueName != null && each.ValueName.Text == varName))
            {
                return each;
            }
        }

        // Otherwise, resolve via SemanticModel
        var info = semanticModel.FindEachVariable(token);
        return info?.EachBlock;
    }

    // ══════════════════════════════════════════════════════════
    //  Cross-file
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Scans all open documents in the workspace for cross-file references to a param
    /// (via <see cref="TemplateParamAssignmentSyntax"/>) and appends edits to <paramref name="result"/>.
    /// </summary>
    private static void CollectCrossFileParamEdits(
        WorkspaceEdit result, GumlDocument sourceDoc,
        GumlWorkspace workspace, string paramName, string newName)
    {
        string sourcePath = PathUtils.UriToFilePath(sourceDoc.Uri);

        foreach (var otherDoc in workspace.GetAllGumlFilesFromDisk())
        {
            string otherPath = PathUtils.UriToFilePath(otherDoc.Uri);
            if (string.Equals(otherPath, sourcePath, StringComparison.OrdinalIgnoreCase))
                continue;
            string? otherDir = Path.GetDirectoryName(otherPath);
            if (otherDir == null) continue;

            // Check if this document imports the source document
            string? importedComponentName = null;
            foreach (var import in otherDoc.Root.Imports)
            {
                string importPath = import.Path.Text.Trim('"');
                string resolved = Path.GetFullPath(Path.Combine(otherDir, importPath));
                if (string.Equals(resolved, sourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    importedComponentName = import.Alias?.Name.Text
                                            ?? Path.GetFileNameWithoutExtension(importPath);
                    break;
                }
            }

            if (importedComponentName == null) continue;

            // Collect param references in matching components:
            // PropertyAssignment (name: value), MappingAssignment (name := value),
            // and TemplateParamAssignment (name => Component { })
            var mapper = new PositionMapper(otherDoc.Text);
            var edits = new List<TextEdit>();

            foreach (var token in otherDoc.Root.DescendantTokens())
            {
                if (token.IsMissing || token.Kind != SyntaxKind.IdentifierToken) continue;
                if (token.Text != paramName) continue;

                // Check if this token is the Name of a param-passing assignment
                bool isParamAssignment = token.Parent is TemplateParamAssignmentSyntax
                    || (token.Parent is PropertyAssignmentSyntax propAssign && token == propAssign.Name)
                    || (token.Parent is MappingAssignmentSyntax mapAssign && token == mapAssign.Name);
                if (!isParamAssignment) continue;

                // Verify the assignment belongs to the imported component
                var parent = token.Parent?.Parent;
                if (parent is ComponentDeclarationSyntax comp
                    && comp.TypeName.Text == importedComponentName)
                {
                    edits.Add(new TextEdit
                    {
                        Range = mapper.GetRange(token.Span),
                        NewText = newName
                    });
                }
            }

            if (edits.Count > 0)
            {
                result.Changes[otherDoc.Uri] = edits;
            }
        }
    }

    /// <summary>
    /// Scans all GUML files in the workspace for cross-file references to an event
    /// (via <see cref="EventSubscriptionSyntax"/>) and appends edits to <paramref name="result"/>.
    /// </summary>
    private static void CollectCrossFileEventEdits(
        WorkspaceEdit result, GumlDocument sourceDoc,
        GumlWorkspace workspace, string eventName, string newName)
    {
        string sourcePath = PathUtils.UriToFilePath(sourceDoc.Uri);

        foreach (var otherDoc in workspace.GetAllGumlFilesFromDisk())
        {
            string otherPath = PathUtils.UriToFilePath(otherDoc.Uri);
            if (string.Equals(otherPath, sourcePath, StringComparison.OrdinalIgnoreCase))
                continue;

            string? otherDir = Path.GetDirectoryName(otherPath);
            if (otherDir == null) continue;

            // Check if this document imports the source document
            string? importedComponentName = null;
            foreach (var import in otherDoc.Root.Imports)
            {
                string importPath = import.Path.Text.Trim('"');
                string resolved = Path.GetFullPath(Path.Combine(otherDir, importPath));
                if (string.Equals(resolved, sourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    importedComponentName = import.Alias?.Name.Text
                                            ?? Path.GetFileNameWithoutExtension(importPath);
                    break;
                }
            }

            if (importedComponentName == null) continue;

            // Collect EventSubscription references in matching components
            var mapper = new PositionMapper(otherDoc.Text);
            var edits = new List<TextEdit>();

            foreach (var token in otherDoc.Root.DescendantTokens())
            {
                if (token.IsMissing || token.Kind != SyntaxKind.EventRefToken) continue;
                if (token.Parent is not EventSubscriptionSyntax) continue;

                string refName = token.Text.TrimStart('#');
                if (refName != eventName) continue;

                // Verify the subscription belongs to the imported component
                var parent = token.Parent?.Parent;
                if (parent is ComponentDeclarationSyntax comp
                    && comp.TypeName.Text == importedComponentName)
                {
                    // Replace only the name part after '#'
                    var nameSpan = new TextSpan(token.Span.Start + 1, token.Span.Length - 1);
                    edits.Add(new TextEdit
                    {
                        Range = mapper.GetRange(nameSpan),
                        NewText = newName
                    });
                }
            }

            if (edits.Count > 0)
            {
                result.Changes[otherDoc.Uri] = edits;
            }
        }
    }

}
