using System.Runtime.CompilerServices;

namespace GUML.Shared.Syntax.Internal;

/// <summary>
/// Hand-written lexer for the GUML language.
/// Produces <see cref="SyntaxToken"/> values with attached leading/trailing trivia.
/// Error-tolerant: unknown characters produce <see cref="SyntaxKind.BadToken"/>.
/// </summary>
internal sealed class Lexer
{
    private readonly string _source;
    private int _position;
    private readonly List<Diagnostic> _diagnostics = new List<Diagnostic>();

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    public Lexer(string source, int startPosition = 0)
    {
        _source = source;
        _position = startPosition;
    }

    /// <summary>
    /// Lex the entire source into a list of tokens. The last token is always <see cref="SyntaxKind.EndOfFileToken"/>.
    /// </summary>
    public List<SyntaxToken> LexAll()
    {
        var tokens = new List<SyntaxToken>(_source.Length / 5);
        while (true)
        {
            var token = NextToken();
            tokens.Add(token);
            if (token.Kind == SyntaxKind.EndOfFileToken)
                break;
        }

        return tokens;
    }

    /// <summary>
    /// Read the next token from the source, attaching leading and trailing trivia.
    /// </summary>
    public SyntaxToken NextToken()
    {
        var leading = ScanLeadingTrivia();
        if (_position >= _source.Length)
        {
            return new SyntaxToken(SyntaxKind.EndOfFileToken, "", leading, SyntaxTriviaList.Empty);
        }

        var kind = SyntaxKind.BadToken;
        int start = _position;

        char c = Current;
        switch (c)
        {
            case '{':
                Advance();
                kind = SyntaxKind.OpenBraceToken;
                break;
            case '}':
                Advance();
                kind = SyntaxKind.CloseBraceToken;
                break;
            case '(':
                Advance();
                kind = SyntaxKind.OpenParenToken;
                break;
            case ')':
                Advance();
                kind = SyntaxKind.CloseParenToken;
                break;
            case '[':
                Advance();
                kind = SyntaxKind.OpenBracketToken;
                break;
            case ']':
                Advance();
                kind = SyntaxKind.CloseBracketToken;
                break;
            case ',':
                Advance();
                kind = SyntaxKind.CommaToken;
                break;
            case '.':
                if (Peek(1) is { } pc && char.IsUpper(pc))
                {
                    kind = ScanEnumValue();
                }
                else
                {
                    Advance();
                    kind = SyntaxKind.DotToken;
                }

                break;
            case '|':
                if (Peek(1) == '|')
                {
                    Advance();
                    Advance();
                    kind = SyntaxKind.BarBarToken;
                }
                else
                {
                    Advance();
                    kind = SyntaxKind.PipeToken;
                }

                break;
            case '?':
                Advance();
                kind = SyntaxKind.QuestionToken;
                break;
            case '+':
                Advance();
                kind = SyntaxKind.PlusToken;
                break;
            case '-':
                Advance();
                kind = SyntaxKind.MinusToken;
                break;
            case '*':
                Advance();
                kind = SyntaxKind.AsteriskToken;
                break;
            case '/':
                Advance();
                kind = SyntaxKind.SlashToken;
                break;
            case '%':
                Advance();
                kind = SyntaxKind.PercentToken;
                break;
            case '!':
                if (Peek(1) == '=')
                {
                    Advance();
                    Advance();
                    kind = SyntaxKind.BangEqualsToken;
                }
                else
                {
                    Advance();
                    kind = SyntaxKind.BangToken;
                }

                break;
            case '=':
                if (Peek(1) == '>')
                {
                    Advance();
                    Advance();
                    kind = SyntaxKind.FatArrowToken;
                }
                else if (Peek(1) == '=')
                {
                    Advance();
                    Advance();
                    kind = SyntaxKind.EqualsEqualsToken;
                }
                else if (Peek(1) == ':')
                {
                    Advance();
                    Advance();
                    kind = SyntaxKind.MapToDataToken;
                }
                else
                {
                    Advance();
                    // Not a valid GUML operator; emit BadToken
                }

                break;
            case ':':
                if (Peek(1) == '=')
                {
                    Advance();
                    Advance();
                    kind = SyntaxKind.MapToPropertyToken;
                }
                else
                {
                    Advance();
                    kind = SyntaxKind.ColonToken;
                }

                break;
            case '<':
                if (Peek(1) == '=' && Peek(2) == '>')
                {
                    Advance();
                    Advance();
                    Advance();
                    kind = SyntaxKind.MapTwoWayToken;
                }
                else if (Peek(1) == '=')
                {
                    Advance();
                    Advance();
                    kind = SyntaxKind.LessThanEqualsToken;
                }
                else
                {
                    Advance();
                    kind = SyntaxKind.LessThanToken;
                }

                break;
            case '>':
                if (Peek(1) == '=')
                {
                    Advance();
                    Advance();
                    kind = SyntaxKind.GreaterThanEqualsToken;
                }
                else
                {
                    Advance();
                    kind = SyntaxKind.GreaterThanToken;
                }

                break;
            case '&':
                if (Peek(1) == '&')
                {
                    Advance();
                    Advance();
                    kind = SyntaxKind.AmpersandAmpersandToken;
                }
                else
                {
                    Advance(); /* BadToken for single & */
                }

                break;
            case '"':
                kind = ScanStringLiteral();
                break;
            case '$':
                kind = Peek(1) == '"' ? ScanTemplateString() : ScanGlobalRef();
                break;
            case '@':
                kind = ScanAliasRef();
                break;
            case '#':
                kind = ScanEventRef();
                break;
            default:
                if (char.IsDigit(c))
                {
                    kind = ScanNumber();
                }
                else if (IsIdentStart(c))
                {
                    kind = ScanIdentifierOrKeyword();
                }
                else
                {
                    _diagnostics.Add(new Diagnostic("GUML0001", $"Unexpected character '{c}'",
                        DiagnosticSeverity.Error, new TextSpan(_position, 1)));
                    Advance();
                    // kind remains BadToken
                }

                break;
        }

        string text = _source.Substring(start, _position - start);
        var trailing = ScanTrailingTrivia();
        return new SyntaxToken(kind, text, leading, trailing);
    }

