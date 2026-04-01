namespace GUML.Shared.Syntax.Nodes;

/// <summary>
/// A component declaration: <c>[@alias:] [/// doc] ComponentName { members }</c>
/// </summary>
public sealed class ComponentDeclarationSyntax : SyntaxNode
{
    public DocumentationCommentSyntax? DocumentationComment { get; }
    public AliasPrefixSyntax? AliasPrefix { get; }
    public SyntaxToken TypeName { get; }
    public SyntaxToken OpenBrace { get; }
    public SyntaxList<SyntaxNode> Members { get; }
    public SyntaxToken CloseBrace { get; }

    public ComponentDeclarationSyntax(
        DocumentationCommentSyntax? documentationComment,
        AliasPrefixSyntax? aliasPrefix,
        SyntaxToken typeName,
        SyntaxToken openBrace,
        SyntaxList<SyntaxNode> members,
        SyntaxToken closeBrace)
        : base(SyntaxKind.ComponentDeclaration)
    {
        DocumentationComment = documentationComment;
        AliasPrefix = aliasPrefix;
        TypeName = typeName;
        OpenBrace = openBrace;
        Members = members;
        CloseBrace = closeBrace;
    }

    public override int FullWidth
    {
        get
        {
            int w = 0;
            if (DocumentationComment != null) w += DocumentationComment.FullWidth;
            if (AliasPrefix != null) w += AliasPrefix.FullWidth;
            w += TypeName.FullWidth;
            w += OpenBrace.FullWidth;
            foreach (var t in Members)
                w += t.FullWidth;
            w += CloseBrace.FullWidth;
            return w;
        }
    }

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        if (DocumentationComment != null) yield return DocumentationComment;
        if (AliasPrefix != null) yield return AliasPrefix;
        yield return TypeName;
        yield return OpenBrace;
        foreach (var t in Members)
            yield return t;
        yield return CloseBrace;
    }

}

/// <summary>
/// An alias prefix: <c>@alias_name:</c>
/// </summary>
public sealed class AliasPrefixSyntax : SyntaxNode
{
    public SyntaxToken AliasRef { get; }
    public SyntaxToken Colon { get; }

    public AliasPrefixSyntax(SyntaxToken aliasRef, SyntaxToken colon)
        : base(SyntaxKind.AliasPrefix)
    {
        AliasRef = aliasRef;
        Colon = colon;
    }

    public override int FullWidth => AliasRef.FullWidth + Colon.FullWidth;

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return AliasRef;
        yield return Colon;
    }
}

/// <summary>
/// A documentation comment block consisting of one or more <c>///</c> lines.
/// Supports structured tags: <c>@name</c> (node naming marker) and <c>@param</c> (event argument docs).
/// </summary>
public sealed class DocumentationCommentSyntax : SyntaxNode
{
    /// <summary>
    /// All individual <c>///</c> comment tokens that form this documentation block.
    /// </summary>
    public IReadOnlyList<SyntaxToken> CommentTokens { get; }

    /// <summary>
    /// Returns the first comment token (for backward compatibility).
    /// </summary>
    public SyntaxToken CommentToken => CommentTokens[0];

    public DocumentationCommentSyntax(IReadOnlyList<SyntaxToken> commentTokens)
        : base(SyntaxKind.DocumentationComment)
    {
        CommentTokens = commentTokens;
    }

    public DocumentationCommentSyntax(SyntaxToken commentToken)
        : this([commentToken])
    {
    }

    public override int FullWidth
    {
        get
        {
            int w = 0;
            foreach (var t in CommentTokens) w += t.FullWidth;
            return w;
        }
    }

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        foreach (var t in CommentTokens) yield return t;
    }

    /// <summary>
    /// Extract the combined documentation text, stripping the <c>///</c> prefix from each line.
    /// Lines containing <c>@tag</c> are excluded from the description text.
    /// </summary>
    public string GetDocumentationText()
    {
        var lines = new List<string>();
        foreach (var token in CommentTokens)
        {
            string line = StripPrefix(token.Text);
            if (!IsTagLine(line))
                lines.Add(line);
        }

        // Remove trailing empty lines
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
            lines.RemoveAt(lines.Count - 1);

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Extract the naming marker from a <c>@name identifier</c> tag, if present.
    /// Returns <c>null</c> when no <c>@name</c> tag exists.
    /// </summary>
    public string? GetNameMarker()
    {
        foreach (var token in CommentTokens)
        {
            string line = StripPrefix(token.Text);
            if (TryParseTag(line, "name", out string value) && !string.IsNullOrWhiteSpace(value))
            {
                // @name returns the first word (the identifier)
                string trimmed = value.Trim();
                int spaceIdx = trimmed.IndexOf(' ');
                return spaceIdx >= 0 ? trimmed.Substring(0, spaceIdx) : trimmed;
            }
        }

        return null;
    }

    /// <summary>
    /// Extract parameter documentation from <c>@param name desc</c> tags.
    /// Returns a list of (paramName, description) pairs.
    /// </summary>
    public IReadOnlyList<(string Name, string Description)> GetParamDocs()
    {
        var result = new List<(string, string)>();
        foreach (var token in CommentTokens)
        {
            string line = StripPrefix(token.Text);
            if (TryParseTag(line, "param", out string value) && !string.IsNullOrWhiteSpace(value))
            {
                string trimmed = value.Trim();
                int spaceIdx = trimmed.IndexOf(' ');
                result.Add(spaceIdx >= 0
                    ? (trimmed.Substring(0, spaceIdx), trimmed.Substring(spaceIdx + 1).Trim())
                    : (trimmed, ""));
            }
        }

        return result;
    }

    private static string StripPrefix(string text)
    {
        // Strip "///" prefix and at most one leading space
        if (text.StartsWith("///"))
        {
            string rest = text.Substring(3);
            if (rest.StartsWith(" "))
                return rest.Substring(1);
            return rest;
        }

        return text;
    }

    private static bool IsTagLine(string strippedLine)
    {
        string trimmed = strippedLine.TrimStart();
        return trimmed.StartsWith("@");
    }

    private static bool TryParseTag(string strippedLine, string tagName, out string value)
    {
        string trimmed = strippedLine.TrimStart();
        string prefix = "@" + tagName;
        if (trimmed.StartsWith(prefix) &&
            (trimmed.Length == prefix.Length || trimmed[prefix.Length] == ' '))
        {
            value = trimmed.Length > prefix.Length ? trimmed.Substring(prefix.Length + 1) : "";
            return true;
        }

        value = "";
        return false;
    }
}
