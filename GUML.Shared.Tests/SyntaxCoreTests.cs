using GUML.Shared.Syntax;
using GUML.Shared.Syntax.Nodes;
using GUML.Shared.Syntax.Nodes.Expressions;

namespace GUML.Shared.Tests;

[TestClass]
public class TextSpanTests
{
    [TestMethod]
    public void Constructor_SetsProperties()
    {
        var span = new TextSpan(5, 10);
        Assert.AreEqual(5, span.Start);
        Assert.AreEqual(10, span.Length);
        Assert.AreEqual(15, span.End);
    }

    [TestMethod]
    public void FromBounds_CreatesCorrectSpan()
    {
        var span = TextSpan.FromBounds(3, 7);
        Assert.AreEqual(3, span.Start);
        Assert.AreEqual(4, span.Length);
        Assert.AreEqual(7, span.End);
    }

    [TestMethod]
    public void Contains_PositionInside()
    {
        var span = new TextSpan(5, 10);
        Assert.IsTrue(span.Contains(5));
        Assert.IsTrue(span.Contains(10));
        Assert.IsTrue(span.Contains(14));
    }

    [TestMethod]
    public void Contains_PositionOutside()
    {
        var span = new TextSpan(5, 10);
        Assert.IsFalse(span.Contains(4));
        Assert.IsFalse(span.Contains(15)); // End is exclusive
    }

    [TestMethod]
    public void OverlapsWith_Overlapping()
    {
        var a = new TextSpan(5, 10); // [5..15)
        var b = new TextSpan(10, 10); // [10..20)
        Assert.IsTrue(a.OverlapsWith(b));
        Assert.IsTrue(b.OverlapsWith(a));
    }

    [TestMethod]
    public void OverlapsWith_Adjacent_NoOverlap()
    {
        var a = new TextSpan(0, 5); // [0..5)
        var b = new TextSpan(5, 5); // [5..10)
        Assert.IsFalse(a.OverlapsWith(b));
    }

    [TestMethod]
    public void OverlapsWith_NonOverlapping()
    {
        var a = new TextSpan(0, 3);
        var b = new TextSpan(10, 3);
        Assert.IsFalse(a.OverlapsWith(b));
    }

    [TestMethod]
    public void Equality_EqualSpans()
    {
        var a = new TextSpan(1, 5);
        var b = new TextSpan(1, 5);
        Assert.AreEqual(a, b);
        Assert.IsTrue(a == b);
        Assert.IsFalse(a != b);
    }

    [TestMethod]
    public void Equality_DifferentSpans()
    {
        var a = new TextSpan(1, 5);
        var b = new TextSpan(1, 6);
        Assert.AreNotEqual(a, b);
        Assert.IsFalse(a == b);
        Assert.IsTrue(a != b);
    }

    [TestMethod]
    public void ToString_Format()
    {
        var span = new TextSpan(3, 5);
        Assert.AreEqual("[3..8)", span.ToString());
    }

    [TestMethod]
    public void ZeroLengthSpan()
    {
        var span = new TextSpan(10, 0);
        Assert.AreEqual(10, span.Start);
        Assert.AreEqual(0, span.Length);
        Assert.AreEqual(10, span.End);
        Assert.IsFalse(span.Contains(10)); // Zero-length span contains nothing
    }
}

[TestClass]
public class SyntaxTriviaTests
{
    [TestMethod]
    public void Trivia_Properties()
    {
        var trivia = new SyntaxTrivia(SyntaxKind.WhitespaceTrivia, "   ");
        Assert.AreEqual(SyntaxKind.WhitespaceTrivia, trivia.Kind);
        Assert.AreEqual("   ", trivia.Text);
        Assert.AreEqual(3, trivia.FullWidth);
    }

    [TestMethod]
    public void TriviaList_Empty()
    {
        var list = SyntaxTriviaList.Empty;
        Assert.AreEqual(0, list.Count);
        Assert.AreEqual(0, list.FullWidth);
        Assert.AreEqual("", list.ToString());
    }

    [TestMethod]
    public void TriviaList_WithItems()
    {
        var list = new SyntaxTriviaList([
            new SyntaxTrivia(SyntaxKind.WhitespaceTrivia, "  "),
            new SyntaxTrivia(SyntaxKind.SingleLineCommentTrivia, "// hi")
        ]);
        Assert.AreEqual(2, list.Count);
        Assert.AreEqual(7, list.FullWidth);
        Assert.AreEqual("  // hi", list.ToString());
    }
}

