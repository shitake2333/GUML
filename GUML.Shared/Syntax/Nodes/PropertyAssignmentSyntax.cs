using GUML.Shared.Syntax.Nodes.Expressions;

namespace GUML.Shared.Syntax.Nodes;

/// <summary>
/// A static property assignment: <c>name: expression</c>
/// </summary>
public sealed class PropertyAssignmentSyntax : SyntaxNode
{
    public SyntaxToken Name { get; }
    public SyntaxToken Colon { get; }
    public ExpressionSyntax Value { get; }
    public SyntaxToken? Comma { get; }

    public PropertyAssignmentSyntax(SyntaxToken name, SyntaxToken colon, ExpressionSyntax value, SyntaxToken? comma)
        : base(SyntaxKind.PropertyAssignment)
    {
        Name = name;
        Colon = colon;
        Value = value;
        Comma = comma;
    }

    public override int FullWidth =>
        Name.FullWidth + Colon.FullWidth + Value.FullWidth + (Comma?.FullWidth ?? 0);

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return Name;
        yield return Colon;
        yield return Value;
        if (Comma != null) yield return Comma;
    }

}
