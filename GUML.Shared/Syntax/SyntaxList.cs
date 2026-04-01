namespace GUML.Shared.Syntax;

/// <summary>
/// An immutable list of syntax nodes.
/// </summary>
/// <typeparam name="T">The syntax node type.</typeparam>
public sealed class SyntaxList<T> where T : SyntaxNode
{
    /// <summary>
    /// An empty list singleton.
    /// </summary>
    public static readonly SyntaxList<T> Empty = new SyntaxList<T>(Array.Empty<T>());

    private readonly T[] _items;

    public SyntaxList(T[] items)
    {
        _items = items;
    }

    public SyntaxList(IReadOnlyList<T> items)
    {
        _items = new T[items.Count];
        for (int i = 0; i < items.Count; i++)
            _items[i] = items[i];
    }

    /// <summary>
    /// The number of items.
    /// </summary>
    public int Count => _items.Length;

    /// <summary>
    /// Get an item by index.
    /// </summary>
    public T this[int index] => _items[index];

    public IEnumerator<T> GetEnumerator()
    {
        foreach (var t in _items)
            yield return t;
    }
}

/// <summary>
/// An immutable list of <see cref="SyntaxToken"/> items (e.g. comma-separated tokens).
/// </summary>
public sealed class SyntaxTokenList
{
    public static readonly SyntaxTokenList Empty = new SyntaxTokenList(Array.Empty<SyntaxToken>());

    private readonly SyntaxToken[] _items;

    public SyntaxTokenList(SyntaxToken[] items)
    {
        _items = items;
    }

    public int Count => _items.Length;
    public SyntaxToken this[int index] => _items[index];

    public IEnumerator<SyntaxToken> GetEnumerator()
    {
        foreach (var t in _items)
            yield return t;
    }
}
