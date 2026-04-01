namespace GUML.Shared.Syntax.Nodes.Expressions;

/// <summary>
/// A template string expression: <c>$"text {expr} text"</c>.
/// <see cref="OpenToken"/> is the <c>$"</c> prefix, <see cref="Parts"/> contains alternating
/// <see cref="TemplateStringTextSyntax"/> and <see cref="TemplateStringInterpolationSyntax"/> nodes,
/// and <see cref="CloseQuoteToken"/> is the trailing <c>"</c>.
/// </summary>
public sealed class TemplateStringExpressionSyntax : ExpressionSyntax
{
    /// <summary>The <c>$"</c> opening token.</summary>
    public SyntaxToken OpenToken { get; }

    /// <summary>Alternating text and interpolation parts.</summary>
    public SyntaxList<SyntaxNode> Parts { get; }

    /// <summary>The closing <c>"</c> token.</summary>
    public SyntaxToken CloseQuoteToken { get; }

    public TemplateStringExpressionSyntax(SyntaxToken openToken, SyntaxList<SyntaxNode> parts,
        SyntaxToken closeQuoteToken)
        : base(SyntaxKind.TemplateStringExpression)
    {
        OpenToken = openToken;
        Parts = parts;
        CloseQuoteToken = closeQuoteToken;
    }

    public override int FullWidth
    {
        get
        {
            int w = OpenToken.FullWidth;
            foreach (var t in Parts)
                w += t.FullWidth;
            w += CloseQuoteToken.FullWidth;
            return w;
        }
    }

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return OpenToken;
        foreach (var t in Parts)
            yield return t;
        yield return CloseQuoteToken;
    }

}

/// <summary>
/// A plain text segment within a template string (e.g. <c>"Hello "</c>).
/// </summary>
public sealed class TemplateStringTextSyntax : SyntaxNode
{
    /// <summary>The text token for this segment.</summary>
    public SyntaxToken TextToken { get; }

    public TemplateStringTextSyntax(SyntaxToken textToken)
        : base(SyntaxKind.TemplateStringText)
    {
        TextToken = textToken;
    }

    public override int FullWidth => TextToken.FullWidth;

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return TextToken;
    }

}

/// <summary>
/// An interpolated segment within a template string: <c>{expression}</c>.
/// </summary>
public sealed class TemplateStringInterpolationSyntax : ExpressionSyntax
{
    public SyntaxToken OpenBrace { get; }
    public ExpressionSyntax Expression { get; }
    public SyntaxToken CloseBrace { get; }

    public TemplateStringInterpolationSyntax(SyntaxToken openBrace, ExpressionSyntax expression, SyntaxToken closeBrace)
        : base(SyntaxKind.TemplateStringInterpolation)
    {
        OpenBrace = openBrace;
        Expression = expression;
        CloseBrace = closeBrace;
    }

    public override int FullWidth =>
        OpenBrace.FullWidth + Expression.FullWidth + CloseBrace.FullWidth;

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return OpenBrace;
        yield return Expression;
        yield return CloseBrace;
    }

}