[TestClass]
public class SyntaxTokenTests
{
    [TestMethod]
    public void Token_BasicProperties()
    {
        var token = new SyntaxToken(
            SyntaxKind.IdentifierToken, "hello",
            SyntaxTriviaList.Empty, SyntaxTriviaList.Empty);
        Assert.AreEqual(SyntaxKind.IdentifierToken, token.Kind);
        Assert.AreEqual("hello", token.Text);
        Assert.AreEqual(5, token.Width);
        Assert.AreEqual(5, token.FullWidth);
        Assert.IsFalse(token.IsMissing);
    }

    [TestMethod]
    public void Token_WithTrivia()
    {
        var leading = new SyntaxTriviaList([new SyntaxTrivia(SyntaxKind.WhitespaceTrivia, "  ")]);
        var trailing = new SyntaxTriviaList([new SyntaxTrivia(SyntaxKind.WhitespaceTrivia, " ")]);
        var token = new SyntaxToken(SyntaxKind.IdentifierToken, "x", leading, trailing);
        Assert.AreEqual(1, token.Width);
        Assert.AreEqual(4, token.FullWidth); // 2 + 1 + 1
    }

    [TestMethod]
    public void Token_Missing()
    {
        var token = SyntaxToken.Missing(SyntaxKind.CloseBraceToken);
        Assert.IsTrue(token.IsMissing);
        Assert.AreEqual("", token.Text);
        Assert.AreEqual(0, token.Width);
        Assert.AreEqual(0, token.FullWidth);
        Assert.AreEqual(SyntaxKind.CloseBraceToken, token.Kind);
    }

    [TestMethod]
    public void Token_ToFullString()
    {
        var leading = new SyntaxTriviaList([new SyntaxTrivia(SyntaxKind.WhitespaceTrivia, "  ")]);
        var trailing = new SyntaxTriviaList([new SyntaxTrivia(SyntaxKind.EndOfLineTrivia, "\n")]);
        var token = new SyntaxToken(SyntaxKind.IdentifierToken, "foo", leading, trailing);
        Assert.AreEqual("  foo\n", token.ToFullString());
    }

    [TestMethod]
    public void Token_ToString_ReturnsText()
    {
        var token = new SyntaxToken(SyntaxKind.IdentifierToken, "bar",
            SyntaxTriviaList.Empty, SyntaxTriviaList.Empty);
        Assert.AreEqual("bar", token.ToString());
    }
}

[TestClass]
public class SyntaxNodeOrTokenTests
{
    [TestMethod]
    public void NodeWrapper()
    {
        // Use a real parsed node
        var result = GumlSyntaxTree.Parse("Label { }");
        var root = result.Root;
        SyntaxNodeOrToken wrapper = root;
        Assert.IsTrue(wrapper.IsNode);
        Assert.IsFalse(wrapper.IsToken);
        Assert.AreSame(root, wrapper.AsNode());
    }

    [TestMethod]
    public void TokenWrapper()
    {
        var token = new SyntaxToken(SyntaxKind.IdentifierToken, "test",
            SyntaxTriviaList.Empty, SyntaxTriviaList.Empty);
        SyntaxNodeOrToken wrapper = token;
        Assert.IsFalse(wrapper.IsNode);
        Assert.IsTrue(wrapper.IsToken);
        Assert.AreSame(token, wrapper.AsToken());
    }

    [TestMethod]
    public void AsNode_ThrowsForToken()
    {
        var token = new SyntaxToken(SyntaxKind.IdentifierToken, "test",
            SyntaxTriviaList.Empty, SyntaxTriviaList.Empty);
        SyntaxNodeOrToken wrapper = token;
        Assert.ThrowsExactly<InvalidOperationException>(wrapper.AsNode);
    }

    [TestMethod]
    public void AsToken_ThrowsForNode()
    {
        var result = GumlSyntaxTree.Parse("Label { }");
        SyntaxNodeOrToken wrapper = result.Root;
        Assert.ThrowsExactly<InvalidOperationException>(wrapper.AsToken);
    }
}

[TestClass]
public class DiagnosticTests
{
    [TestMethod]
    public void Diagnostic_Properties()
    {
        var span = new TextSpan(10, 5);
        var diag = new Diagnostic("GUML0001", "Test error", DiagnosticSeverity.Error, span);
        Assert.AreEqual("GUML0001", diag.Id);
        Assert.AreEqual("Test error", diag.Message);
        Assert.AreEqual(DiagnosticSeverity.Error, diag.Severity);
        Assert.AreEqual(span, diag.Span);
    }

