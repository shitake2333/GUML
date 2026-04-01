namespace GUML.Shared.Syntax.Nodes.Expressions;

/// <summary>
/// An enum value expression: <c>.PascalCase</c>
/// </summary>
public sealed class EnumValueExpressionSyntax : ExpressionSyntax
{
    public SyntaxToken Token { get; }

    public EnumValueExpressionSyntax(SyntaxToken token)
        : base(SyntaxKind.EnumValueExpression)
    {
        Token = token;
    }

    public override int FullWidth => Token.FullWidth;

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return Token;
    }
}
