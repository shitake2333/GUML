using GUML.Analyzer.Utils;
using GUML.Analyzer.Workspace;
using GUML.Shared.Converter;
using GUML.Shared.Syntax;
using GUML.Shared.Syntax.Nodes;
using GUML.Shared.Syntax.Nodes.Expressions;
using Serilog;

namespace GUML.Analyzer.Handlers;

/// <summary>
/// Provides go-to-definition locations for symbols in a GUML document.
/// </summary>
public static class DefinitionHandler
{
    /// <summary>
    /// Returns definition locations for the token at the given position.
    /// </summary>
    public static List<LspLocation> GetDefinitions(
        GumlDocument document, SemanticModel? semanticModel, LspPosition position,
        ProjectAnalyzer analyzer)
    {
        var results = new List<LspLocation>();
        var mapper = new PositionMapper(document.Text);
        int offset = mapper.GetOffset(position);

        var token = document.Root.FindToken(offset);
        if (token == null || token.IsMissing) return results;

        switch (token.Kind)
        {
            case SyntaxKind.StringLiteralToken when token.Parent is ImportDirectiveSyntax import:
                ResolveImportPath(results, import, document.Uri);
                break;

            case SyntaxKind.GlobalRefToken when token.Text == "$controller":
                ResolveController(results, semanticModel);
                break;

            case SyntaxKind.IdentifierToken when token.Parent is MemberAccessExpressionSyntax memberAccess
                                                 && token == memberAccess.Name:
                ResolveMemberAccess(results, token, memberAccess, semanticModel, document);
                break;

            case SyntaxKind.IdentifierToken when token.Parent is ReferenceExpressionSyntax:
                ResolveParamUsage(results, token, document);
                if (results.Count == 0)
                    ResolveEachVariableUsage(results, token, document, semanticModel);
                break;

            case SyntaxKind.IdentifierToken when token.Parent is PropertyAssignmentSyntax propAssign
                                                 && token == propAssign.Name:
                ResolveImportedPropertyDeclaration(results, token, document, semanticModel);
                if (results.Count == 0)
                    ResolveBuiltinPropertyDeclaration(results, token, analyzer);
                break;

            case SyntaxKind.ComponentNameToken:
                ResolveImportedComponent(results, token, document);
                if (results.Count == 0)
                    ResolveBuiltinComponent(results, token, analyzer);
                break;

            case SyntaxKind.AliasRefToken:
                ResolveAlias(results, token, document, semanticModel);
                break;

            case SyntaxKind.EventRefToken:
                ResolveEventRef(results, token, document, semanticModel);
                break;
        }

        return results;
    }

    private static void ResolveImportPath(List<LspLocation> results, ImportDirectiveSyntax import, string documentUri)
    {
        string pathText = import.Path.Text;
        // Strip surrounding quotes
        if (pathText is ['"', _, ..] && pathText[^1] == '"')
            pathText = pathText[1..^1];

        if (string.IsNullOrEmpty(pathText)) return;

        // Resolve relative to the current document's directory
        string docPath = PathUtils.UriToFilePath(documentUri);
        if (string.IsNullOrEmpty(docPath)) return;

        string? dir = Path.GetDirectoryName(docPath);
        if (dir == null) return;

        string resolved = Path.GetFullPath(Path.Combine(dir, pathText));
        if (!File.Exists(resolved)) return;

        results.Add(new LspLocation
        {
            Uri = PathUtils.FilePathToUri(resolved), Range = new LspRange(new LspPosition(0, 0), new LspPosition(0, 0))
        });
    }

    private static void ResolveController(List<LspLocation> results, SemanticModel? semanticModel)
    {
        var controller = semanticModel?.GetController();
        if (controller?.SourceFile is not { Length: > 0 } srcFile) return;

        var pos = new LspPosition(controller.SourceLine, 0);
        results.Add(new LspLocation { Uri = PathUtils.FilePathToUri(srcFile), Range = new LspRange(pos, pos) });
    }

