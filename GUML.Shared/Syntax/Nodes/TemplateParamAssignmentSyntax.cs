namespace GUML.Shared.Syntax.Nodes;

/// <summary>
/// A template parameter assignment: <c>name => Component { ... }</c>
/// </summary>
public sealed class TemplateParamAssignmentSyntax : SyntaxNode
{
    public SyntaxToken Name { get; }
    public SyntaxToken FatArrow { get; }
    public ComponentDeclarationSyntax Component { get; }
    public SyntaxToken? Comma { get; }

    public TemplateParamAssignmentSyntax(SyntaxToken name, SyntaxToken fatArrow, ComponentDeclarationSyntax component,
        SyntaxToken? comma)
        : base(SyntaxKind.TemplateParamAssignment)
    {
        Name = name;
        FatArrow = fatArrow;
        Component = component;
        Comma = comma;
    }

    public override int FullWidth =>
        Name.FullWidth + FatArrow.FullWidth + Component.FullWidth + (Comma?.FullWidth ?? 0);

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return Name;
        yield return FatArrow;
        yield return Component;
        if (Comma != null) yield return Comma;
    }

}
