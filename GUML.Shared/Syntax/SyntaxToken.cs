namespace GUML.Shared.Syntax;

/// <summary>
/// A terminal node in the syntax tree representing a single lexical token.
/// Tokens are the leaves of the tree and carry trivia (whitespace, comments).
/// </summary>
public sealed class SyntaxToken
{
    /// <summary>
    /// The kind of this token.
    /// </summary>
    public SyntaxKind Kind { get; }

    /// <summary>
    /// The raw text of this token (excluding trivia).
    /// Empty string for missing tokens.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Whether this token was fabricated by the parser to recover from an error.
    /// </summary>
    public bool IsMissing { get; }

    /// <summary>
    /// Trivia appearing before this token (whitespace, comments, etc.).
    /// </summary>
    public SyntaxTriviaList LeadingTrivia { get; }

    /// <summary>
    /// Trivia appearing after this token.
    /// </summary>
    public SyntaxTriviaList TrailingTrivia { get; }

    /// <summary>
    /// The width of the token text only.
    /// </summary>
    public int Width => Text.Length;

    /// <summary>
    /// The total width including leading and trailing trivia.
    /// </summary>
    public int FullWidth => LeadingTrivia.FullWidth + Width + TrailingTrivia.FullWidth;

    // Red-tree fields set when this token is placed in a red tree.
    internal SyntaxNode? _parentNode;
    internal int _position;

    /// <summary>
    /// The parent syntax node in the red tree, if any.
    /// </summary>
    public SyntaxNode? Parent => _parentNode;

    /// <summary>
    /// The absolute span of this token in the source (excluding trivia).
    /// </summary>
    public TextSpan Span => new TextSpan(_position + LeadingTrivia.FullWidth, Width);

    /// <summary>
    /// The absolute span of this token including trivia.
    /// </summary>
    public TextSpan FullSpan => new TextSpan(_position, FullWidth);

    public SyntaxToken(SyntaxKind kind, string text, SyntaxTriviaList leadingTrivia, SyntaxTriviaList trailingTrivia,
        bool isMissing = false)
    {
        Kind = kind;
        Text = text;
        LeadingTrivia = leadingTrivia;
        TrailingTrivia = trailingTrivia;
        IsMissing = isMissing;
    }

    /// <summary>
    /// Create a missing token (fabricated by the parser for error recovery).
    /// </summary>
    public static SyntaxToken Missing(SyntaxKind kind)
    {
        return new SyntaxToken(kind, "", SyntaxTriviaList.Empty, SyntaxTriviaList.Empty, isMissing: true);
    }

    /// <summary>
    /// Reconstruct the full text including trivia.
    /// </summary>
    public string ToFullString()
    {
        var sb = new StringBuilder(FullWidth);
        sb.Append(LeadingTrivia);
        sb.Append(Text);
        sb.Append(TrailingTrivia);
        return sb.ToString();
    }

    public override string ToString() => Text;
}
