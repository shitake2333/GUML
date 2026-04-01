namespace GUML.Shared.Syntax;

/// <summary>
/// Discriminated wrapper that holds either a <see cref="SyntaxNode"/> or a <see cref="SyntaxToken"/>.
/// Used when iterating children of a syntax node in document order.
/// </summary>
public readonly struct SyntaxNodeOrToken
{
    private readonly SyntaxNode? _node;
    private readonly SyntaxToken? _token;

    public SyntaxNodeOrToken(SyntaxNode node) { _node = node; _token = null; }
    public SyntaxNodeOrToken(SyntaxToken token) { _node = null; _token = token; }

    /// <summary>
    /// Whether this is a node.
    /// </summary>
    public bool IsNode => _node != null;

    /// <summary>
    /// Whether this is a token.
    /// </summary>
    public bool IsToken => _token != null;

    /// <summary>
    /// Get as a node. Throws if this is a token.
    /// </summary>
    public SyntaxNode AsNode() => _node ?? throw new InvalidOperationException("Not a node.");

    /// <summary>
    /// Get as a token. Throws if this is a node.
    /// </summary>
    public SyntaxToken AsToken() => _token ?? throw new InvalidOperationException("Not a token.");

    public TextSpan FullSpan => IsNode ? _node!.FullSpan : _token!.FullSpan;
    public TextSpan Span => IsNode ? _node!.Span : _token!.Span;

    public static implicit operator SyntaxNodeOrToken(SyntaxNode node) => new SyntaxNodeOrToken(node);
    public static implicit operator SyntaxNodeOrToken(SyntaxToken token) => new SyntaxNodeOrToken(token);
}
