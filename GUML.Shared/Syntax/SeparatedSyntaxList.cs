namespace GUML.Shared.Syntax;

/// <summary>
/// An immutable list of syntax nodes separated by <see cref="SyntaxToken"/> separators
/// (e.g. comma-separated argument lists).
/// <para>
/// Enumerating this list yields only the <typeparamref name="T"/> nodes;
/// separators are accessible via <see cref="GetSeparator"/>.
/// </para>
/// </summary>
/// <typeparam name="T">The syntax node type of each element.</typeparam>
public sealed class SeparatedSyntaxList<T> where T : SyntaxNode
{
    /// <summary>
    /// An empty separated list singleton.
    /// </summary>
    public static readonly SeparatedSyntaxList<T> Empty =
        new(Array.Empty<T>(), Array.Empty<SyntaxToken>());

    private readonly T[] _nodes;
    private readonly SyntaxToken[] _separators;

    /// <summary>
    /// Create a separated list from the given nodes and separators.
    /// </summary>
    /// <param name="nodes">Element nodes.</param>
    /// <param name="separators">
    /// Separator tokens between consecutive nodes.
    /// Must have <c>nodes.Count - 1</c> items (or 0 when <paramref name="nodes"/> is empty).
    /// </param>
    public SeparatedSyntaxList(IReadOnlyList<T> nodes, IReadOnlyList<SyntaxToken> separators)
    {
        _nodes = new T[nodes.Count];
        for (int i = 0; i < nodes.Count; i++)
            _nodes[i] = nodes[i];

        _separators = new SyntaxToken[separators.Count];
        for (int i = 0; i < separators.Count; i++)
            _separators[i] = separators[i];
    }

    /// <summary>The number of element nodes.</summary>
    public int Count => _nodes.Length;

    /// <summary>The number of separator tokens.</summary>
    public int SeparatorCount => _separators.Length;

    /// <summary>Get an element node by index.</summary>
    public T this[int index] => _nodes[index];

    /// <summary>Get a separator token by index (0 = separator between nodes 0 and 1).</summary>
    public SyntaxToken GetSeparator(int index) => _separators[index];

    /// <summary>
    /// Total width of all nodes and separators (including trivia).
    /// </summary>
    public int FullWidth
    {
        get
        {
            int w = 0;
            for (int i = 0; i < _nodes.Length; i++)
            {
                w += _nodes[i].FullWidth;
                if (i < _separators.Length)
                    w += _separators[i].FullWidth;
            }

            return w;
        }
    }

    /// <summary>
    /// Enumerate all children (nodes and separators interleaved) as
    /// <see cref="SyntaxNodeOrToken"/>.
    /// </summary>
    public IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        for (int i = 0; i < _nodes.Length; i++)
        {
            yield return _nodes[i];
            if (i < _separators.Length)
                yield return _separators[i];
        }
    }

    /// <summary>
    /// Enumerate only the element nodes (skips separators).
    /// This is the default enumerator.
    /// </summary>
    public IEnumerator<T> GetEnumerator()
    {
        foreach (var n in _nodes)
            yield return n;
    }
}
