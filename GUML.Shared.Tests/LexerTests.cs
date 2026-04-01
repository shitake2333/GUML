using GUML.Shared.Syntax;
using GUML.Shared.Syntax.Internal;

namespace GUML.Shared.Tests;

[TestClass]
public class LexerTests
{
    private static List<SyntaxToken> Lex(string source)
    {
        var lexer = new Lexer(source);
        return lexer.LexAll();
    }

    private static SyntaxToken LexSingle(string source)
    {
        var tokens = Lex(source);
        // Expect exactly one non-EOF token followed by EOF
        Assert.IsTrue(tokens.Count >= 2, $"Expected at least 2 tokens, got {tokens.Count}");
        Assert.AreEqual(SyntaxKind.EndOfFileToken, tokens[tokens.Count - 1].Kind);
        return tokens[0];
    }

    // ================================================================
    // Basic token recognition
    // ================================================================

    [TestMethod]
    [DataRow("{", SyntaxKind.OpenBraceToken)]
    [DataRow("}", SyntaxKind.CloseBraceToken)]
    [DataRow("(", SyntaxKind.OpenParenToken)]
    [DataRow(")", SyntaxKind.CloseParenToken)]
    [DataRow("[", SyntaxKind.OpenBracketToken)]
    [DataRow("]", SyntaxKind.CloseBracketToken)]
    [DataRow(",", SyntaxKind.CommaToken)]
    [DataRow(".", SyntaxKind.DotToken)]
    [DataRow(":", SyntaxKind.ColonToken)]
    [DataRow("|", SyntaxKind.PipeToken)]
    [DataRow("?", SyntaxKind.QuestionToken)]
    [DataRow("=>", SyntaxKind.FatArrowToken)]
    [DataRow(":=", SyntaxKind.MapToPropertyToken)]
    [DataRow("=:", SyntaxKind.MapToDataToken)]
    [DataRow("<=>", SyntaxKind.MapTwoWayToken)]
    public void Punctuators(string text, SyntaxKind expected)
    {
        var token = LexSingle(text);
        Assert.AreEqual(expected, token.Kind);
        Assert.AreEqual(text, token.Text);
    }

    [TestMethod]
    [DataRow("+", SyntaxKind.PlusToken)]
    [DataRow("-", SyntaxKind.MinusToken)]
    [DataRow("*", SyntaxKind.AsteriskToken)]
    [DataRow("/", SyntaxKind.SlashToken)]
    [DataRow("%", SyntaxKind.PercentToken)]
    [DataRow("!", SyntaxKind.BangToken)]
    [DataRow("==", SyntaxKind.EqualsEqualsToken)]
    [DataRow("!=", SyntaxKind.BangEqualsToken)]
    [DataRow("<", SyntaxKind.LessThanToken)]
    [DataRow(">", SyntaxKind.GreaterThanToken)]
    [DataRow("<=", SyntaxKind.LessThanEqualsToken)]
    [DataRow(">=", SyntaxKind.GreaterThanEqualsToken)]
    [DataRow("||", SyntaxKind.BarBarToken)]
    [DataRow("&&", SyntaxKind.AmpersandAmpersandToken)]
    public void Operators(string text, SyntaxKind expected)
    {
        var token = LexSingle(text);
        Assert.AreEqual(expected, token.Kind);
        Assert.AreEqual(text, token.Text);
    }

    [TestMethod]
    [DataRow("import", SyntaxKind.ImportKeyword)]
    [DataRow("as", SyntaxKind.AsKeyword)]
    [DataRow("param", SyntaxKind.ParamKeyword)]
    [DataRow("event", SyntaxKind.EventKeyword)]
    [DataRow("each", SyntaxKind.EachKeyword)]
    [DataRow("new", SyntaxKind.NewKeyword)]
    [DataRow("image", SyntaxKind.ImageKeyword)]
    [DataRow("font", SyntaxKind.FontKeyword)]
    [DataRow("audio", SyntaxKind.AudioKeyword)]
    [DataRow("video", SyntaxKind.VideoKeyword)]
    [DataRow("true", SyntaxKind.TrueLiteralToken)]
    [DataRow("false", SyntaxKind.FalseLiteralToken)]
    [DataRow("null", SyntaxKind.NullLiteralToken)]
    public void Keywords(string text, SyntaxKind expected)
    {
        var token = LexSingle(text);
        Assert.AreEqual(expected, token.Kind);
        Assert.AreEqual(text, token.Text);
    }

    // ================================================================
    // Identifiers
    // ================================================================

