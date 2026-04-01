namespace GUML.Shared.Syntax.Nodes.Expressions;

/// <summary>
/// A prefix unary expression: <c>!expr</c>, <c>+expr</c>, or <c>-expr</c>.
/// </summary>
public sealed class PrefixUnaryExpressionSyntax : ExpressionSyntax
{
    public SyntaxToken OperatorToken { get; }
    public ExpressionSyntax Operand { get; }

    public PrefixUnaryExpressionSyntax(SyntaxToken operatorToken, ExpressionSyntax operand)
        : base(SyntaxKind.PrefixUnaryExpression)
    {
        OperatorToken = operatorToken;
        Operand = operand;
    }

    public override int FullWidth =>
        OperatorToken.FullWidth + Operand.FullWidth;

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return OperatorToken;
        yield return Operand;
    }

}
