using GUML.Shared.Syntax.Nodes.Expressions;

namespace GUML.Shared.Syntax.Nodes;

/// <summary>
/// A dictionary entry: <c>key: value</c> used in dictionary literals.
/// </summary>
public sealed class DictionaryEntrySyntax : SyntaxNode
{
    public ExpressionSyntax Key { get; }
    public SyntaxToken Colon { get; }
    public ExpressionSyntax Value { get; }
    public SyntaxToken? Comma { get; }

    public DictionaryEntrySyntax(ExpressionSyntax key, SyntaxToken colon, ExpressionSyntax value, SyntaxToken? comma)
        : base(SyntaxKind.DictionaryEntry)
    {
        Key = key;
        Colon = colon;
        Value = value;
        Comma = comma;
    }

    public override int FullWidth =>
        Key.FullWidth + Colon.FullWidth + Value.FullWidth + (Comma?.FullWidth ?? 0);

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return Key;
        yield return Colon;
        yield return Value;
        if (Comma != null) yield return Comma;
    }

}
