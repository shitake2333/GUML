using RegexTokenizeGenerator;

namespace RegexTokenizeGenerator.Tests;

[TestClass]
public class TokenizerTests
{
    private TokenizeGenerator? _tokenizer;
    private List<(Func<string, ITokenize, string>, Func<ITokenize, string>)>? _rules;

    [TestInitialize]
    public void Setup()
    {
        var patternWhitespace = TokenizeGenerator.CharsPattern(['\t', ' ']);
        var patternNewline = TokenizeGenerator.CharsPattern(['\r', '\n']);

        // Define some patterns
        var patternString = TokenizeGenerator.StringPattern();

        // Trie test: multiple patterns matching prefixes
        var patternOperators = TokenizeGenerator.ValuesPattern(["==", "=", "!=", "!", "++", "+"]);

        var patternInteger = TokenizeGenerator.NumberPattern(false);
        var patternIdentifier = TokenizeGenerator.ValuesPattern(["foo", "bar"]); // Simple exact match for these

        _rules = new List<(Func<string, ITokenize, string>, Func<ITokenize, string>)>
        {
            ((_, _) => "", patternWhitespace), // Skip whitespace
            ((_, _) => "", patternNewline), // Skip newlines (lines still counted)
            ((_, _) => "string", patternString),
            ((_, _) => "operator", patternOperators),
            ((_, _) => "number", patternInteger),
            ((_, _) => "identifier", patternIdentifier)
        };

        _tokenizer = new TokenizeGenerator(_rules); // Will be re-instantiated in some tests if needed
    }

    [TestMethod]
    public void TestBasicTokenize()
    {
        var code = "foo = bar";
        var tokens = _tokenizer!.Tokenize(code);

        Assert.HasCount(4, tokens); // foo, =, bar, eof

        Assert.AreEqual("foo", tokens[0].Value);
        Assert.AreEqual("identifier", tokens[0].Name);
        Assert.AreEqual(1, tokens[0].Line);
        Assert.AreEqual(1, tokens[0].Column);

        Assert.AreEqual("=", tokens[1].Value);
        Assert.AreEqual("operator", tokens[1].Name);
        Assert.AreEqual(1, tokens[1].Line);
        Assert.AreEqual(5, tokens[1].Column);

        Assert.AreEqual("bar", tokens[2].Value);
        Assert.AreEqual("identifier", tokens[2].Name);

        Assert.AreEqual("eof", tokens[3].Name);
    }

    [TestMethod]
    public void TestLongestMatch()
    {
        // Should match "==" not "=" twice
        var code = "==";
        var tokens = _tokenizer!.Tokenize(code);

        Assert.HasCount(2, tokens); // ==, eof
        Assert.AreEqual("==", tokens[0].Value);
        Assert.AreEqual("operator", tokens[0].Name);
    }

    [TestMethod]
    public void TestPrefixMismatchFallback()
    {
        // Tests Trie backtracking/fallback logic implicitly
        // patternOperators has "++" and "+"
        // input "+++" -> "++", "+"
        var code = "+++";
        var tokens = _tokenizer!.Tokenize(code);

        Assert.HasCount(3, tokens); // ++, +, eof
        Assert.AreEqual("++", tokens[0].Value);
        Assert.AreEqual("+", tokens[1].Value);
    }

    [TestMethod]
    public void TestStringLiteral()
    {
        var code = "'hello world' \"another string\"";
        var tokens = _tokenizer!.Tokenize(code);

        Assert.HasCount(3, tokens); // string, string, eof
        Assert.AreEqual("'hello world'", tokens[0].Value);
        Assert.AreEqual("\"another string\"", tokens[1].Value);
    }

    [TestMethod]
    public void TestStringEscape()
    {
        var code = "'hello \\'world\\''";
        var tokens = _tokenizer!.Tokenize(code);

        Assert.HasCount(2, tokens);
        Assert.AreEqual("'hello \\'world\\''", tokens[0].Value);
    }

    [TestMethod]
    public void TestUnterminatedString()
    {
        try
        {
            _tokenizer!.Tokenize("'open string");
            Assert.Fail("Should have thrown TokenizeException for unterminated string");
        }
        catch (TokenizeException ex)
        {
            Assert.Contains("Unterminated string literal", ex.Message);
        }
    }

    [TestMethod]
    public void TestMultilineStringError()
    {
        try
        {
            _tokenizer!.Tokenize("'multi\nline'");
            Assert.Fail("Should have thrown TokenizeException for multiline string");
        }
        catch (TokenizeException ex)
        {
            Assert.Contains("String literal must be on a single line", ex.Message);
            Assert.AreEqual(1, ex.Line);
        }
    }

