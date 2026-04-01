namespace GUML.Shared.Syntax.Nodes.Expressions;

/// <summary>
/// An untyped object literal expression: <c>{ key1: val1, key2: val2 }</c>.
/// Used as a named initializer in struct expressions and as each-block parameters.
/// </summary>
public sealed class ObjectLiteralExpressionSyntax : ExpressionSyntax
{
    public SyntaxToken OpenBrace { get; }
    public SyntaxList<PropertyAssignmentSyntax> Properties { get; }
    public SyntaxToken CloseBrace { get; }

    public ObjectLiteralExpressionSyntax(
        SyntaxToken openBrace,
        SyntaxList<PropertyAssignmentSyntax> properties,
        SyntaxToken closeBrace)
        : base(SyntaxKind.ObjectLiteralExpression)
    {
        OpenBrace = openBrace;
        Properties = properties;
        CloseBrace = closeBrace;
    }

    public override int FullWidth
    {
        get
        {
            int w = OpenBrace.FullWidth;
            foreach (var t in Properties)
                w += t.FullWidth;

            w += CloseBrace.FullWidth;
            return w;
        }
    }

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return OpenBrace;
        foreach (var t in Properties)
            yield return t;

        yield return CloseBrace;
    }

}
