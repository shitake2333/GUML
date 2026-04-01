namespace GUML.Shared.Syntax.Nodes.Expressions;

/// <summary>
/// A conditional (ternary) expression: <c>condition ? whenTrue : whenFalse</c>
/// </summary>
public sealed class ConditionalExpressionSyntax : ExpressionSyntax
{
    public ExpressionSyntax Condition { get; }
    public SyntaxToken QuestionToken { get; }
    public ExpressionSyntax WhenTrue { get; }
    public SyntaxToken ColonToken { get; }
    public ExpressionSyntax WhenFalse { get; }

    public ConditionalExpressionSyntax(
        ExpressionSyntax condition,
        SyntaxToken questionToken,
        ExpressionSyntax whenTrue,
        SyntaxToken colonToken,
        ExpressionSyntax whenFalse)
        : base(SyntaxKind.ConditionalExpression)
    {
        Condition = condition;
        QuestionToken = questionToken;
        WhenTrue = whenTrue;
        ColonToken = colonToken;
        WhenFalse = whenFalse;
    }

    public override int FullWidth =>
        Condition.FullWidth + QuestionToken.FullWidth + WhenTrue.FullWidth
        + ColonToken.FullWidth + WhenFalse.FullWidth;

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return Condition;
        yield return QuestionToken;
        yield return WhenTrue;
        yield return ColonToken;
        yield return WhenFalse;
    }

}
