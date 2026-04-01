using GUML.Analyzer.Utils;
using GUML.Analyzer.Workspace;
using GUML.Shared.Syntax;
using GUML.Shared.Syntax.Nodes;
using GUML.Shared.Syntax.Nodes.Expressions;

namespace GUML.Analyzer.Handlers;

/// <summary>
/// Provides semantic token encoding for a GUML document.
/// Walks the syntax tree and produces delta-encoded token data per the LSP specification.
/// </summary>
public static class SemanticTokensHandler
{
    /// <summary>
    /// Returns semantic tokens for the entire document, or a specific range.
    /// </summary>
    public static SemanticTokensResult GetTokens(GumlDocument document, LspRange? range = null)
    {
        var mapper = new PositionMapper(document.Text);
        TextSpan? restrictSpan = range.HasValue ? mapper.GetSpan(range.Value) : null;

        var rawTokens = new List<RawSemanticToken>();
        CollectTokens(document.Root, mapper, rawTokens, restrictSpan);

        // Sort by position (line, then character)
        rawTokens.Sort((a, b) =>
        {
            int cmp = a.Line.CompareTo(b.Line);
            return cmp != 0 ? cmp : a.StartChar.CompareTo(b.StartChar);
        });

        // Delta-encode
        int[] data = new int[rawTokens.Count * 5];
        int prevLine = 0, prevChar = 0;
        for (int i = 0; i < rawTokens.Count; i++)
        {
            var t = rawTokens[i];
            int deltaLine = t.Line - prevLine;
            int deltaChar = deltaLine == 0 ? t.StartChar - prevChar : t.StartChar;

            data[i * 5] = deltaLine;
            data[i * 5 + 1] = deltaChar;
            data[i * 5 + 2] = t.Length;
            data[i * 5 + 3] = t.TokenType;
            data[i * 5 + 4] = t.TokenModifiers;

            prevLine = t.Line;
            prevChar = t.StartChar;
        }

        return new SemanticTokensResult { Data = data };
    }

    private static void CollectTokens(SyntaxNode node, PositionMapper mapper,
        List<RawSemanticToken> tokens, TextSpan? restrictSpan)
    {
        foreach (var token in node.DescendantTokens())
        {
            if (token.IsMissing) continue;

            // Collect comment trivia from leading trivia
            CollectCommentTrivia(token, mapper, tokens, restrictSpan);

            if (token.Width == 0) continue;

            var span = token.Span;
            if (restrictSpan.HasValue && !restrictSpan.Value.OverlapsWith(span))
                continue;

            int tokenType = MapTokenType(token);
            if (tokenType < 0) continue;

            int modifiers = GetModifiers(token);

            var pos = mapper.GetPosition(span.Start);
            tokens.Add(new RawSemanticToken(pos.Line, pos.Character, token.Width, tokenType, modifiers));
        }
    }

    private static void CollectCommentTrivia(SyntaxToken token, PositionMapper mapper,
        List<RawSemanticToken> tokens, TextSpan? restrictSpan)
    {
        // Leading trivia comments
        int offset = token.FullSpan.Start;
        foreach (var trivia in token.LeadingTrivia)
        {
            if (trivia.Kind is SyntaxKind.SingleLineCommentTrivia or SyntaxKind.DocumentationCommentTrivia)
            {
                var span = new TextSpan(offset, trivia.FullWidth);
                if (!restrictSpan.HasValue || restrictSpan.Value.OverlapsWith(span))
                {
                    var pos = mapper.GetPosition(offset);
                    tokens.Add(new RawSemanticToken(pos.Line, pos.Character, trivia.FullWidth,
                        SemanticTokenTypes.Comment, 0));
                }
            }

            offset += trivia.FullWidth;
        }

        // Trailing trivia comments
        offset = token.Span.Start + token.Width;
        foreach (var trivia in token.TrailingTrivia)
        {
            if (trivia.Kind is SyntaxKind.SingleLineCommentTrivia or SyntaxKind.DocumentationCommentTrivia)
            {
                var span = new TextSpan(offset, trivia.FullWidth);
                if (!restrictSpan.HasValue || restrictSpan.Value.OverlapsWith(span))
                {
                    var pos = mapper.GetPosition(offset);
                    tokens.Add(new RawSemanticToken(pos.Line, pos.Character, trivia.FullWidth,
                        SemanticTokenTypes.Comment, 0));
                }
            }

            offset += trivia.FullWidth;
        }
    }

