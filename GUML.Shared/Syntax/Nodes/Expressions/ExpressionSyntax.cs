namespace GUML.Shared.Syntax.Nodes.Expressions;

/// <summary>
/// Abstract base class for all expression syntax nodes.
/// </summary>
public abstract class ExpressionSyntax : SyntaxNode
{
    protected ExpressionSyntax(SyntaxKind kind) : base(kind) { }
}
