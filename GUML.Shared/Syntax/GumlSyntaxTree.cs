using GUML.Shared.Syntax.Internal;

namespace GUML.Shared.Syntax;

/// <summary>
/// Entry point for parsing GUML source text into a full-fidelity syntax tree.
/// </summary>
public static class GumlSyntaxTree
{
    /// <summary>
    /// Parse GUML source text into a full-fidelity syntax tree.
    /// Always succeeds — malformed input produces a partial tree with diagnostics.
    /// </summary>
    /// <param name="text">The GUML source text to parse.</param>
    /// <returns>A <see cref="ParseResult"/> containing the root node and diagnostics.</returns>
    public static ParseResult Parse(string text)
    {
        var lexer = new Lexer(text);
        var tokens = lexer.LexAll();
        var parser = new Parser(tokens);
        var root = parser.ParseDocument();
        root.ComputePositions(null, 0);
        var diagnostics = new List<Diagnostic>(lexer.Diagnostics.Count + parser.Diagnostics.Count);
        diagnostics.AddRange(lexer.Diagnostics);
        diagnostics.AddRange(parser.Diagnostics);
        return new ParseResult(root, diagnostics, text, tokens);
    }
}