    [TestMethod]
    public void Diagnostic_ToString()
    {
        var diag = new Diagnostic("GUML0002", "Bad things", DiagnosticSeverity.Warning, new TextSpan(5, 3));
        string str = diag.ToString();
        Assert.IsTrue(str.Contains("GUML0002"));
        Assert.IsTrue(str.Contains("Bad things"));
    }
}

[TestClass]
public class TextChangeTests
{
    [TestMethod]
    public void TextChange_Properties()
    {
        var change = new TextChange(10, 3, "hello");
        Assert.AreEqual(10, change.Start);
        Assert.AreEqual(3, change.OldLength);
        Assert.AreEqual("hello", change.NewText);
    }
}

// ================================================================
// SyntaxList<T> tests
// ================================================================

[TestClass]
public class SyntaxListTests
{
    [TestMethod]
    public void Empty_HasCountZero()
    {
        var list = SyntaxList<SyntaxNode>.Empty;
        Assert.AreEqual(0, list.Count);
    }

    [TestMethod]
    public void ConstructFromArray_CountAndIndexer()
    {
        // Build a list from a parsed document's imports
        var result = GumlSyntaxTree.Parse("import \"a.guml\"\nimport \"b.guml\"\nPanel { }");
        var imports = result.Root.Imports;
        Assert.AreEqual(2, imports.Count);
        Assert.AreEqual("\"a.guml\"", imports[0].Path.Text);
        Assert.AreEqual("\"b.guml\"", imports[1].Path.Text);
    }

    [TestMethod]
    public void ConstructFromList_EquivalentToArray()
    {
        var result = GumlSyntaxTree.Parse("import \"a.guml\"\nimport \"b.guml\"\nPanel { }");
        var imports = result.Root.Imports;
        // Enumerate to collect items
        var enumerated = new List<ImportDirectiveSyntax>();
        foreach (var imp in imports)
            enumerated.Add(imp);
        Assert.AreEqual(2, enumerated.Count);
    }

    [TestMethod]
    public void Enumeration_VisitsAllItems()
    {
        var result = GumlSyntaxTree.Parse("Label {\n    text: \"a\",\n    visible: true,\n    x: 1\n}");
        var members = result.Root.RootComponent.Members;
        Assert.AreEqual(3, members.Count);
        int count = 0;
        foreach (var _ in members)
            count++;
        Assert.AreEqual(3, count);
    }

    [TestMethod]
    public void Empty_Enumeration_YieldsNothing()
    {
        var list = SyntaxList<SyntaxNode>.Empty;
        int count = 0;
        foreach (var _ in list)
            count++;
        Assert.AreEqual(0, count);
    }
}

// ================================================================
// SyntaxTokenList tests
// ================================================================

[TestClass]
public class SyntaxTokenListTests
{
    [TestMethod]
    public void Empty_HasCountZero()
    {
        var list = SyntaxTokenList.Empty;
        Assert.AreEqual(0, list.Count);
    }

    [TestMethod]
    public void WithItems_CountAndIndexer()
    {
        var t1 = new SyntaxToken(SyntaxKind.IdentifierToken, "a",
            SyntaxTriviaList.Empty, SyntaxTriviaList.Empty);
        var t2 = new SyntaxToken(SyntaxKind.IdentifierToken, "b",
            SyntaxTriviaList.Empty, SyntaxTriviaList.Empty);
        var list = new SyntaxTokenList([t1, t2]);
        Assert.AreEqual(2, list.Count);
        Assert.AreEqual("a", list[0].Text);
        Assert.AreEqual("b", list[1].Text);
    }

    [TestMethod]
    public void Enumeration_VisitsAllTokens()
    {
        var t1 = new SyntaxToken(SyntaxKind.IdentifierToken, "x",
            SyntaxTriviaList.Empty, SyntaxTriviaList.Empty);
        var t2 = new SyntaxToken(SyntaxKind.IntegerLiteralToken, "42",
            SyntaxTriviaList.Empty, SyntaxTriviaList.Empty);
        var list = new SyntaxTokenList([t1, t2]);
        var collected = new List<string>();
        foreach (var t in list)
            collected.Add(t.Text);
        CollectionAssert.AreEqual(new[] { "x", "42" }, collected);
    }