    [TestMethod]
    public void LowercaseIdentifier()
    {
        var token = LexSingle("myProp");
        Assert.AreEqual(SyntaxKind.IdentifierToken, token.Kind);
        Assert.AreEqual("myProp", token.Text);
    }

    [TestMethod]
    public void UppercaseIdentifier_IsComponentName()
    {
        var token = LexSingle("Button");
        Assert.AreEqual(SyntaxKind.ComponentNameToken, token.Kind);
        Assert.AreEqual("Button", token.Text);
    }

    [TestMethod]
    public void UnderscoreIdentifier()
    {
        var token = LexSingle("_private");
        Assert.AreEqual(SyntaxKind.IdentifierToken, token.Kind);
        Assert.AreEqual("_private", token.Text);
    }

    // ================================================================
    // Literals
    // ================================================================

    [TestMethod]
    public void IntegerLiteral()
    {
        var token = LexSingle("42");
        Assert.AreEqual(SyntaxKind.IntegerLiteralToken, token.Kind);
        Assert.AreEqual("42", token.Text);
    }

    [TestMethod]
    public void FloatLiteral()
    {
        var token = LexSingle("3.14");
        Assert.AreEqual(SyntaxKind.FloatLiteralToken, token.Kind);
        Assert.AreEqual("3.14", token.Text);
    }

    [TestMethod]
    public void StringLiteral()
    {
        var token = LexSingle("\"hello world\"");
        Assert.AreEqual(SyntaxKind.StringLiteralToken, token.Kind);
        Assert.AreEqual("\"hello world\"", token.Text);
    }

    [TestMethod]
    public void StringLiteral_WithEscapes()
    {
        var token = LexSingle("\"line1\\nline2\"");
        Assert.AreEqual(SyntaxKind.StringLiteralToken, token.Kind);
        Assert.AreEqual("\"line1\\nline2\"", token.Text);
    }

    [TestMethod]
    public void TemplateStringLiteral()
    {
        var token = LexSingle("$\"hello {name}\"");
        Assert.AreEqual(SyntaxKind.TemplateStringLiteralToken, token.Kind);
        Assert.AreEqual("$\"hello {name}\"", token.Text);
    }

    [TestMethod]
    public void TemplateStringLiteral_NestedBraces()
    {
        var token = LexSingle("$\"value: {a + b}\"");
        Assert.AreEqual(SyntaxKind.TemplateStringLiteralToken, token.Kind);
        Assert.AreEqual("$\"value: {a + b}\"", token.Text);
    }

    // ================================================================
    // Special references
    // ================================================================

    [TestMethod]
    public void GlobalRef()
    {
        var token = LexSingle("$controller");
        Assert.AreEqual(SyntaxKind.GlobalRefToken, token.Kind);
        Assert.AreEqual("$controller", token.Text);
    }

    [TestMethod]
    public void AliasRef()
    {
        var token = LexSingle("@myAlias");
        Assert.AreEqual(SyntaxKind.AliasRefToken, token.Kind);
        Assert.AreEqual("@myAlias", token.Text);
    }

    [TestMethod]
    public void EventRef()
    {
        var token = LexSingle("#pressed");
        Assert.AreEqual(SyntaxKind.EventRefToken, token.Kind);
        Assert.AreEqual("#pressed", token.Text);
    }

    [TestMethod]
    public void EnumValue()
    {
        var token = LexSingle(".Center");
        Assert.AreEqual(SyntaxKind.EnumValueToken, token.Kind);
        Assert.AreEqual(".Center", token.Text);
    }

    [TestMethod]
    public void Dot_NotEnumValue_WhenFollowedByLowercase()
    {
        // ".member" should be dot + identifier, not enum value
        var tokens = Lex(".member");
        Assert.AreEqual(SyntaxKind.DotToken, tokens[0].Kind);
        Assert.AreEqual(".", tokens[0].Text);
        Assert.AreEqual(SyntaxKind.IdentifierToken, tokens[1].Kind);
        Assert.AreEqual("member", tokens[1].Text);
    }

    // ================================================================
    // Trivia handling
    // ================================================================

    [TestMethod]
    public void LeadingWhitespaceTrivia()
    {
        var token = LexSingle("   hello");
        Assert.AreEqual(SyntaxKind.IdentifierToken, token.Kind);
        Assert.AreEqual("hello", token.Text);
        Assert.AreEqual(1, token.LeadingTrivia.Count);
        Assert.AreEqual(SyntaxKind.WhitespaceTrivia, token.LeadingTrivia[0].Kind);
        Assert.AreEqual("   ", token.LeadingTrivia[0].Text);
    }

