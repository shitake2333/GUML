using GUML.Analyzer.Utils;
using GUML.Analyzer.Workspace;
using GUML.Shared.Syntax;
using GUML.Shared.Syntax.Nodes;

namespace GUML.Analyzer.Handlers;

/// <summary>
/// Provides document highlight: finds all occurrences of the symbol under the cursor.
/// </summary>
public static class DocumentHighlightHandler
{
    /// <summary>
    /// Returns highlights for all occurrences of the token at the given position.
    /// </summary>
    public static List<DocumentHighlight> GetHighlights(GumlDocument document, LspPosition position)
    {
        var results = new List<DocumentHighlight>();
        var mapper = new PositionMapper(document.Text);
        int offset = mapper.GetOffset(position);

        var token = document.Root.FindToken(offset);
        if (token == null || token.IsMissing) return results;

        string target = token.Text;
        if (string.IsNullOrEmpty(target)) return results;

        var targetKinds = GetMatchingKinds(token);

        foreach (var t in document.Root.DescendantTokens())
        {
            if (t.IsMissing || t.Width == 0) continue;
            if (t.Text != target) continue;
            if (!targetKinds.Contains(t.Kind)) continue;

            var kind = DetermineHighlightKind(t);
            results.Add(new DocumentHighlight { Range = mapper.GetRange(t.Span), Kind = kind });
        }

        return results;
    }

    /// <summary>
    /// Gets the set of SyntaxKinds that should match for highlighting.
    /// For identifiers, we match both IdentifierToken and GlobalRefToken depending on context.
    /// </summary>
    private static HashSet<SyntaxKind> GetMatchingKinds(SyntaxToken token)
    {
        var kinds = new HashSet<SyntaxKind> { token.Kind };

        // Param declarations and references both use IdentifierToken,
        // so matching on Kind + Text is sufficient — no extra kinds needed.

        return kinds;
    }

    private static DocumentHighlightKind DetermineHighlightKind(SyntaxToken token)
    {
        // Write positions: property/mapping left-hand side, param declaration name
        if (token.Parent is PropertyAssignmentSyntax prop && token == prop.Name)
            return DocumentHighlightKind.Write;
        if (token.Parent is MappingAssignmentSyntax mapping && token == mapping.Name)
            return DocumentHighlightKind.Write;
        if (token.Parent is ParameterDeclarationSyntax paramDecl && token == paramDecl.Name)
            return DocumentHighlightKind.Write;
        if (token.Parent is EventDeclarationSyntax eventDecl && token == eventDecl.Name)
            return DocumentHighlightKind.Write;

        return DocumentHighlightKind.Read;
    }
}
