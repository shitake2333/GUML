namespace GUML.Shared.Syntax.Nodes;

/// <summary>
/// Root node of a GUML document: import directives followed by a root component.
/// </summary>
public sealed class GumlDocumentSyntax : SyntaxNode
{
    public SyntaxList<ImportDirectiveSyntax> Imports { get; }
    public ComponentDeclarationSyntax RootComponent { get; }
    public SyntaxToken EndOfFile { get; }

    public GumlDocumentSyntax(
        SyntaxList<ImportDirectiveSyntax> imports,
        ComponentDeclarationSyntax rootComponent,
        SyntaxToken endOfFile)
        : base(SyntaxKind.GumlDocument)
    {
        Imports = imports;
        RootComponent = rootComponent;
        EndOfFile = endOfFile;
    }

    public override int FullWidth
    {
        get
        {
            int w = 0;
            foreach (var t in Imports)
            {
                w += t.FullWidth;
            }

            w += RootComponent.FullWidth;
            w += EndOfFile.FullWidth;
            return w;
        }
    }

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        foreach (var t in Imports)
        {
            yield return t;
        }

        yield return RootComponent;
        yield return EndOfFile;
    }

}
