using GUML.Shared.Syntax.Nodes.Expressions;

namespace GUML.Shared.Syntax.Nodes;

/// <summary>
/// An argument list: <c>(expr1, expr2, ...)</c> used in call expressions and struct constructors.
/// </summary>
public sealed class ArgumentListSyntax : SyntaxNode
{
    public SyntaxToken OpenParen { get; }
    public SeparatedSyntaxList<ExpressionSyntax> Arguments { get; }
    public SyntaxToken CloseParen { get; }

    public ArgumentListSyntax(SyntaxToken openParen, SeparatedSyntaxList<ExpressionSyntax> arguments,
        SyntaxToken closeParen)
        : base(SyntaxKind.ArgumentList)
    {
        OpenParen = openParen;
        Arguments = arguments;
        CloseParen = closeParen;
    }

    public override int FullWidth
    {
        get
        {
            int w = OpenParen.FullWidth;
            w += Arguments.FullWidth;
            w += CloseParen.FullWidth;
            return w;
        }
    }

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return OpenParen;
        foreach (var child in Arguments.ChildNodesAndTokens())
            yield return child;
        yield return CloseParen;
    }

}