    [TestMethod]
    public void TestUnexpectedToken()
    {
        try
        {
            _tokenizer!.Tokenize("unknown");
            Assert.Fail("Should have thrown TokenizeException for unknown token");
        }
        catch (TokenizeException ex)
        {
            Assert.Contains("Unexpected token", ex.Message);
            Assert.Contains("'u'", ex.Message);
            Assert.AreEqual(1, ex.Line);
            Assert.AreEqual(1, ex.Column);
        }
    }

    [TestMethod]
    public void TestLineTracking()
    {
        var code = "foo\nbar\n=";
        var tokens = _tokenizer!.Tokenize(code);

        Assert.HasCount(4, tokens); // foo, bar, =, eof

        Assert.AreEqual(1, tokens[0].Line);
        Assert.AreEqual(2, tokens[1].Line);
        Assert.AreEqual(3, tokens[2].Line);
        Assert.AreEqual(1, tokens[2].Column);
    }

    [TestMethod]
    public void TestEmptyInput()
    {
        var tokens = _tokenizer!.Tokenize("");
        Assert.HasCount(1, tokens);
        Assert.AreEqual("eof", tokens[0].Name);
    }

    [TestMethod]
    public void TestComments()
    {
        var commentPattern = TokenizeGenerator.CommentPattern("//");
        var customRules = new List<(Func<string, ITokenize, string>, Func<ITokenize, string>)>
        {
            ((_, _) => "", commentPattern), // Skip comments
             ((_, _) => "", TokenizeGenerator.CharsPattern([' ', '\n'])), // Skip whitespace
            ((_, _) => "id", TokenizeGenerator.ValuePattern("a"))
        };
        var tokenizer = new TokenizeGenerator(customRules);

        var code = "a // comment here\na";
        var tokens = tokenizer.Tokenize(code);

        Assert.HasCount(3, tokens); // a, a, eof (comments skipped)
        Assert.AreEqual("a", tokens[0].Value);
        Assert.AreEqual("a", tokens[1].Value); // Second 'a' on new line
    }

    [TestMethod]
    public void TestCommentConsumesUntilNewline()
    {
        var commentPattern = TokenizeGenerator.CommentPattern("//");
        // Need a rule to consume newline if comment doesn't
        var newlinePattern = TokenizeGenerator.CharsPattern(['\n']);

        var customRules = new List<(Func<string, ITokenize, string>, Func<ITokenize, string>)>
        {
            ((s, t) => "comment", commentPattern),
            ((s, t) => "newline", newlinePattern),
            ((s, t) => "id", TokenizeGenerator.ValuePattern("x"))
        };
        var tokenizer = new TokenizeGenerator(customRules);

        var code = "//comment\nx";
        var tokens = tokenizer.Tokenize(code);

        Assert.HasCount(3, tokens); // comment, newline, id, eof
        Assert.AreEqual("//comment", tokens[0].Value);
        Assert.AreEqual("x", tokens[1].Value);
    }

    // Comprehensive tests merged

    [TestMethod]
    public void TestValuesPatternTrieOverlappingPrefixes()
    {
        var patterns = new[] { "a", "ab", "abc", "abd" };

        var rule = TokenizeGenerator.ValuesPattern(patterns);
        var specs = new List<(Func<string, ITokenize, string>, Func<ITokenize, string>)>
        {
            ((_, _) => "id", rule),
            ((_, _) => "", TokenizeGenerator.CharsPattern([' ']))
        };
        var tokenizer = new TokenizeGenerator(specs);
        var tokens = tokenizer.Tokenize("abc abd ab a");

        Assert.HasCount(5, tokens);
        Assert.AreEqual("abc", tokens[0].Value);
        Assert.AreEqual("abd", tokens[1].Value);
        Assert.AreEqual("ab", tokens[2].Value);
        Assert.AreEqual("a", tokens[3].Value);
    }

