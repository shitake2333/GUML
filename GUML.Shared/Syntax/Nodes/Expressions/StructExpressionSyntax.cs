namespace GUML.Shared.Syntax.Nodes.Expressions;

/// <summary>
/// A struct constructor expression: <c>Type(arg1, arg2)</c> or <c>Type({ key: value })</c>.
/// Either PositionalArgs or NamedArgs is used, not both.
/// </summary>
public sealed class StructExpressionSyntax : ExpressionSyntax
{
    public SyntaxToken TypeName { get; }
    public SyntaxToken OpenParen { get; }

    /// <summary>
    /// Positional arguments, if non-null. Mutually exclusive with <see cref="NamedArgs"/>.
    /// </summary>
    public SeparatedSyntaxList<ExpressionSyntax>? PositionalArgs { get; }

    /// <summary>
    /// Named arguments (object literal inside parens), if non-null. Mutually exclusive with <see cref="PositionalArgs"/>.
    /// </summary>
    public ObjectLiteralExpressionSyntax? NamedArgs { get; }

    public SyntaxToken CloseParen { get; }

    public StructExpressionSyntax(
        SyntaxToken typeName,
        SyntaxToken openParen,
        SeparatedSyntaxList<ExpressionSyntax>? positionalArgs,
        ObjectLiteralExpressionSyntax? namedArgs,
        SyntaxToken closeParen)
        : base(SyntaxKind.StructExpression)
    {
        TypeName = typeName;
        OpenParen = openParen;
        PositionalArgs = positionalArgs;
        NamedArgs = namedArgs;
        CloseParen = closeParen;
    }

    public override int FullWidth
    {
        get
        {
            int w = TypeName.FullWidth + OpenParen.FullWidth;
            if (PositionalArgs != null)
                w += PositionalArgs.FullWidth;
            if (NamedArgs != null)
                w += NamedArgs.FullWidth;
            w += CloseParen.FullWidth;
            return w;
        }
    }

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return TypeName;
        yield return OpenParen;
        if (PositionalArgs != null)
        {
            foreach (var child in PositionalArgs.ChildNodesAndTokens())
                yield return child;
        }

        if (NamedArgs != null)
            yield return NamedArgs;
        yield return CloseParen;
    }

}