    private static int MapTokenType(SyntaxToken token)
    {
        return token.Kind switch
        {
            // Keywords
            SyntaxKind.ImportKeyword => SemanticTokenTypes.Keyword,
            SyntaxKind.AsKeyword => SemanticTokenTypes.Keyword,
            SyntaxKind.ParamKeyword => SemanticTokenTypes.Keyword,
            SyntaxKind.EventKeyword => SemanticTokenTypes.Keyword,
            SyntaxKind.EachKeyword => SemanticTokenTypes.Keyword,
            SyntaxKind.NewKeyword => SemanticTokenTypes.Keyword,
            SyntaxKind.ImageKeyword => SemanticTokenTypes.Keyword,
            SyntaxKind.FontKeyword => SemanticTokenTypes.Keyword,
            SyntaxKind.AudioKeyword => SemanticTokenTypes.Keyword,
            SyntaxKind.VideoKeyword => SemanticTokenTypes.Keyword,
            SyntaxKind.TrueLiteralToken => SemanticTokenTypes.Keyword,
            SyntaxKind.FalseLiteralToken => SemanticTokenTypes.Keyword,
            SyntaxKind.NullLiteralToken => SemanticTokenTypes.Keyword,

            // Class / component names
            SyntaxKind.ComponentNameToken => SemanticTokenTypes.Class,

            // Properties
            SyntaxKind.IdentifierToken when IsPropertyName(token) => SemanticTokenTypes.Property,

            // Parameters
            SyntaxKind.IdentifierToken when IsParameterDecl(token) => SemanticTokenTypes.Parameter,

            // Variables (general identifiers)
            SyntaxKind.IdentifierToken => SemanticTokenTypes.Variable,

            // Global references ($controller, $root)
            SyntaxKind.GlobalRefToken => SemanticTokenTypes.Variable,

            // Event references (#signal)
            SyntaxKind.EventRefToken => SemanticTokenTypes.Event,

            // Enum values (.Center)
            SyntaxKind.EnumValueToken => SemanticTokenTypes.EnumMember,

            // Alias references (@alias)
            SyntaxKind.AliasRefToken => SemanticTokenTypes.Variable,

            // Strings
            SyntaxKind.StringLiteralToken => SemanticTokenTypes.String,
            SyntaxKind.TemplateStringLiteralToken => SemanticTokenTypes.String,

            // Numbers
            SyntaxKind.IntegerLiteralToken => SemanticTokenTypes.Number,
            SyntaxKind.FloatLiteralToken => SemanticTokenTypes.Number,

            // Mapping / arrow operators
            SyntaxKind.MapToPropertyToken => SemanticTokenTypes.Operator,
            SyntaxKind.MapToDataToken => SemanticTokenTypes.Operator,
            SyntaxKind.MapTwoWayToken => SemanticTokenTypes.Operator,
            SyntaxKind.FatArrowToken => SemanticTokenTypes.Operator,

            // Arithmetic operators
            SyntaxKind.PlusToken => SemanticTokenTypes.Operator,
            SyntaxKind.MinusToken => SemanticTokenTypes.Operator,
            SyntaxKind.AsteriskToken => SemanticTokenTypes.Operator,
            SyntaxKind.SlashToken => SemanticTokenTypes.Operator,
            SyntaxKind.PercentToken => SemanticTokenTypes.Operator,

            // Comparison operators
            SyntaxKind.EqualsEqualsToken => SemanticTokenTypes.Operator,
            SyntaxKind.BangEqualsToken => SemanticTokenTypes.Operator,
            SyntaxKind.LessThanToken => SemanticTokenTypes.Operator,
            SyntaxKind.GreaterThanToken => SemanticTokenTypes.Operator,
            SyntaxKind.LessThanEqualsToken => SemanticTokenTypes.Operator,
            SyntaxKind.GreaterThanEqualsToken => SemanticTokenTypes.Operator,

            // Logical operators
            SyntaxKind.AmpersandAmpersandToken => SemanticTokenTypes.Operator,
            SyntaxKind.BarBarToken => SemanticTokenTypes.Operator,
            SyntaxKind.BangToken => SemanticTokenTypes.Operator,

            // Ternary operator tokens
            SyntaxKind.QuestionToken => SemanticTokenTypes.Operator,
            SyntaxKind.ColonToken when token.Parent is ConditionalExpressionSyntax => SemanticTokenTypes.Operator,

            // Punctuation — map to operator so semantic highlighting covers
            SyntaxKind.DotToken => SemanticTokenTypes.Operator,
            SyntaxKind.ColonToken => SemanticTokenTypes.Operator,
            SyntaxKind.OpenParenToken => SemanticTokenTypes.Operator,
            SyntaxKind.CloseParenToken => SemanticTokenTypes.Operator,
            SyntaxKind.OpenBraceToken => SemanticTokenTypes.Operator,
            SyntaxKind.CloseBraceToken => SemanticTokenTypes.Operator,
            SyntaxKind.CommaToken => SemanticTokenTypes.Operator,
            SyntaxKind.PipeToken => SemanticTokenTypes.Operator,

            _ => -1 // Not a semantic token
        };
    }

    private static int GetModifiers(SyntaxToken token)
    {
        int mods = 0;

        // Parameter declarations get the "declaration" modifier
        if (token.Kind == SyntaxKind.IdentifierToken && IsParameterDecl(token))
            mods |= 1 << SemanticTokenModifiers.Declaration;

        // $controller is readonly
        if (token.Kind == SyntaxKind.GlobalRefToken)
            mods |= 1 << SemanticTokenModifiers.Readonly;

        return mods;
    }

    private static bool IsPropertyName(SyntaxToken token)
    {
        return token.Parent is PropertyAssignmentSyntax prop && token == prop.Name
               || token.Parent is MappingAssignmentSyntax mapping && token == mapping.Name
               || token.Parent is MemberAccessExpressionSyntax memberAccess && token == memberAccess.Name;
    }

    private static bool IsParameterDecl(SyntaxToken token)
    {
        return token.Parent is ParameterDeclarationSyntax param && token == param.Name;
    }

    private readonly record struct RawSemanticToken(
        int Line,
        int StartChar,
        int Length,
        int TokenType,
        int TokenModifiers);
}
