namespace GUML.Shared.Syntax.Nodes.Expressions;

/// <summary>
/// A binary expression: <c>left op right</c> (e.g. <c>a + b</c>, <c>x == y</c>).
/// </summary>
public sealed class BinaryExpressionSyntax : ExpressionSyntax
{
    public ExpressionSyntax Left { get; }
    public SyntaxToken OperatorToken { get; }
    public ExpressionSyntax Right { get; }

    public BinaryExpressionSyntax(ExpressionSyntax left, SyntaxToken operatorToken, ExpressionSyntax right)
        : base(SyntaxKind.BinaryExpression)
    {
        Left = left;
        OperatorToken = operatorToken;
        Right = right;
    }

    public override int FullWidth =>
        Left.FullWidth + OperatorToken.FullWidth + Right.FullWidth;

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return Left;
        yield return OperatorToken;
        yield return Right;
    }

}