    [TestMethod]
    public void TrailingWhitespaceTrivia()
    {
        // After "hello", there is trailing whitespace then newline
        var tokens = Lex("hello   \nworld");
        var first = tokens[0];
        Assert.AreEqual("hello", first.Text);
        Assert.IsTrue(first.TrailingTrivia.Count >= 1);
    }

    [TestMethod]
    public void SingleLineComment_AsLeadingTrivia()
    {
        var tokens = Lex("// comment\nhello");
        // The comment + newline is leading trivia of "hello"
        var helloToken = tokens[0];
        Assert.AreEqual(SyntaxKind.IdentifierToken, helloToken.Kind);
        Assert.AreEqual("hello", helloToken.Text);
        // Leading trivia should contain comment and newline
        bool hasComment = false;
        foreach (var trivia in helloToken.LeadingTrivia)
        {
            if (trivia.Kind == SyntaxKind.SingleLineCommentTrivia)
                hasComment = true;
        }

        Assert.IsTrue(hasComment, "Expected single line comment in leading trivia");
    }

    [TestMethod]
    public void DocumentationComment_AsTrivia()
    {
        var tokens = Lex("/// doc comment\nhello");
        var helloToken = tokens[0];
        Assert.AreEqual("hello", helloToken.Text);
        bool hasDocComment = false;
        foreach (var trivia in helloToken.LeadingTrivia)
        {
            if (trivia.Kind == SyntaxKind.DocumentationCommentTrivia)
                hasDocComment = true;
        }

        Assert.IsTrue(hasDocComment, "Expected documentation comment in leading trivia");
    }

    [TestMethod]
    public void TrailingComment()
    {
        var tokens = Lex("hello // trail");
        var first = tokens[0];
        Assert.AreEqual("hello", first.Text);
        bool hasTrailComment = false;
        foreach (var trivia in first.TrailingTrivia)
        {
            if (trivia.Kind == SyntaxKind.SingleLineCommentTrivia)
                hasTrailComment = true;
        }

        Assert.IsTrue(hasTrailComment, "Expected trailing comment");
    }

    // ================================================================
    // Error tolerance
    // ================================================================

    [TestMethod]
    public void UnterminatedString_ProducesDiagnostic()
    {
        var lexer = new Lexer("\"unterminated");
        var tokens = lexer.LexAll();
        Assert.AreEqual(SyntaxKind.StringLiteralToken, tokens[0].Kind);
        Assert.IsTrue(lexer.Diagnostics.Count > 0, "Expected diagnostic for unterminated string");
        Assert.AreEqual("GUML0002", lexer.Diagnostics[0].Id);
    }

    [TestMethod]
    public void UnterminatedTemplateString_ProducesDiagnostic()
    {
        var lexer = new Lexer("$\"hello {name");
        var tokens = lexer.LexAll();
        Assert.AreEqual(SyntaxKind.TemplateStringLiteralToken, tokens[0].Kind);
        Assert.IsTrue(lexer.Diagnostics.Count > 0, "Expected diagnostic for unterminated template string");
        Assert.AreEqual("GUML0003", lexer.Diagnostics[0].Id);
    }

    [TestMethod]
    public void UnexpectedCharacter_ProducesBadToken()
    {
        var lexer = new Lexer("~");
        var tokens = lexer.LexAll();
        Assert.AreEqual(SyntaxKind.BadToken, tokens[0].Kind);
        Assert.IsTrue(lexer.Diagnostics.Count > 0);
        Assert.AreEqual("GUML0001", lexer.Diagnostics[0].Id);
    }

    [TestMethod]
    public void DollarWithoutIdentifier_ProducesDiagnostic()
    {
        var lexer = new Lexer("$ ");
        _ = lexer.LexAll();
        Assert.IsTrue(lexer.Diagnostics.Count > 0);
        Assert.AreEqual("GUML0004", lexer.Diagnostics[0].Id);
    }

    [TestMethod]
    public void AtWithoutIdentifier_ProducesDiagnostic()
    {
        var lexer = new Lexer("@ ");
        _ = lexer.LexAll();
        Assert.IsTrue(lexer.Diagnostics.Count > 0);
        Assert.AreEqual("GUML0005", lexer.Diagnostics[0].Id);
    }

    [TestMethod]
    public void HashWithoutIdentifier_ProducesDiagnostic()
    {
        var lexer = new Lexer("# ");
        _ = lexer.LexAll();
        Assert.IsTrue(lexer.Diagnostics.Count > 0);
        Assert.AreEqual("GUML0006", lexer.Diagnostics[0].Id);
    }

