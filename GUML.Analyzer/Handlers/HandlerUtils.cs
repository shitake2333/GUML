using GUML.Shared.Syntax;
using GUML.Shared.Syntax.Nodes;
using GUML.Shared.Syntax.Nodes.Expressions;

namespace GUML.Analyzer.Handlers;

/// <summary>
/// Shared utility methods used across multiple LSP handlers.
/// </summary>
internal static class HandlerUtils
{
    /// <summary>
    /// Walks up nested <see cref="MemberAccessExpressionSyntax"/> nodes to find
    /// the innermost <c>$controller</c> reference, or null if not rooted in $controller.
    /// </summary>
    internal static ReferenceExpressionSyntax? GetControllerRoot(MemberAccessExpressionSyntax memberAccess)
    {
        ExpressionSyntax expr = memberAccess.Expression;
        while (expr is MemberAccessExpressionSyntax inner)
            expr = inner.Expression;

        return expr is ReferenceExpressionSyntax { Identifier.Text: "$controller" } refExpr ? refExpr : null;
    }

    /// <summary>
    /// Walks up the syntax tree to find the enclosing component type name.
    /// </summary>
    internal static string? FindEnclosingComponentType(SyntaxNode? node)
    {
        for (var current = node; current != null; current = current.Parent)
        {
            if (current is ComponentDeclarationSyntax comp)
                return comp.TypeName.Text;
        }

        return null;
    }

    /// <summary>
    /// Walks up the syntax tree to find the enclosing property or mapping assignment name.
    /// </summary>
    internal static string? FindPropertyName(SyntaxNode? node)
    {
        for (var current = node; current != null; current = current.Parent)
        {
            if (current is PropertyAssignmentSyntax prop)
                return prop.Name.Text;
            if (current is MappingAssignmentSyntax mapping)
                return mapping.Name.Text;
        }

        return null;
    }

    /// <summary>
    /// Walks up the syntax tree to find the root <see cref="GumlDocumentSyntax"/> node.
    /// </summary>
    internal static GumlDocumentSyntax? GetDocumentRoot(SyntaxNode? node)
    {
        for (var current = node; current != null; current = current.Parent)
        {
            if (current is GumlDocumentSyntax doc)
                return doc;
        }

        return null;
    }
}
