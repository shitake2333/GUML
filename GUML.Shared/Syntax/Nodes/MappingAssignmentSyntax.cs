using GUML.Shared.Syntax.Nodes.Expressions;

namespace GUML.Shared.Syntax.Nodes;

/// <summary>
/// A mapping assignment: <c>name := expr</c>, <c>name =: expr</c>, or <c>name &lt;=&gt; expr</c>
/// </summary>
public sealed class MappingAssignmentSyntax : SyntaxNode
{
    public SyntaxToken Name { get; }
    public SyntaxToken Operator { get; }
    public ExpressionSyntax Value { get; }
    public SyntaxToken? Comma { get; }

    public MappingAssignmentSyntax(SyntaxToken name, SyntaxToken op, ExpressionSyntax value, SyntaxToken? comma)
        : base(SyntaxKind.MappingAssignment)
    {
        Name = name;
        Operator = op;
        Value = value;
        Comma = comma;
    }

    public override int FullWidth =>
        Name.FullWidth + Operator.FullWidth + Value.FullWidth + (Comma?.FullWidth ?? 0);

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return Name;
        yield return Operator;
        yield return Value;
        if (Comma != null) yield return Comma;
    }

}