    [TestMethod]
    public void Empty_Enumeration_YieldsNothing()
    {
        var list = SyntaxTokenList.Empty;
        int count = 0;
        foreach (var _ in list)
            count++;
        Assert.AreEqual(0, count);
    }
}

// ================================================================
// SeparatedSyntaxList<T> tests
// ================================================================

[TestClass]
public class SeparatedSyntaxListTests
{
    /// <summary>
    /// Helper: parse a struct expression and obtain its PositionalArgs separated list.
    /// e.g. "Panel { pos: Vector2(1, 2, 3) }"
    /// </summary>
    private static SeparatedSyntaxList<ExpressionSyntax> ParseStructArgs(string guml)
    {
        var result = GumlSyntaxTree.Parse(guml);
        var prop = (PropertyAssignmentSyntax)result.Root.RootComponent.Members[0];
        var structExpr = (StructExpressionSyntax)prop.Value;
        return structExpr.PositionalArgs!;
    }

    [TestMethod]
    public void Empty_HasZeroCounts()
    {
        var list = SeparatedSyntaxList<ExpressionSyntax>.Empty;
        Assert.AreEqual(0, list.Count);
        Assert.AreEqual(0, list.SeparatorCount);
        Assert.AreEqual(0, list.FullWidth);
    }

    [TestMethod]
    public void SingleItem_NoSeparators()
    {
        // vec2(5) has 1 arg and 0 separators
        var args = ParseStructArgs("Panel { pos: Vector2(5) }");
        Assert.AreEqual(1, args.Count);
        Assert.AreEqual(0, args.SeparatorCount);
    }

    [TestMethod]
    public void MultipleItems_SeparatorCount()
    {
        // vec2(1, 2) has 2 args and 1 separator
        var args = ParseStructArgs("Panel { pos: Vector2(1, 2) }");
        Assert.AreEqual(2, args.Count);
        Assert.AreEqual(1, args.SeparatorCount);
        Assert.AreEqual(SyntaxKind.CommaToken, args.GetSeparator(0).Kind);
    }

    [TestMethod]
    public void ThreeItems_TwoSeparators()
    {
        var args = ParseStructArgs("Panel { pos: Vector3(1, 2, 3) }");
        Assert.AreEqual(3, args.Count);
        Assert.AreEqual(2, args.SeparatorCount);
        Assert.AreEqual(",", args.GetSeparator(0).Text);
        Assert.AreEqual(",", args.GetSeparator(1).Text);
    }

    [TestMethod]
    public void Indexer_ReturnsCorrectNode()
    {
        var args = ParseStructArgs("Panel { pos: Vector2(10, 20) }");
        var first = (LiteralExpressionSyntax)args[0];
        var second = (LiteralExpressionSyntax)args[1];
        Assert.AreEqual("10", first.Token.Text);
        Assert.AreEqual("20", second.Token.Text);
    }

    [TestMethod]
    public void FullWidth_IncludesNodesAndSeparators()
    {
        var args = ParseStructArgs("Panel { pos: Vector2(1, 2) }");
        // The FullWidth should be > 0 and include the tokens and whitespace
        Assert.IsTrue(args.FullWidth > 0);
    }

    [TestMethod]
    public void ChildNodesAndTokens_InterleavesSeparators()
    {
        var args = ParseStructArgs("Panel { pos: Vector2(1, 2, 3) }");
        var children = args.ChildNodesAndTokens().ToList();
        // Expected: node, sep, node, sep, node => 5 items
        Assert.AreEqual(5, children.Count);
        Assert.IsTrue(children[0].IsNode);
        Assert.IsTrue(children[1].IsToken);
        Assert.IsTrue(children[2].IsNode);
        Assert.IsTrue(children[3].IsToken);
        Assert.IsTrue(children[4].IsNode);
    }

    [TestMethod]
    public void Enumeration_SkipsSeparators()
    {
        var args = ParseStructArgs("Panel { pos: Vector2(10, 20, 30) }");
        var nodes = new List<ExpressionSyntax>();
        foreach (var n in args)
            nodes.Add(n);
        Assert.AreEqual(3, nodes.Count);
        // All should be nodes (expressions), not separators
        foreach (var n in nodes)
            Assert.IsInstanceOfType(n, typeof(LiteralExpressionSyntax));
    }

    [TestMethod]
    public void Empty_ChildNodesAndTokens_YieldsNothing()
    {
        var list = SeparatedSyntaxList<ExpressionSyntax>.Empty;
        Assert.AreEqual(0, list.ChildNodesAndTokens().Count());
    }
}
