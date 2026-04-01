namespace GUML.Shared.Syntax.Nodes.Expressions;

/// <summary>
/// An object creation expression: <c>new TypeName { prop1: val1, prop2: val2 }</c>
/// </summary>
public sealed class ObjectCreationExpressionSyntax : ExpressionSyntax
{
    public SyntaxToken NewKeyword { get; }
    public SyntaxToken TypeName { get; }
    public SyntaxToken OpenBrace { get; }
    public SyntaxList<PropertyAssignmentSyntax> Properties { get; }
    public SyntaxToken CloseBrace { get; }

    public ObjectCreationExpressionSyntax(
        SyntaxToken newKeyword,
        SyntaxToken typeName,
        SyntaxToken openBrace,
        SyntaxList<PropertyAssignmentSyntax> properties,
        SyntaxToken closeBrace)
        : base(SyntaxKind.ObjectCreationExpression)
    {
        NewKeyword = newKeyword;
        TypeName = typeName;
        OpenBrace = openBrace;
        Properties = properties;
        CloseBrace = closeBrace;
    }

    public override int FullWidth
    {
        get
        {
            int w = NewKeyword.FullWidth + TypeName.FullWidth + OpenBrace.FullWidth;
            foreach (var t in Properties)
            {
                w += t.FullWidth;
            }

            w += CloseBrace.FullWidth;
            return w;
        }
    }

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return NewKeyword;
        yield return TypeName;
        yield return OpenBrace;
        foreach (var t in Properties)
        {
            yield return t;
        }

        yield return CloseBrace;
    }

}