    // ================================================================
    // Edge cases
    // ================================================================

    [TestMethod]
    public void EmptyInput()
    {
        var tokens = Lex("");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.EndOfFileToken, tokens[0].Kind);
    }

    [TestMethod]
    public void WhitespaceOnly()
    {
        var tokens = Lex("   \t  ");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.EndOfFileToken, tokens[0].Kind);
        Assert.IsTrue(tokens[0].LeadingTrivia.Count > 0);
    }

    [TestMethod]
    public void CommentOnly()
    {
        var tokens = Lex("// a comment");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(SyntaxKind.EndOfFileToken, tokens[0].Kind);
    }

    [TestMethod]
    public void MultipleTokenSequence()
    {
        var tokens = Lex("name: 42");
        // name, :, 42, EOF
        Assert.AreEqual(4, tokens.Count);
        Assert.AreEqual(SyntaxKind.IdentifierToken, tokens[0].Kind);
        Assert.AreEqual(SyntaxKind.ColonToken, tokens[1].Kind);
        Assert.AreEqual(SyntaxKind.IntegerLiteralToken, tokens[2].Kind);
        Assert.AreEqual(SyntaxKind.EndOfFileToken, tokens[3].Kind);
    }

    [TestMethod]
    public void LexAll_EndsWithEOF()
    {
        var tokens = Lex("Button { }");
        Assert.AreEqual(SyntaxKind.EndOfFileToken, tokens[tokens.Count - 1].Kind);
    }

    [TestMethod]
    public void FullWidth_IncludesTrivia()
    {
        var token = LexSingle("  hello  ");
        // leading "  " + text "hello" + trailing "  "
        Assert.AreEqual(9, token.FullWidth);
        Assert.AreEqual(5, token.Width);
    }

    [TestMethod]
    public void CompositeOperator_LessThanEquals_NotMapTwoWay()
    {
        // "<=" should be LessThanEqualsToken, not start of "<=>"
        var tokens = Lex("<= x");
        Assert.AreEqual(SyntaxKind.LessThanEqualsToken, tokens[0].Kind);
    }

    [TestMethod]
    public void MapTwoWay_Token()
    {
        var token = LexSingle("<=>");
        Assert.AreEqual(SyntaxKind.MapTwoWayToken, token.Kind);
        Assert.AreEqual("<=>", token.Text);
    }

    [TestMethod]
    public void SingleEquals_IsBadToken()
    {
        var token = LexSingle("=");
        // Single '=' is not a valid GUML operator → BadToken
        Assert.AreEqual(SyntaxKind.BadToken, token.Kind);
    }

    [TestMethod]
    public void SingleAmpersand_IsBadToken()
    {
        var token = LexSingle("&");
        Assert.AreEqual(SyntaxKind.BadToken, token.Kind);
    }

    // ================================================================
    // Additional edge-case tests
    // ================================================================

    [TestMethod]
    public void NegativeIntegerLiteral_ProducesTwoTokens()
    {
        // "-42" is lexed as MinusToken + IntegerLiteralToken (parser handles unary minus)
        var tokens = Lex("-42");
        Assert.AreEqual(3, tokens.Count); // -, 42, EOF
        Assert.AreEqual(SyntaxKind.MinusToken, tokens[0].Kind);
        Assert.AreEqual(SyntaxKind.IntegerLiteralToken, tokens[1].Kind);
        Assert.AreEqual("42", tokens[1].Text);
    }

    [TestMethod]
    public void ZeroIntegerLiteral()
    {
        var token = LexSingle("0");
        Assert.AreEqual(SyntaxKind.IntegerLiteralToken, token.Kind);
        Assert.AreEqual("0", token.Text);
    }

    [TestMethod]
    public void FloatLiteral_LeadingDotWithDigits_IsNotFloat()
    {
        // ".5" in GUML is DotToken + IntegerLiteralToken (no leading-dot float syntax)
        var tokens = Lex(".5");
        Assert.AreEqual(SyntaxKind.DotToken, tokens[0].Kind);
        Assert.AreEqual(SyntaxKind.IntegerLiteralToken, tokens[1].Kind);
    }

    [TestMethod]
    public void FloatLiteral_TrailingDot_IsIntegerAndDot()
    {
        // "1." in GUML is IntegerLiteralToken + DotToken (no trailing-dot float syntax)
        var tokens = Lex("1.");
        Assert.AreEqual(SyntaxKind.IntegerLiteralToken, tokens[0].Kind);
        Assert.AreEqual("1", tokens[0].Text);
        Assert.AreEqual(SyntaxKind.DotToken, tokens[1].Kind);
    }

    [TestMethod]
    public void StringLiteral_EmptyString()
    {
        var token = LexSingle("\"\"");
        Assert.AreEqual(SyntaxKind.StringLiteralToken, token.Kind);
        Assert.AreEqual("\"\"", token.Text);
    }

    [TestMethod]
    public void TemplateString_Empty()
    {
        var token = LexSingle("$\"\"");
        Assert.AreEqual(SyntaxKind.TemplateStringLiteralToken, token.Kind);
        Assert.AreEqual("$\"\"", token.Text);
    }

    [TestMethod]
    public void TemplateString_MultipleInterpolations()
    {
        var token = LexSingle("$\"{a} and {b}\"");
        Assert.AreEqual(SyntaxKind.TemplateStringLiteralToken, token.Kind);
        Assert.AreEqual("$\"{a} and {b}\"", token.Text);
    }

    [TestMethod]
    public void Identifier_StartsWithUnderscore_And_Digit()
    {
        var token = LexSingle("_item1");
        Assert.AreEqual(SyntaxKind.IdentifierToken, token.Kind);
        Assert.AreEqual("_item1", token.Text);
    }

    [TestMethod]
    public void ComponentName_WithDigits()
    {
        // "Vector2" starts with uppercase → ComponentNameToken
        var token = LexSingle("Vector2");
        Assert.AreEqual(SyntaxKind.ComponentNameToken, token.Kind);
        Assert.AreEqual("Vector2", token.Text);
    }

    [TestMethod]
    public void EnumValue_MultiplePascalCaseWords()
    {
        var token = LexSingle(".HorizontalCenter");
        Assert.AreEqual(SyntaxKind.EnumValueToken, token.Kind);
        Assert.AreEqual(".HorizontalCenter", token.Text);
    }

    [TestMethod]
    public void GlobalRef_WithUnderscore()
    {
        var token = LexSingle("$my_controller");
        Assert.AreEqual(SyntaxKind.GlobalRefToken, token.Kind);
        Assert.AreEqual("$my_controller", token.Text);
    }

    [TestMethod]
    public void AliasRef_WithUnderscore()
    {
        var token = LexSingle("@my_alias");
        Assert.AreEqual(SyntaxKind.AliasRefToken, token.Kind);
        Assert.AreEqual("@my_alias", token.Text);
    }

    [TestMethod]
    public void EventRef_WithUnderscore()
    {
        var token = LexSingle("#text_changed");
        Assert.AreEqual(SyntaxKind.EventRefToken, token.Kind);
        Assert.AreEqual("#text_changed", token.Text);
    }

    [TestMethod]
    public void MultipleNewlines_AreTrivia()
    {
        // Multiple newlines between tokens are all trivia
        var tokens = Lex("a\n\n\nb");
        Assert.AreEqual(3, tokens.Count); // a, b, EOF
        Assert.AreEqual("a", tokens[0].Text);
        Assert.AreEqual("b", tokens[1].Text);
        // The trailing trivia/leading trivia of "b" should include the newlines
        bool hasNewline = false;
        foreach (var trivia in tokens[1].LeadingTrivia)
        {
            if (trivia.Kind == SyntaxKind.EndOfLineTrivia)
                hasNewline = true;
        }
        Assert.IsTrue(hasNewline, "Expected end-of-line trivia between tokens");
    }

    [TestMethod]
    public void TabIndent_IsWhitespaceTrivia()
    {
        var token = LexSingle("\t\thello");
        Assert.AreEqual(1, token.LeadingTrivia.Count);
        Assert.AreEqual(SyntaxKind.WhitespaceTrivia, token.LeadingTrivia[0].Kind);
        Assert.AreEqual("\t\t", token.LeadingTrivia[0].Text);
    }

    [TestMethod]
    public void StringLiteral_WithUnicodeCharacters()
    {
        var token = LexSingle("\"日本語\"");
        Assert.AreEqual(SyntaxKind.StringLiteralToken, token.Kind);
        Assert.AreEqual("\"日本語\"", token.Text);
    }

    [TestMethod]
    public void Span_IsSet_AfterLexing()
    {
        // Span positions are absolute; without ComputePositions, _position=0
        // Leading trivia "  " (2 chars) means Span.Start = 2
        var token = LexSingle("  hello");
        Assert.AreEqual(2, token.Span.Start);  // after leading trivia
        Assert.AreEqual(7, token.Span.End);    // start + width
    }
}
