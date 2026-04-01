namespace GUML.Shared.Syntax.Nodes.Expressions;

/// <summary>
/// A typed dictionary literal: <c>Dictionary[KeyType, ValueType]{ key1: val1, key2: val2 }</c>
/// </summary>
public sealed class DictionaryLiteralExpressionSyntax : ExpressionSyntax
{
    public SyntaxToken TypeName { get; }
    public SyntaxToken OpenBracket { get; }
    public SyntaxToken KeyType { get; }
    public SyntaxToken TypeComma { get; }
    public SyntaxToken ValueType { get; }
    public SyntaxToken CloseBracket { get; }
    public SyntaxToken OpenBrace { get; }
    public SyntaxList<DictionaryEntrySyntax> Entries { get; }
    public SyntaxToken CloseBrace { get; }

    public DictionaryLiteralExpressionSyntax(
        SyntaxToken typeName,
        SyntaxToken openBracket,
        SyntaxToken keyType,
        SyntaxToken typeComma,
        SyntaxToken valueType,
        SyntaxToken closeBracket,
        SyntaxToken openBrace,
        SyntaxList<DictionaryEntrySyntax> entries,
        SyntaxToken closeBrace)
        : base(SyntaxKind.DictionaryLiteralExpression)
    {
        TypeName = typeName;
        OpenBracket = openBracket;
        KeyType = keyType;
        TypeComma = typeComma;
        ValueType = valueType;
        CloseBracket = closeBracket;
        OpenBrace = openBrace;
        Entries = entries;
        CloseBrace = closeBrace;
    }

    public override int FullWidth
    {
        get
        {
            int w = TypeName.FullWidth + OpenBracket.FullWidth + KeyType.FullWidth
                    + TypeComma.FullWidth + ValueType.FullWidth + CloseBracket.FullWidth
                    + OpenBrace.FullWidth;
            foreach (var t in Entries)
                w += t.FullWidth;

            w += CloseBrace.FullWidth;
            return w;
        }
    }

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return TypeName;
        yield return OpenBracket;
        yield return KeyType;
        yield return TypeComma;
        yield return ValueType;
        yield return CloseBracket;
        yield return OpenBrace;
        foreach (var t in Entries)
            yield return t;

        yield return CloseBrace;
    }

}
