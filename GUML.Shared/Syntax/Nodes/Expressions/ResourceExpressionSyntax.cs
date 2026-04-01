namespace GUML.Shared.Syntax.Nodes.Expressions;

/// <summary>
/// A resource loading expression: <c>image("path")</c>, <c>font("path")</c>, <c>audio("path")</c>, or <c>video("path")</c>.
/// </summary>
public sealed class ResourceExpressionSyntax : ExpressionSyntax
{
    public SyntaxToken Keyword { get; }
    public SyntaxToken OpenParen { get; }
    public ExpressionSyntax Path { get; }
    public SyntaxToken CloseParen { get; }

    public ResourceExpressionSyntax(SyntaxToken keyword, SyntaxToken openParen, ExpressionSyntax path,
        SyntaxToken closeParen)
        : base(SyntaxKind.ResourceExpression)
    {
        Keyword = keyword;
        OpenParen = openParen;
        Path = path;
        CloseParen = closeParen;
    }

    public override int FullWidth =>
        Keyword.FullWidth + OpenParen.FullWidth + Path.FullWidth + CloseParen.FullWidth;

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return Keyword;
        yield return OpenParen;
        yield return Path;
        yield return CloseParen;
    }

}
