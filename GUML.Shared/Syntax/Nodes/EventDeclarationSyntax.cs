namespace GUML.Shared.Syntax.Nodes;

/// <summary>
/// An event declaration: <c>[/// doc] event name</c> or <c>[/// doc] event name(Type argName, ...)</c>
/// </summary>
public sealed class EventDeclarationSyntax : SyntaxNode
{
    /// <summary>
    /// Optional documentation comment block preceding this event declaration.
    /// May contain <c>@param</c> tags for event argument documentation.
    /// </summary>
    public DocumentationCommentSyntax? DocumentationComment { get; }

    public SyntaxToken EventKeyword { get; }
    public SyntaxToken Name { get; }
    public SyntaxToken? OpenParen { get; }
    public SyntaxList<EventArgumentSyntax>? Arguments { get; }
    public SyntaxToken? CloseParen { get; }

    public EventDeclarationSyntax(
        DocumentationCommentSyntax? documentationComment,
        SyntaxToken eventKeyword,
        SyntaxToken name,
        SyntaxToken? openParen,
        SyntaxList<EventArgumentSyntax>? arguments,
        SyntaxToken? closeParen)
        : base(SyntaxKind.EventDeclaration)
    {
        DocumentationComment = documentationComment;
        EventKeyword = eventKeyword;
        Name = name;
        OpenParen = openParen;
        Arguments = arguments;
        CloseParen = closeParen;
    }

    public override int FullWidth
    {
        get
        {
            int w = (DocumentationComment?.FullWidth ?? 0) + EventKeyword.FullWidth + Name.FullWidth;
            if (OpenParen != null) w += OpenParen.FullWidth;
            if (Arguments != null)
            {
                foreach (var t in Arguments)
                    w += t.FullWidth;
            }

            if (CloseParen != null) w += CloseParen.FullWidth;
            return w;
        }
    }

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        if (DocumentationComment != null) yield return DocumentationComment;
        yield return EventKeyword;
        yield return Name;
        if (OpenParen != null) yield return OpenParen;
        if (Arguments != null)
        {
            foreach (var t in Arguments)
                yield return t;
        }

        if (CloseParen != null) yield return CloseParen;
    }

}

/// <summary>
/// An event argument: <c>Type</c> or <c>Type name</c>
/// </summary>
public sealed class EventArgumentSyntax : SyntaxNode
{
    public SyntaxToken TypeName { get; }
    public SyntaxToken? Name { get; }
    public SyntaxToken? Comma { get; }

    public EventArgumentSyntax(SyntaxToken typeName, SyntaxToken? name, SyntaxToken? comma)
        : base(SyntaxKind.EventArgument)
    {
        TypeName = typeName;
        Name = name;
        Comma = comma;
    }

    public override int FullWidth =>
        TypeName.FullWidth + (Name?.FullWidth ?? 0) + (Comma?.FullWidth ?? 0);

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return TypeName;
        if (Name != null) yield return Name;
        if (Comma != null) yield return Comma;
    }
}
