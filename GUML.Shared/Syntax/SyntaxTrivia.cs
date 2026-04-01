namespace GUML.Shared.Syntax;

/// <summary>
/// A piece of trivia (whitespace, comment, skipped tokens) attached to a token.
/// Trivia is not semantically meaningful but preserves full source fidelity.
/// </summary>
public sealed class SyntaxTrivia
{
    /// <summary>
    /// The kind of trivia.
    /// </summary>
    public SyntaxKind Kind { get; }

    /// <summary>
    /// The raw text of this trivia.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// The width (character count) of this trivia.
    /// </summary>
    public int FullWidth => Text.Length;

    public SyntaxTrivia(SyntaxKind kind, string text)
    {
        Kind = kind;
        Text = text;
    }

    public override string ToString() => Text;
}

/// <summary>
/// An immutable list of <see cref="SyntaxTrivia"/> items attached to a token.
/// </summary>
public sealed class SyntaxTriviaList
{
    /// <summary>
    /// An empty trivia list singleton.
    /// </summary>
    public static readonly SyntaxTriviaList Empty = new SyntaxTriviaList(Array.Empty<SyntaxTrivia>());

    private readonly SyntaxTrivia[] _items;

    public SyntaxTriviaList(SyntaxTrivia[] items)
    {
        _items = items;
    }

    /// <summary>
    /// The number of trivia items.
    /// </summary>
    public int Count => _items.Length;

    /// <summary>
    /// Get a trivia item by index.
    /// </summary>
    public SyntaxTrivia this[int index] => _items[index];

    /// <summary>
    /// The total character width of all trivia in this list.
    /// </summary>
    public int FullWidth
    {
        get
        {
            int w = 0;
            foreach (var t in _items)
                w += t.FullWidth;

            return w;
        }
    }

    public IEnumerator<SyntaxTrivia> GetEnumerator()
    {
        foreach (var t in _items)
            yield return t;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var t in _items)
            sb.Append(t.Text);

        return sb.ToString();
    }
}
