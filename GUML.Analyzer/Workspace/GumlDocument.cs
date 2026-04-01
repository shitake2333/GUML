using GUML.Shared.Syntax;
using GUML.Shared.Syntax.Nodes;

namespace GUML.Analyzer.Workspace;

/// <summary>
/// Tracks the state of a single open .guml document:
/// source text, syntax tree, version, and cached position mapper.
/// </summary>
public sealed class GumlDocument
{
    /// <summary>The document URI as sent by the client.</summary>
    public string Uri { get; }

    /// <summary>The full source text of the document.</summary>
    public string Text { get; }

    /// <summary>The document version (incremented on each change).</summary>
    public int Version { get; }

    /// <summary>The parse result (syntax tree + diagnostics).</summary>
    public ParseResult ParseResult { get; }

    /// <summary>Syntax diagnostics from the parser.</summary>
    public IReadOnlyList<Diagnostic> SyntaxDiagnostics => ParseResult.Diagnostics;

    /// <summary>The root syntax node of the document.</summary>
    public GumlDocumentSyntax Root => ParseResult.Root;

    /// <summary>
    /// Creates a new document from full text (initial parse or update).
    /// </summary>
    public GumlDocument(string uri, string text, int version = 1)
    {
        Uri = uri;
        Text = text;
        Version = version;
        ParseResult = GumlSyntaxTree.Parse(text);
    }
}
