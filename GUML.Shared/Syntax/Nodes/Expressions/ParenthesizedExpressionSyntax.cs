namespace GUML.Shared.Syntax.Nodes.Expressions;

/// <summary>
/// A parenthesized expression: <c>(expression)</c>
/// </summary>
public sealed class ParenthesizedExpressionSyntax : ExpressionSyntax
{
    public SyntaxToken OpenParen { get; }
    public ExpressionSyntax Expression { get; }
    public SyntaxToken CloseParen { get; }

    public ParenthesizedExpressionSyntax(SyntaxToken openParen, ExpressionSyntax expression, SyntaxToken closeParen)
        : base(SyntaxKind.ParenthesizedExpression)
    {
        OpenParen = openParen;
        Expression = expression;
        CloseParen = closeParen;
    }

    public override int FullWidth =>
        OpenParen.FullWidth + Expression.FullWidth + CloseParen.FullWidth;

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return OpenParen;
        yield return Expression;
        yield return CloseParen;
    }

}
