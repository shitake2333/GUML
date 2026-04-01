namespace GUML.Shared.Syntax.Nodes;

/// <summary>
/// Represents skipped tokens collected during error recovery.
/// These tokens were unexpected and not part of any valid production.
/// </summary>
public sealed class SkippedTokensSyntax : SyntaxNode
{
    public SyntaxTokenList Tokens { get; }

    public SkippedTokensSyntax(SyntaxTokenList tokens)
        : base(SyntaxKind.SkippedTokens)
    {
        Tokens = tokens;
    }

    public override int FullWidth
    {
        get
        {
            int w = 0;
            for (int i = 0; i < Tokens.Count; i++)
                w += Tokens[i].FullWidth;
            return w;
        }
    }

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        for (int i = 0; i < Tokens.Count; i++)
            yield return Tokens[i];
    }
}
