namespace GUML.Shared.Syntax.Nodes.Expressions;

/// <summary>
/// A reference expression: plain identifier, <c>$globalRef</c>, or <c>@aliasRef</c>.
/// </summary>
public sealed class ReferenceExpressionSyntax : ExpressionSyntax
{
    public SyntaxToken Identifier { get; }

    public ReferenceExpressionSyntax(SyntaxToken identifier)
        : base(SyntaxKind.ReferenceExpression)
    {
        Identifier = identifier;
    }

    public override int FullWidth => Identifier.FullWidth;

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return Identifier;
    }
}
