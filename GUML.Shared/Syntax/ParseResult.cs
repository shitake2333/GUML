using GUML.Shared.Syntax.Nodes;

namespace GUML.Shared.Syntax;

/// <summary>
/// Result of parsing a GUML document.
/// Always contains a root <see cref="GumlDocumentSyntax"/> — malformed input
/// produces a partial tree with diagnostics.
/// </summary>
public sealed class ParseResult
{
    /// <summary>
    /// The root syntax node of the parsed document.
    /// </summary>
    public GumlDocumentSyntax Root { get; }

    /// <summary>
    /// All diagnostics (errors, warnings) produced during parsing.
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    /// <summary>
    /// The original source text that was parsed.
    /// </summary>
    public string SourceText { get; }

    /// <summary>
    /// Token list produced during lexing, exposed as a truly read-only view.
    /// </summary>
    internal IReadOnlyList<SyntaxToken> Tokens { get; }

    public ParseResult(GumlDocumentSyntax root, IReadOnlyList<Diagnostic> diagnostics, string sourceText,
        IReadOnlyList<SyntaxToken>? tokens = null)
    {
        Root = root;
        Diagnostics = diagnostics;
        SourceText = sourceText;
        Tokens = tokens is List<SyntaxToken> list
            ? list.AsReadOnly()
            : (tokens ?? Array.Empty<SyntaxToken>());
    }
}