    [TestMethod]
    public void TestValuesPatternTrieNoMatchBacktracking()
    {
        var patterns = new[] { "apple", "apply" };
        var rule = TokenizeGenerator.ValuesPattern(patterns);
        var specs = new List<(Func<string, ITokenize, string>, Func<ITokenize, string>)>
        {
            ((_, _) => "id", rule)
        };
        var tokenizer = new TokenizeGenerator(specs);

        try
        {
            tokenizer.Tokenize("applz");
            Assert.Fail("Should throw unexpected token exception");
        }
        catch (TokenizeException ex)
        {
            Assert.AreEqual(0, ex.StartIndex);
            Assert.AreEqual(1, ex.Line);
            Assert.AreEqual(1, ex.Column);
            Assert.Contains("Unexpected token", ex.Message, "Message was: " + ex.Message);
        }
    }

    [TestMethod]
    public void TestValuesPatternTriePartialMatchBacktracking()
    {
        var patterns = new[] { "apple" };
        var appleRule = TokenizeGenerator.ValuesPattern(patterns);
        var appRule = TokenizeGenerator.ValuePattern("app");

        var specs = new List<(Func<string, ITokenize, string>, Func<ITokenize, string>)>
        {
            ((_, _) => "apple_rule", appleRule),
            ((_, _) => "app_rule", appRule)
        };
        var tokenizer = new TokenizeGenerator(specs);

        var tokens = tokenizer.Tokenize("app");

        Assert.HasCount(2, tokens);
        Assert.AreEqual("app", tokens[0].Value);
        Assert.AreEqual("app_rule", tokens[0].Name);
    }

    [TestMethod]
    public void TestNumberPatternAllVariations()
    {
        var integerRule = TokenizeGenerator.NumberPattern(false);
        var floatRule = TokenizeGenerator.NumberPattern(true);
        var wsRule = TokenizeGenerator.CharsPattern([' ']);

        var specs = new List<(Func<string, ITokenize, string>, Func<ITokenize, string>)>
        {
            ((_, _) => "", wsRule),
            ((_, _) => "float", floatRule),
            ((_, _) => "integer", integerRule)
        };

        var tokenizer = new TokenizeGenerator(specs);

        var code = "123 456.789 0.1 0";
        var tokens = tokenizer.Tokenize(code);

        Assert.AreEqual("123", tokens[0].Value);
        Assert.AreEqual("float", tokens[0].Name);

        Assert.AreEqual("456.789", tokens[1].Value); // ws skipped

        Assert.AreEqual("0.1", tokens[2].Value);

        Assert.AreEqual("0", tokens[3].Value);
    }

    [TestMethod]
    public void TestCommentsEdgeCases()
    {
        var commentRule = TokenizeGenerator.CommentPattern("//");
        var idRule = TokenizeGenerator.ValuePattern("a");
        var wsRule = TokenizeGenerator.CharsPattern([' ', '\n']);

        var specs = new List<(Func<string, ITokenize, string>, Func<ITokenize, string>)>
        {
            ((_, _) => "comment", commentRule),
            ((_, _) => "ws", wsRule),
            ((_, _) => "id", idRule)
        };
        var tokenizer = new TokenizeGenerator(specs);

        // Case 1: Comment at EOF without newline
        var tokens1 = tokenizer.Tokenize("a // comment");
        // tokens: "id"(a), "ws"( ), "comment"(// comment), "eof"
        Assert.HasCount(4, tokens1);

        Assert.AreEqual("a", tokens1[0].Value);
        Assert.AreEqual("// comment", tokens1[2].Value); // Should capture until EOF
        Assert.AreEqual("eof", tokens1[3].Name);

        // Case 2: Empty comment
        var tokens2 = tokenizer.Tokenize("//");
        Assert.HasCount(2, tokens2); // comment, eof
        Assert.AreEqual("//", tokens2[0].Value);
        Assert.AreEqual("eof", tokens2[1].Name);
    }

    [TestMethod]
    public void TestStringEscapesAndEmpty()
    {
        var stringRule = TokenizeGenerator.StringPattern();
        var specs = new List<(Func<string, ITokenize, string>, Func<ITokenize, string>)>
        {
            ((_, _) => "str", stringRule),
            ((_, _) => "ws", TokenizeGenerator.CharsPattern(['\t', ' ']))
        };
        var tokenizer = new TokenizeGenerator(specs);

        var code = "\"\" \"a\" \"\\'\" \"\\\\\"";
        var tokens = tokenizer.Tokenize(code);

        Assert.AreEqual("\"\"", tokens[0].Value); // Escape sequence
        Assert.AreEqual("ws", tokens[1].Name); // empty string
        Assert.AreEqual("\"a\"", tokens[2].Value); // Normal string
        Assert.AreEqual("\"\\'\"", tokens[4].Value); // String with escaped quote

        var tokens2 = tokenizer.Tokenize("\"\\\"\""); // Represents string: "\""
        Assert.AreEqual("\"\\\"\"", tokens2[0].Value);
    }

