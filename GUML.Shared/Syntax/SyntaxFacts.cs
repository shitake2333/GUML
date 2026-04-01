namespace GUML.Shared.Syntax;

/// <summary>
/// Provides static utility methods for querying properties of <see cref="SyntaxKind"/> values,
/// including keyword recognition, operator precedence, and token text mappings.
/// </summary>
public static class SyntaxFacts
{
    /// <summary>
    /// Try to match a word to a keyword <see cref="SyntaxKind"/>. Returns <see cref="SyntaxKind.None"/> if not a keyword.
    /// </summary>
    public static SyntaxKind GetKeywordKind(string text)
    {
        switch (text)
        {
            case "import": return SyntaxKind.ImportKeyword;
            case "as": return SyntaxKind.AsKeyword;
            case "param": return SyntaxKind.ParamKeyword;
            case "event": return SyntaxKind.EventKeyword;
            case "each": return SyntaxKind.EachKeyword;
            case "new": return SyntaxKind.NewKeyword;
            case "image": return SyntaxKind.ImageKeyword;
            case "font": return SyntaxKind.FontKeyword;
            case "audio": return SyntaxKind.AudioKeyword;
            case "video": return SyntaxKind.VideoKeyword;
            case "true": return SyntaxKind.TrueLiteralToken;
            case "false": return SyntaxKind.FalseLiteralToken;
            case "null": return SyntaxKind.NullLiteralToken;
            default: return SyntaxKind.None;
        }
    }