    private static void ResolveMemberAccess(
        List<LspLocation> results, SyntaxToken token,
        MemberAccessExpressionSyntax memberAccess, SemanticModel? semanticModel,
        GumlDocument document)
    {
        // $controller.xxx or $controller.a.b — jump to the C# member declaration
        // For chained access, only the first segment can jump to controller source.
        var root = HandlerUtils.GetControllerRoot(memberAccess);
        if (root != null)
        {
            var controller = semanticModel?.GetController();
            if (controller?.SourceFile is not { Length: > 0 } srcFile) return;

            // Find which segment of the chain this token belongs to (the direct memberAccess)
            string pascalName = KeyConverter.ToPascalCase(token.Text);
            if (controller.MemberSourceLines.TryGetValue(pascalName, out var srcPos))
            {
                var pos = new LspPosition(srcPos.Line, srcPos.Column);
                results.Add(new LspLocation { Uri = PathUtils.FilePathToUri(srcFile), Range = new LspRange(pos, pos) });
            }

            return;
        }

        // item.xxx — jump to the each variable declaration
        if (semanticModel != null)
        {
            ExpressionSyntax expr = memberAccess.Expression;
            while (expr is MemberAccessExpressionSyntax inner)
                expr = inner.Expression;

            if (expr is ReferenceExpressionSyntax refExpr)
            {
                var eachVar = semanticModel.FindEachVariable(refExpr.Identifier);
                if (eachVar != null)
                {
                    var mapper = new PositionMapper(document.Text);
                    SyntaxToken? declToken = eachVar.IsIndex
                        ? eachVar.EachBlock.IndexName
                        : eachVar.EachBlock.ValueName;
                    if (declToken != null)
                    {
                        results.Add(new LspLocation { Uri = document.Uri, Range = mapper.GetRange(declToken.Span) });
                    }

                    return;
                }
            }
        }

        // $root.param_name → jump to the param/event declaration in this document
        if (memberAccess.Expression is ReferenceExpressionSyntax { Identifier.Text: "$root" })
        {
            string memberName = token.Text;
            var mapper = new PositionMapper(document.Text);

            foreach (var member in document.Root.RootComponent.Members)
            {
                if (member is ParameterDeclarationSyntax param && param.Name.Text == memberName)
                {
                    results.Add(new LspLocation { Uri = document.Uri, Range = mapper.GetRange(param.Name.Span) });
                    return;
                }

                if (member is EventDeclarationSyntax evt && evt.Name.Text == memberName)
                {
                    results.Add(new LspLocation { Uri = document.Uri, Range = mapper.GetRange(evt.Name.Span) });
                    return;
                }
            }
        }

        // @alias.xxx — jump to the alias declaration
        if (semanticModel != null)
        {
            ExpressionSyntax aliasExpr = memberAccess.Expression;
            while (aliasExpr is MemberAccessExpressionSyntax aliasInner)
                aliasExpr = aliasInner.Expression;

            if (aliasExpr is ReferenceExpressionSyntax { Identifier.Kind: SyntaxKind.AliasRefToken } aliasRef)
            {
                var alias = semanticModel.FindAlias(aliasRef.Identifier.Text);
                if (alias != null)
                {
                    var mapper = new PositionMapper(document.Text);
                    results.Add(new LspLocation
                    {
                        Uri = document.Uri,
                        Range = mapper.GetRange(alias.DeclarationToken.Span)
                    });
                }
            }
        }
    }

    private static void ResolveParamUsage(List<LspLocation> results, SyntaxToken token, GumlDocument document)
    {
        // Find the parameter declaration with the same name in this document
        string name = token.Text;
        var mapper = new PositionMapper(document.Text);

        foreach (var node in document.Root.DescendantNodes())
        {
            if (node is ParameterDeclarationSyntax paramDecl && paramDecl.Name.Text == name)
            {
                results.Add(new LspLocation { Uri = document.Uri, Range = mapper.GetRange(paramDecl.Name.Span) });
                break;
            }
        }
    }