    [TestMethod]
    public void TestPositionAfterMultilineToken()
    {
        Func<ITokenize, string> multilinePattern = (t) =>
        {
            if (t.Next() == '@')
            {
                var sb = new System.Text.StringBuilder("@");
                while (true)
                {
                    var ch = t.Next();
                    if (ch == null) break;
                    sb.Append(ch);
                    if (ch == ';') break; // End on semicolon
                }
                return sb.ToString();
            }
            return "";
        };

        var specs = new List<(Func<string, ITokenize, string>, Func<ITokenize, string>)>
        {
            ((_, _) => "block", multilinePattern),
            ((_, _) => "", TokenizeGenerator.CharsPattern(['\n'])), // Skip newlines between tokens
            ((_, _) => "id", TokenizeGenerator.ValuePattern("end"))
        };
        var tokenizer = new TokenizeGenerator(specs);

        var code = "@\n\n;\nend";

        var tokens = tokenizer.Tokenize(code);

        Assert.AreEqual("@\n\n;", tokens[0].Value);
        Assert.AreEqual(1, tokens[0].Line);

        Assert.AreEqual("end", tokens[1].Value);
        Assert.AreEqual(4, tokens[1].Line); // Should be line 4
        Assert.AreEqual(1, tokens[1].Column);
    }

    [TestMethod]
    public void TestValuesPatternTrieOrdering()
    {
        // Ensure longest match wins even with prefix overlap
        var patterns = new[] { "apple", "app", "application" };
        var valuesPattern = TokenizeGenerator.ValuesPattern(patterns);

        var customRules = new List<(Func<string, ITokenize, string>, Func<ITokenize, string>)>
        {
             ((_, _) => "word", valuesPattern),
             ((_, _) => "", TokenizeGenerator.CharsPattern([' ']))
        };
        var tokenizer = new TokenizeGenerator(customRules);

        var code = "application apple app";
        var tokens = tokenizer.Tokenize(code);

        Assert.HasCount(4, tokens);
        Assert.AreEqual("application", tokens[0].Value);
        Assert.AreEqual("apple", tokens[1].Value);
        Assert.AreEqual("app", tokens[2].Value);
    }

    [TestMethod]
    public void TestGetPositionLogic()
    {
        var idPattern = TokenizeGenerator.ValuePattern("abc");
        var nlPattern = TokenizeGenerator.CharsPattern(['\n']);
        var defPattern = TokenizeGenerator.ValuePattern("def");

        var customRules = new List<(Func<string, ITokenize, string>, Func<ITokenize, string>)>
        {
            ((_, _) => "id", idPattern),
            ((_, _) => "nl", nlPattern),
            ((_, _) => "id", defPattern),
        };

        var tokenizer = new TokenizeGenerator(customRules);
        var tokens = tokenizer.Tokenize("abc\ndef");

        Assert.AreEqual(2, tokens[2].Line); // def
        Assert.AreEqual(1, tokens[2].Column); // d starts at col 1 of line 2
    }

    [TestMethod]
    public void TestDiagnosticOutput()
    {
        // Check if PrintDiagnostic correctly formats the error message
        var code = "foo bar\nbaz qux";


        var ex = new TokenizeException("Test error", code, 0, 1, 2, 5);

        var diagnostic = ex.PrintDiagnostic();

        var lines = diagnostic.Split('\n');
        Assert.Contains("error: Test error at 2:5.", lines[0]);
        Assert.Contains("--> line:2:5", lines[1]);
        Assert.Contains("   |", lines[2]);
        Assert.Contains(" 2 | baz qux", lines[3]);
        Assert.Contains("   |     ^ Test error", lines[4]);

        // Test line number formatting for large line numbers
        var spanCode = "";
        for (int i = 1; i <= 10000; i++)
        {
            spanCode += $"Line {i}\n";
        }
        var spanEx = new TokenizeException("Span error", spanCode, 0, 1, 9999, 5);
        var spanDiagnostic = spanEx.PrintDiagnostic();
        var spanLines = spanDiagnostic.Split('\n');
        Assert.Contains("  --> line:9999:5", spanLines[1]);
        Assert.Contains("      |", spanLines[2]);
        Assert.Contains(" 9999 | Line 9999", spanLines[3]);
        Assert.Contains("      |     ^ Span error at 9999:5", spanLines[4]);
    }
}