    /// <summary>
    /// Returns whether the given kind is a keyword token.
    /// </summary>
    public static bool IsKeyword(SyntaxKind kind)
    {
        switch (kind)
        {
            case SyntaxKind.ImportKeyword:
            case SyntaxKind.AsKeyword:
            case SyntaxKind.ParamKeyword:
            case SyntaxKind.EventKeyword:
            case SyntaxKind.EachKeyword:
            case SyntaxKind.NewKeyword:
            case SyntaxKind.ImageKeyword:
            case SyntaxKind.FontKeyword:
            case SyntaxKind.AudioKeyword:
            case SyntaxKind.VideoKeyword:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Returns whether the given kind is a literal token.
    /// </summary>
    public static bool IsLiteral(SyntaxKind kind)
    {
        switch (kind)
        {
            case SyntaxKind.StringLiteralToken:
            case SyntaxKind.TemplateStringLiteralToken:
            case SyntaxKind.IntegerLiteralToken:
            case SyntaxKind.FloatLiteralToken:
            case SyntaxKind.TrueLiteralToken:
            case SyntaxKind.FalseLiteralToken:
            case SyntaxKind.NullLiteralToken:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Returns whether the given kind is a resource keyword (image, font, audio, video).
    /// </summary>
    public static bool IsResourceKeyword(SyntaxKind kind)
    {
        switch (kind)
        {
            case SyntaxKind.ImageKeyword:
            case SyntaxKind.FontKeyword:
            case SyntaxKind.AudioKeyword:
            case SyntaxKind.VideoKeyword:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Returns whether the given kind is a mapping operator (:=, =:, &lt;=&gt;).
    /// </summary>
    public static bool IsMappingOperator(SyntaxKind kind)
    {
        switch (kind)
        {
            case SyntaxKind.MapToPropertyToken:
            case SyntaxKind.MapToDataToken:
            case SyntaxKind.MapTwoWayToken:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Returns whether the given kind is a prefix unary operator (!, +, -).
    /// </summary>
    public static bool IsPrefixUnaryOperator(SyntaxKind kind)
    {
        switch (kind)
        {
            case SyntaxKind.BangToken:
            case SyntaxKind.PlusToken:
            case SyntaxKind.MinusToken:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Returns whether the given kind is a binary operator.
    /// </summary>
    public static bool IsBinaryOperator(SyntaxKind kind)
    {
        return GetBinaryOperatorPrecedence(kind) > 0;
    }

    /// <summary>
    /// Returns the precedence of a binary operator. Higher values bind tighter.
    /// Returns 0 if the kind is not a binary operator.
    /// </summary>
    /// <remarks>
    /// Precedence levels:
    ///   60 — *, /, %
    ///   50 — +, -
    ///   40 — &lt;, &gt;, &lt;=, &gt;=
    ///   30 — ==, !=
    ///   20 — &amp;&amp;
    ///   10 — ||
    /// </remarks>
    public static int GetBinaryOperatorPrecedence(SyntaxKind kind)
    {
        switch (kind)
        {
            case SyntaxKind.AsteriskToken:
            case SyntaxKind.SlashToken:
            case SyntaxKind.PercentToken:
                return 60;

            case SyntaxKind.PlusToken:
            case SyntaxKind.MinusToken:
                return 50;

            case SyntaxKind.LessThanToken:
            case SyntaxKind.GreaterThanToken:
            case SyntaxKind.LessThanEqualsToken:
            case SyntaxKind.GreaterThanEqualsToken:
                return 40;

            case SyntaxKind.EqualsEqualsToken:
            case SyntaxKind.BangEqualsToken:
                return 30;

            case SyntaxKind.AmpersandAmpersandToken:
                return 20;

            case SyntaxKind.BarBarToken:
                return 10;

            default:
                return 0;
        }
    }

    /// <summary>
    /// Returns the fixed text representation of a token kind, or null if the kind does not have fixed text.
    /// </summary>
    public static string? GetText(SyntaxKind kind)
    {
        switch (kind)
        {
            // Punctuators
            case SyntaxKind.OpenBraceToken: return "{";
            case SyntaxKind.CloseBraceToken: return "}";
            case SyntaxKind.OpenParenToken: return "(";
            case SyntaxKind.CloseParenToken: return ")";
            case SyntaxKind.OpenBracketToken: return "[";
            case SyntaxKind.CloseBracketToken: return "]";
            case SyntaxKind.CommaToken: return ",";
            case SyntaxKind.DotToken: return ".";
            case SyntaxKind.ColonToken: return ":";
            case SyntaxKind.PipeToken: return "|";
            case SyntaxKind.QuestionToken: return "?";
            case SyntaxKind.FatArrowToken: return "=>";
            case SyntaxKind.MapToPropertyToken: return ":=";
            case SyntaxKind.MapToDataToken: return "=:";
            case SyntaxKind.MapTwoWayToken: return "<=>";

            // Operators
            case SyntaxKind.PlusToken: return "+";
            case SyntaxKind.MinusToken: return "-";
            case SyntaxKind.AsteriskToken: return "*";
            case SyntaxKind.SlashToken: return "/";
            case SyntaxKind.PercentToken: return "%";
            case SyntaxKind.BangToken: return "!";
            case SyntaxKind.EqualsEqualsToken: return "==";
            case SyntaxKind.BangEqualsToken: return "!=";
            case SyntaxKind.LessThanToken: return "<";
            case SyntaxKind.GreaterThanToken: return ">";
            case SyntaxKind.LessThanEqualsToken: return "<=";
            case SyntaxKind.GreaterThanEqualsToken: return ">=";
            case SyntaxKind.BarBarToken: return "||";
            case SyntaxKind.AmpersandAmpersandToken: return "&&";

            // Keywords
            case SyntaxKind.ImportKeyword: return "import";
            case SyntaxKind.AsKeyword: return "as";
            case SyntaxKind.ParamKeyword: return "param";
            case SyntaxKind.EventKeyword: return "event";
            case SyntaxKind.EachKeyword: return "each";
            case SyntaxKind.NewKeyword: return "new";
            case SyntaxKind.ImageKeyword: return "image";
            case SyntaxKind.FontKeyword: return "font";
            case SyntaxKind.AudioKeyword: return "audio";
            case SyntaxKind.VideoKeyword: return "video";

            // Boolean and null literals
            case SyntaxKind.TrueLiteralToken: return "true";
            case SyntaxKind.FalseLiteralToken: return "false";
            case SyntaxKind.NullLiteralToken: return "null";

            default: return null;
        }
    }

    /// <summary>
    /// Returns whether a token kind can start an expression.
    /// </summary>
    public static bool CanStartExpression(SyntaxKind kind)
    {
        switch (kind)
        {
            // Literals
            case SyntaxKind.StringLiteralToken:
            case SyntaxKind.TemplateStringLiteralToken:
            case SyntaxKind.IntegerLiteralToken:
            case SyntaxKind.FloatLiteralToken:
            case SyntaxKind.TrueLiteralToken:
            case SyntaxKind.FalseLiteralToken:
            case SyntaxKind.NullLiteralToken:
            // References
            case SyntaxKind.IdentifierToken:
            case SyntaxKind.GlobalRefToken:
            case SyntaxKind.AliasRefToken:
            case SyntaxKind.ComponentNameToken:
            // Enum value
            case SyntaxKind.EnumValueToken:
            // Prefix unary
            case SyntaxKind.BangToken:
            case SyntaxKind.PlusToken:
            case SyntaxKind.MinusToken:
            // Parenthesized
            case SyntaxKind.OpenParenToken:
            // New expression
            case SyntaxKind.NewKeyword:
            // Resource keywords
            case SyntaxKind.ImageKeyword:
            case SyntaxKind.FontKeyword:
            case SyntaxKind.AudioKeyword:
            case SyntaxKind.VideoKeyword:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Returns whether a token kind can start a component body member.
    /// </summary>
    public static bool CanStartMember(SyntaxKind kind)
    {
        switch (kind)
        {
            case SyntaxKind.IdentifierToken:       // property, mapping, template param
            case SyntaxKind.EventRefToken:          // #event subscription
            case SyntaxKind.ParamKeyword:           // param declaration
            case SyntaxKind.EventKeyword:           // event declaration
            case SyntaxKind.EachKeyword:            // each block
            case SyntaxKind.ComponentNameToken:     // nested component
            case SyntaxKind.AliasRefToken:          // @alias: Component
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Returns whether the given kind is trivia.
    /// </summary>
    public static bool IsTrivia(SyntaxKind kind)
    {
        switch (kind)
        {
            case SyntaxKind.WhitespaceTrivia:
            case SyntaxKind.EndOfLineTrivia:
            case SyntaxKind.SingleLineCommentTrivia:
            case SyntaxKind.DocumentationCommentTrivia:
            case SyntaxKind.SkippedTokensTrivia:
                return true;
            default:
                return false;
        }
    }
}