    // ------------------------------------------------------------------
    // Trivia scanning
    // ------------------------------------------------------------------

    private SyntaxTriviaList ScanLeadingTrivia()
    {
        List<SyntaxTrivia>? list = null;
        while (_position < _source.Length)
        {
            char c = Current;
            if (c == ' ' || c == '\t')
            {
                var trivia = ScanWhitespace();
                (list ??= new List<SyntaxTrivia>()).Add(trivia);
            }
            else if (c == '\r' || c == '\n')
            {
                var trivia = ScanEndOfLine();
                (list ??= new List<SyntaxTrivia>()).Add(trivia);
            }
            else if (c == '/' && Peek(1) == '/')
            {
                if (Peek(2) == '/')
                {
                    var trivia = ScanDocComment();
                    (list ??= new List<SyntaxTrivia>()).Add(trivia);
                }
                else
                {
                    var trivia = ScanSingleLineComment();
                    (list ??= new List<SyntaxTrivia>()).Add(trivia);
                }
            }
            else
            {
                break;
            }
        }

        return list == null ? SyntaxTriviaList.Empty : new SyntaxTriviaList(list.ToArray());
    }

    private SyntaxTriviaList ScanTrailingTrivia()
    {
        // Trailing trivia: whitespace/tabs on the same line up to (but not including) the newline.
        List<SyntaxTrivia>? list = null;
        while (_position < _source.Length)
        {
            char c = Current;
            if (c == ' ' || c == '\t')
            {
                var trivia = ScanWhitespace();
                (list ??= new List<SyntaxTrivia>()).Add(trivia);
            }
            else if (c == '/' && Peek(1) == '/')
            {
                // Single-line comment on the same line is trailing trivia
                if (Peek(2) == '/')
                {
                    var trivia = ScanDocComment();
                    (list ??= new List<SyntaxTrivia>()).Add(trivia);
                }
                else
                {
                    var trivia = ScanSingleLineComment();
                    (list ??= new List<SyntaxTrivia>()).Add(trivia);
                }
            }
            else if (c == '\r' || c == '\n')
            {
                // Include the newline as trailing trivia, then stop
                var trivia = ScanEndOfLine();
                (list ??= new List<SyntaxTrivia>()).Add(trivia);
                break;
            }
            else
            {
                break;
            }
        }

        return list == null ? SyntaxTriviaList.Empty : new SyntaxTriviaList(list.ToArray());
    }

    private SyntaxTrivia ScanWhitespace()
    {
        int start = _position;
        while (_position < _source.Length && (Current == ' ' || Current == '\t'))
            Advance();
        return new SyntaxTrivia(SyntaxKind.WhitespaceTrivia, _source.Substring(start, _position - start));
    }

    private SyntaxTrivia ScanEndOfLine()
    {
        int start = _position;
        if (Current == '\r')
        {
            Advance();
            if (_position < _source.Length && Current == '\n')
                Advance();
        }
        else
        {
            Advance(); // '\n'
        }

        return new SyntaxTrivia(SyntaxKind.EndOfLineTrivia, _source.Substring(start, _position - start));
    }

    private SyntaxTrivia ScanSingleLineComment()
    {
        int start = _position;
        while (_position < _source.Length && Current != '\r' && Current != '\n')
            Advance();
        return new SyntaxTrivia(SyntaxKind.SingleLineCommentTrivia, _source.Substring(start, _position - start));
    }

    private SyntaxTrivia ScanDocComment()
    {
        int start = _position;
        while (_position < _source.Length && Current != '\r' && Current != '\n')
            Advance();
        return new SyntaxTrivia(SyntaxKind.DocumentationCommentTrivia, _source.Substring(start, _position - start));
    }

    // ------------------------------------------------------------------
    // Token scanning
    // ------------------------------------------------------------------

