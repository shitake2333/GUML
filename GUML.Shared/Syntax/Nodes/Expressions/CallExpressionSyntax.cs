namespace GUML.Shared.Syntax.Nodes.Expressions;

/// <summary>
/// A function call expression: <c>expr(arg1, arg2, ...)</c>
/// </summary>
public sealed class CallExpressionSyntax : ExpressionSyntax
{
    public ExpressionSyntax Expression { get; }
    public SyntaxToken OpenParen { get; }
    public SeparatedSyntaxList<ExpressionSyntax> Arguments { get; }
    public SyntaxToken CloseParen { get; }

    public CallExpressionSyntax(
        ExpressionSyntax expression,
        SyntaxToken openParen,
        SeparatedSyntaxList<ExpressionSyntax> arguments,
        SyntaxToken closeParen)
        : base(SyntaxKind.CallExpression)
    {
        Expression = expression;
        OpenParen = openParen;
        Arguments = arguments;
        CloseParen = closeParen;
    }

    public override int FullWidth
    {
        get
        {
            int w = Expression.FullWidth + OpenParen.FullWidth;
            w += Arguments.FullWidth;
            w += CloseParen.FullWidth;
            return w;
        }
    }

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return Expression;
        yield return OpenParen;
        foreach (var child in Arguments.ChildNodesAndTokens())
            yield return child;
        yield return CloseParen;
    }

}
