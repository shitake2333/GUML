namespace GUML.Shared.Syntax.Nodes.Expressions;

/// <summary>
/// A literal expression: string, integer, float, bool, or null.
/// </summary>
public sealed class LiteralExpressionSyntax : ExpressionSyntax
{
    public SyntaxToken Token { get; }

    public LiteralExpressionSyntax(SyntaxToken token)
        : base(SyntaxKind.LiteralExpression)
    {
        Token = token;
    }

    public override int FullWidth => Token.FullWidth;

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return Token;
    }
}
