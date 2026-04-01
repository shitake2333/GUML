namespace GUML.Shared.Syntax.Nodes.Expressions;

/// <summary>
/// An array literal expression: <c>Type[elem1, elem2, ...]</c>
/// </summary>
public sealed class ArrayLiteralExpressionSyntax : ExpressionSyntax
{
    public SyntaxToken TypeName { get; }
    public SyntaxToken OpenBracket { get; }
    public SeparatedSyntaxList<ExpressionSyntax> Elements { get; }
    public SyntaxToken CloseBracket { get; }

    public ArrayLiteralExpressionSyntax(
        SyntaxToken typeName,
        SyntaxToken openBracket,
        SeparatedSyntaxList<ExpressionSyntax> elements,
        SyntaxToken closeBracket)
        : base(SyntaxKind.ArrayLiteralExpression)
    {
        TypeName = typeName;
        OpenBracket = openBracket;
        Elements = elements;
        CloseBracket = closeBracket;
    }

    public override int FullWidth
    {
        get
        {
            int w = TypeName.FullWidth + OpenBracket.FullWidth;
            w += Elements.FullWidth;
            w += CloseBracket.FullWidth;
            return w;
        }
    }

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return TypeName;
        yield return OpenBracket;
        foreach (var child in Elements.ChildNodesAndTokens())
            yield return child;
        yield return CloseBracket;
    }

}