    private static void ResolveEachVariableUsage(
        List<LspLocation> results, SyntaxToken token, GumlDocument document, SemanticModel? semanticModel)
    {
        if (semanticModel == null) return;

        var eachVar = semanticModel.FindEachVariable(token);
        if (eachVar == null) return;

        var mapper = new PositionMapper(document.Text);
        SyntaxToken? declToken = eachVar.IsIndex
            ? eachVar.EachBlock.IndexName
            : eachVar.EachBlock.ValueName;

        if (declToken != null)
        {
            results.Add(new LspLocation { Uri = document.Uri, Range = mapper.GetRange(declToken.Span) });
        }
    }

    private static void ResolveImportedComponent(List<LspLocation> results, SyntaxToken token, GumlDocument document)
    {
        string typeName = token.Text;

        foreach (var import in document.Root.Imports)
        {
            // Match by alias or derived name (filename without extension)
            string? alias = import.Alias?.Name.Text;
            string pathText = import.Path.Text;
            if (pathText is ['"', _, ..] && pathText[^1] == '"')
                pathText = pathText[1..^1];

            string derivedName = Path.GetFileNameWithoutExtension(pathText);
            if (typeName != alias && typeName != derivedName) continue;

            if (string.IsNullOrEmpty(pathText)) continue;

            string docPath = PathUtils.UriToFilePath(document.Uri);
            string? dir = Path.GetDirectoryName(docPath);
            if (dir == null) continue;

            string resolved = Path.GetFullPath(Path.Combine(dir, pathText));
            if (!File.Exists(resolved)) continue;

            results.Add(new LspLocation
            {
                Uri = PathUtils.FilePathToUri(resolved),
                Range = new LspRange(new LspPosition(0, 0), new LspPosition(0, 0))
            });
            return;
        }
    }

    /// <summary>
    /// Resolves a component type name to its C# type definition using the Roslyn compilation.
    /// Works for both user-defined types (source navigation) and SDK types (metadata-as-source).
    /// </summary>
    private static void ResolveBuiltinComponent(
        List<LspLocation> results, SyntaxToken token, ProjectAnalyzer analyzer)
    {
        var location = analyzer.ResolveTypeSource(token.Text);
        if (location == null) return;

        var pos = new LspPosition(location.Line, location.Column);
        results.Add(new LspLocation
        {
            Uri = PathUtils.FilePathToUri(location.FilePath),
            Range = new LspRange(pos, pos)
        });
    }

    /// <summary>
    /// Resolves a property name inside a built-in (Godot SDK or user C#) component
    /// to its declaration in source code or a generated metadata-as-source file.
    /// Walks the inheritance chain to find the type that actually declares the property.
    /// </summary>
    private static void ResolveBuiltinPropertyDeclaration(
        List<LspLocation> results, SyntaxToken token, ProjectAnalyzer analyzer)
    {
        // Find enclosing component type
        string? componentType = HandlerUtils.FindEnclosingComponentType(token.Parent);
        if (componentType == null) return;

        // Convert GUML snake_case property name to C# PascalCase
        string pascalName = KeyConverter.ToPascalCase(token.Text);

        var location = analyzer.ResolvePropertySource(componentType, pascalName);
        if (location == null) return;

        var pos = new LspPosition(location.Line, location.Column);
        results.Add(new LspLocation
        {
            Uri = PathUtils.FilePathToUri(location.FilePath),
            Range = new LspRange(pos, pos)
        });
    }

