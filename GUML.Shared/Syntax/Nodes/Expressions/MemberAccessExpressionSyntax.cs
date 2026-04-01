namespace GUML.Shared.Syntax.Nodes.Expressions;

/// <summary>
/// A member access expression: <c>expr.name</c>
/// </summary>
public sealed class MemberAccessExpressionSyntax : ExpressionSyntax
{
    public ExpressionSyntax Expression { get; }
    public SyntaxToken DotToken { get; }
    public SyntaxToken Name { get; }

    public MemberAccessExpressionSyntax(ExpressionSyntax expression, SyntaxToken dotToken, SyntaxToken name)
        : base(SyntaxKind.MemberAccessExpression)
    {
        Expression = expression;
        DotToken = dotToken;
        Name = name;
    }

    public override int FullWidth =>
        Expression.FullWidth + DotToken.FullWidth + Name.FullWidth;

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return Expression;
        yield return DotToken;
        yield return Name;
    }

}