    private SyntaxKind ScanStringLiteral()
    {
        Advance(); // opening "
        while (_position < _source.Length)
        {
            char c = Current;
            if (c == '"')
            {
                Advance();
                return SyntaxKind.StringLiteralToken;
            }

            if (c == '\\')
            {
                Advance(); // backslash
                if (_position < _source.Length)
                    Advance(); // escaped char
            }
            else if (c == '\r' || c == '\n')
            {
                // Unterminated string at newline
                break;
            }
            else
            {
                Advance();
            }
        }

        _diagnostics.Add(new Diagnostic("GUML0002", "Unterminated string literal",
            DiagnosticSeverity.Error, new TextSpan(_position, 0)));
        return SyntaxKind.StringLiteralToken;
    }

    private SyntaxKind ScanTemplateString()
    {
        Advance(); // $
        Advance(); // opening "
        int braceDepth = 0;
        while (_position < _source.Length)
        {
            char c = Current;
            if (braceDepth > 0)
            {
                if (c == '{') braceDepth++;
                else if (c == '}') braceDepth--;
                Advance();
            }
            else if (c == '"')
            {
                Advance();
                return SyntaxKind.TemplateStringLiteralToken;
            }
            else if (c == '\\')
            {
                Advance();
                if (_position < _source.Length)
                    Advance();
            }
            else if (c == '{')
            {
                braceDepth++;
                Advance();
            }
            else if (c == '\r' || c == '\n')
            {
                break;
            }
            else
            {
                Advance();
            }
        }

        _diagnostics.Add(new Diagnostic("GUML0003", "Unterminated template string literal",
            DiagnosticSeverity.Error, new TextSpan(_position, 0)));
        return SyntaxKind.TemplateStringLiteralToken;
    }

    private SyntaxKind ScanNumber()
    {
        bool isFloat = false;
        while (_position < _source.Length && char.IsDigit(Current))
            Advance();
        if (_position < _source.Length && Current == '.' && Peek(1) is { } nc && char.IsDigit(nc))
        {
            isFloat = true;
            Advance(); // .
            while (_position < _source.Length && char.IsDigit(Current))
                Advance();
        }

        return isFloat ? SyntaxKind.FloatLiteralToken : SyntaxKind.IntegerLiteralToken;
    }

    private SyntaxKind ScanIdentifierOrKeyword()
    {
        int start = _position;
        Advance(); // first char already validated
        while (_position < _source.Length && IsIdentPart(Current))
            Advance();

        string word = _source.Substring(start, _position - start);

        // PascalCase identifiers that start with uppercase are component names
        if (char.IsUpper(word[0]))
        {
            return SyntaxKind.ComponentNameToken;
        }

        // Check keywords
        SyntaxKind kw = SyntaxFacts.GetKeywordKind(word);
        if (kw != SyntaxKind.None)
            return kw;

        return SyntaxKind.IdentifierToken;
    }

    private SyntaxKind ScanGlobalRef()
    {
        // $identifier
        Advance(); // $
        if (_position < _source.Length && IsIdentStart(Current))
        {
            while (_position < _source.Length && IsIdentPart(Current))
                Advance();
            return SyntaxKind.GlobalRefToken;
        }

        _diagnostics.Add(new Diagnostic("GUML0004", "Expected identifier after '$'",
            DiagnosticSeverity.Error, new TextSpan(_position, 0)));
        return SyntaxKind.GlobalRefToken;
    }

    private SyntaxKind ScanAliasRef()
    {
        // @identifier
        Advance(); // @
        if (_position < _source.Length && IsIdentStart(Current))
        {
            while (_position < _source.Length && IsIdentPart(Current))
                Advance();
            return SyntaxKind.AliasRefToken;
        }

        _diagnostics.Add(new Diagnostic("GUML0005", "Expected identifier after '@'",
            DiagnosticSeverity.Error, new TextSpan(_position, 0)));
        return SyntaxKind.AliasRefToken;
    }

    private SyntaxKind ScanEventRef()
    {
        // #identifier
        Advance(); // #
        if (_position < _source.Length && IsIdentStart(Current))
        {
            while (_position < _source.Length && IsIdentPart(Current))
                Advance();
            return SyntaxKind.EventRefToken;
        }

        _diagnostics.Add(new Diagnostic("GUML0006", "Expected identifier after '#'",
            DiagnosticSeverity.Error, new TextSpan(_position, 0)));
        return SyntaxKind.EventRefToken;
    }

    private SyntaxKind ScanEnumValue()
    {
        // .PascalCase
        Advance(); // .
        if (_position < _source.Length && char.IsUpper(Current))
        {
            while (_position < _source.Length && IsIdentPart(Current))
                Advance();
            return SyntaxKind.EnumValueToken;
        }

        // Should not reach here since caller checked, but just in case
        return SyntaxKind.DotToken;
    }

    // ------------------------------------------------------------------
    // Character helpers
    // ------------------------------------------------------------------

    private char Current => _source[_position];

    private char? Peek(int offset)
    {
        int idx = _position + offset;
        return idx < _source.Length ? _source[idx] : null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Advance() => _position++;

    private static bool IsIdentStart(char c) => c == '_' || char.IsLetter(c);
    private static bool IsIdentPart(char c) => c == '_' || char.IsLetterOrDigit(c);
}