    /// <summary>
    /// Resolves a property name inside an imported component to the param declaration
    /// in the imported .guml file.
    /// </summary>
    private static void ResolveImportedPropertyDeclaration(
        List<LspLocation> results, SyntaxToken token, GumlDocument document, SemanticModel? semanticModel)
    {
        if (semanticModel == null) return;

        // Find enclosing component
        string? componentType = null;
        for (var current = token.Parent; current != null; current = current.Parent)
        {
            if (current is ComponentDeclarationSyntax comp)
            {
                componentType = comp.TypeName.Text;
                break;
            }
        }

        if (componentType == null) return;

        // Check if it's an imported component
        string? importSourcePath = semanticModel.GetImportSourcePath(componentType);
        if (importSourcePath == null) return;

        // Resolve the file path
        string docPath = PathUtils.UriToFilePath(document.Uri);
        string? docDir = Path.GetDirectoryName(docPath);
        if (docDir == null) return;

        string resolved = Path.GetFullPath(Path.Combine(docDir, importSourcePath));
        if (!File.Exists(resolved)) return;

        // Parse the imported file and find the param/event declaration
        try
        {
            string importedText = File.ReadAllText(resolved);
            var parseResult = GumlSyntaxTree.Parse(importedText);
            var rootComp = parseResult.Root.RootComponent;
            string propName = token.Text;

            var importedMapper = new PositionMapper(importedText);
            string importedUri = PathUtils.FilePathToUri(resolved);

            foreach (var member in rootComp.Members)
            {
                if (member is ParameterDeclarationSyntax param && param.Name.Text == propName)
                {
                    results.Add(new LspLocation
                    {
                        Uri = importedUri,
                        Range = importedMapper.GetRange(param.Name.Span)
                    });
                    return;
                }

                if (member is EventDeclarationSyntax evt && evt.Name.Text == propName)
                {
                    results.Add(new LspLocation
                    {
                        Uri = importedUri,
                        Range = importedMapper.GetRange(evt.Name.Span)
                    });
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Debug(ex, "Failed to resolve imported property declaration in {File}", resolved);
        }
    }

    private static void ResolveAlias(
        List<LspLocation> results, SyntaxToken token, GumlDocument document, SemanticModel? semanticModel)
    {
        if (semanticModel == null) return;

        var alias = semanticModel.FindAlias(token.Text);
        if (alias == null) return;

        // If we're already at the declaration, don't jump to self
        if (token.Parent is AliasPrefixSyntax) return;

        var mapper = new PositionMapper(document.Text);
        results.Add(new LspLocation
        {
            Uri = document.Uri,
            Range = mapper.GetRange(alias.DeclarationToken.Span)
        });
    }

    /// <summary>
    /// Resolves an event reference (<c>#event_name</c>) inside an imported component
    /// to the event declaration in the imported .guml file, or to the event declaration
    /// in the current document if the event belongs to the root component.
    /// </summary>
    private static void ResolveEventRef(
        List<LspLocation> results, SyntaxToken token, GumlDocument document, SemanticModel? semanticModel)
    {
        string eventName = token.Text.StartsWith("#") ? token.Text[1..] : token.Text;

        // Find enclosing component type
        string? componentType = HandlerUtils.FindEnclosingComponentType(token.Parent);
        if (componentType == null) return;

        // Check if the enclosing component is the root component — event declared in this file
        if (componentType == document.Root.RootComponent.TypeName.Text)
        {
            var mapper = new PositionMapper(document.Text);
            foreach (var member in document.Root.RootComponent.Members)
            {
                if (member is EventDeclarationSyntax evt && evt.Name.Text == eventName)
                {
                    results.Add(new LspLocation { Uri = document.Uri, Range = mapper.GetRange(evt.Name.Span) });
                    return;
                }
            }
        }

        // Check imported component — resolve to its source .guml file
        if (semanticModel == null) return;

        string? importSourcePath = semanticModel.GetImportSourcePath(componentType);
        if (importSourcePath == null) return;

        string docPath = PathUtils.UriToFilePath(document.Uri);
        string? docDir = Path.GetDirectoryName(docPath);
        if (docDir == null) return;

        string resolved = Path.GetFullPath(Path.Combine(docDir, importSourcePath));
        if (!File.Exists(resolved)) return;

        try
        {
            string importedText = File.ReadAllText(resolved);
            var parseResult = GumlSyntaxTree.Parse(importedText);
            var rootComp = parseResult.Root.RootComponent;

            var importedMapper = new PositionMapper(importedText);
            string importedUri = PathUtils.FilePathToUri(resolved);

            foreach (var member in rootComp.Members)
            {
                if (member is EventDeclarationSyntax evt && evt.Name.Text == eventName)
                {
                    results.Add(new LspLocation
                    {
                        Uri = importedUri,
                        Range = importedMapper.GetRange(evt.Name.Span)
                    });
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Debug(ex, "Failed to resolve event declaration in {File}", resolved);
        }
    }

}
