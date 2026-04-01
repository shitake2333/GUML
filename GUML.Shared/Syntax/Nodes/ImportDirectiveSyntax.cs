namespace GUML.Shared.Syntax.Nodes;

/// <summary>
/// An import directive: <c>import "path" [as Alias]</c>
/// </summary>
public sealed class ImportDirectiveSyntax : SyntaxNode
{
    public SyntaxToken ImportKeyword { get; }
    public SyntaxToken Path { get; }
    public ImportAliasSyntax? Alias { get; }

    public ImportDirectiveSyntax(SyntaxToken importKeyword, SyntaxToken path, ImportAliasSyntax? alias)
        : base(SyntaxKind.ImportDirective)
    {
        ImportKeyword = importKeyword;
        Path = path;
        Alias = alias;
    }

    public override int FullWidth =>
        ImportKeyword.FullWidth + Path.FullWidth + (Alias?.FullWidth ?? 0);

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return ImportKeyword;
        yield return Path;
        if (Alias != null)
            yield return Alias;
    }

}

/// <summary>
/// The alias portion of an import: <c>as Name</c>
/// </summary>
public sealed class ImportAliasSyntax : SyntaxNode
{
    public SyntaxToken AsKeyword { get; }
    public SyntaxToken Name { get; }

    public ImportAliasSyntax(SyntaxToken asKeyword, SyntaxToken name)
        : base(SyntaxKind.ImportAlias)
    {
        AsKeyword = asKeyword;
        Name = name;
    }

    public override int FullWidth => AsKeyword.FullWidth + Name.FullWidth;

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return AsKeyword;
        yield return Name;
    }
}
