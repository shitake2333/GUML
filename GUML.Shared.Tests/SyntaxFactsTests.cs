using GUML.Shared.Syntax;

namespace GUML.Shared.Tests;

[TestClass]
public class SyntaxFactsTests
{
    // ================================================================
    // GetKeywordKind
    // ================================================================

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
    public void GetKeywordKind_ReturnsCorrectKind(string text, SyntaxKind expected)
    {
        Assert.AreEqual(expected, SyntaxFacts.GetKeywordKind(text));
    }

    [TestMethod]
    [DataRow("myVar")]
    [DataRow("Button")]
    [DataRow("IMPORT")]
    [DataRow("")]
    public void GetKeywordKind_ReturnsNone_ForNonKeywords(string text)
    {
        Assert.AreEqual(SyntaxKind.None, SyntaxFacts.GetKeywordKind(text));
    }

    // ================================================================
    // IsKeyword
    // ================================================================

    [TestMethod]
    public void IsKeyword_TrueForAllKeywords()
    {
        Assert.IsTrue(SyntaxFacts.IsKeyword(SyntaxKind.ImportKeyword));
        Assert.IsTrue(SyntaxFacts.IsKeyword(SyntaxKind.AsKeyword));
        Assert.IsTrue(SyntaxFacts.IsKeyword(SyntaxKind.ParamKeyword));
        Assert.IsTrue(SyntaxFacts.IsKeyword(SyntaxKind.EventKeyword));
        Assert.IsTrue(SyntaxFacts.IsKeyword(SyntaxKind.EachKeyword));
        Assert.IsTrue(SyntaxFacts.IsKeyword(SyntaxKind.NewKeyword));
        Assert.IsTrue(SyntaxFacts.IsKeyword(SyntaxKind.ImageKeyword));
        Assert.IsTrue(SyntaxFacts.IsKeyword(SyntaxKind.FontKeyword));
        Assert.IsTrue(SyntaxFacts.IsKeyword(SyntaxKind.AudioKeyword));
        Assert.IsTrue(SyntaxFacts.IsKeyword(SyntaxKind.VideoKeyword));
    }

    [TestMethod]
    public void IsKeyword_FalseForNonKeywords()
    {
        Assert.IsFalse(SyntaxFacts.IsKeyword(SyntaxKind.IdentifierToken));
        Assert.IsFalse(SyntaxFacts.IsKeyword(SyntaxKind.TrueLiteralToken));
        Assert.IsFalse(SyntaxFacts.IsKeyword(SyntaxKind.OpenBraceToken));
    }

    // ================================================================
    // IsLiteral
    // ================================================================

    [TestMethod]
    public void IsLiteral_TrueForLiterals()
    {
        Assert.IsTrue(SyntaxFacts.IsLiteral(SyntaxKind.StringLiteralToken));
        Assert.IsTrue(SyntaxFacts.IsLiteral(SyntaxKind.TemplateStringLiteralToken));
        Assert.IsTrue(SyntaxFacts.IsLiteral(SyntaxKind.IntegerLiteralToken));
        Assert.IsTrue(SyntaxFacts.IsLiteral(SyntaxKind.FloatLiteralToken));
        Assert.IsTrue(SyntaxFacts.IsLiteral(SyntaxKind.TrueLiteralToken));
        Assert.IsTrue(SyntaxFacts.IsLiteral(SyntaxKind.FalseLiteralToken));
        Assert.IsTrue(SyntaxFacts.IsLiteral(SyntaxKind.NullLiteralToken));
    }

    [TestMethod]
    public void IsLiteral_FalseForNonLiterals()
    {
        Assert.IsFalse(SyntaxFacts.IsLiteral(SyntaxKind.IdentifierToken));
        Assert.IsFalse(SyntaxFacts.IsLiteral(SyntaxKind.ImportKeyword));
    }

    // ================================================================
    // IsResourceKeyword
    // ================================================================

    [TestMethod]
    public void IsResourceKeyword_TrueForResources()
    {
        Assert.IsTrue(SyntaxFacts.IsResourceKeyword(SyntaxKind.ImageKeyword));
        Assert.IsTrue(SyntaxFacts.IsResourceKeyword(SyntaxKind.FontKeyword));
        Assert.IsTrue(SyntaxFacts.IsResourceKeyword(SyntaxKind.AudioKeyword));
        Assert.IsTrue(SyntaxFacts.IsResourceKeyword(SyntaxKind.VideoKeyword));
    }

    [TestMethod]
    public void IsResourceKeyword_FalseForOthers()
    {
        Assert.IsFalse(SyntaxFacts.IsResourceKeyword(SyntaxKind.ImportKeyword));
        Assert.IsFalse(SyntaxFacts.IsResourceKeyword(SyntaxKind.IdentifierToken));
    }

    // ================================================================
    // IsMappingOperator
    // ================================================================

    [TestMethod]
    public void IsMappingOperator_TrueForMappings()
    {
        Assert.IsTrue(SyntaxFacts.IsMappingOperator(SyntaxKind.MapToPropertyToken));
        Assert.IsTrue(SyntaxFacts.IsMappingOperator(SyntaxKind.MapToDataToken));
        Assert.IsTrue(SyntaxFacts.IsMappingOperator(SyntaxKind.MapTwoWayToken));
    }

    [TestMethod]
    public void IsMappingOperator_FalseForOthers()
    {
        Assert.IsFalse(SyntaxFacts.IsMappingOperator(SyntaxKind.ColonToken));
        Assert.IsFalse(SyntaxFacts.IsMappingOperator(SyntaxKind.EqualsEqualsToken));
    }

    // ================================================================
    // IsPrefixUnaryOperator
    // ================================================================

    [TestMethod]
    public void IsPrefixUnaryOperator_TrueForUnaryOps()
    {
        Assert.IsTrue(SyntaxFacts.IsPrefixUnaryOperator(SyntaxKind.BangToken));
        Assert.IsTrue(SyntaxFacts.IsPrefixUnaryOperator(SyntaxKind.PlusToken));
        Assert.IsTrue(SyntaxFacts.IsPrefixUnaryOperator(SyntaxKind.MinusToken));
    }

    // ================================================================
    // GetBinaryOperatorPrecedence
    // ================================================================

    [TestMethod]
    public void Precedence_MultiplicativeIsHighest()
    {
        Assert.AreEqual(60, SyntaxFacts.GetBinaryOperatorPrecedence(SyntaxKind.AsteriskToken));
        Assert.AreEqual(60, SyntaxFacts.GetBinaryOperatorPrecedence(SyntaxKind.SlashToken));
        Assert.AreEqual(60, SyntaxFacts.GetBinaryOperatorPrecedence(SyntaxKind.PercentToken));
    }

    [TestMethod]
    public void Precedence_AdditiveIsLowerThanMultiplicative()
    {
        Assert.AreEqual(50, SyntaxFacts.GetBinaryOperatorPrecedence(SyntaxKind.PlusToken));
        Assert.AreEqual(50, SyntaxFacts.GetBinaryOperatorPrecedence(SyntaxKind.MinusToken));
    }

    [TestMethod]
    public void Precedence_Relational()
    {
        Assert.AreEqual(40, SyntaxFacts.GetBinaryOperatorPrecedence(SyntaxKind.LessThanToken));
        Assert.AreEqual(40, SyntaxFacts.GetBinaryOperatorPrecedence(SyntaxKind.GreaterThanToken));
        Assert.AreEqual(40, SyntaxFacts.GetBinaryOperatorPrecedence(SyntaxKind.LessThanEqualsToken));
        Assert.AreEqual(40, SyntaxFacts.GetBinaryOperatorPrecedence(SyntaxKind.GreaterThanEqualsToken));
    }

    [TestMethod]
    public void Precedence_Equality()
    {
        Assert.AreEqual(30, SyntaxFacts.GetBinaryOperatorPrecedence(SyntaxKind.EqualsEqualsToken));
        Assert.AreEqual(30, SyntaxFacts.GetBinaryOperatorPrecedence(SyntaxKind.BangEqualsToken));
    }

    [TestMethod]
    public void Precedence_LogicalAnd()
    {
        Assert.AreEqual(20, SyntaxFacts.GetBinaryOperatorPrecedence(SyntaxKind.AmpersandAmpersandToken));
    }

    [TestMethod]
    public void Precedence_LogicalOrIsLowest()
    {
        Assert.AreEqual(10, SyntaxFacts.GetBinaryOperatorPrecedence(SyntaxKind.BarBarToken));
    }

    [TestMethod]
    public void Precedence_NonOperator_ReturnsZero()
    {
        Assert.AreEqual(0, SyntaxFacts.GetBinaryOperatorPrecedence(SyntaxKind.ColonToken));
        Assert.AreEqual(0, SyntaxFacts.GetBinaryOperatorPrecedence(SyntaxKind.IdentifierToken));
    }

    // ================================================================
    // IsBinaryOperator
    // ================================================================

    [TestMethod]
    public void IsBinaryOperator_TrueForAllBinaryOps()
    {
        Assert.IsTrue(SyntaxFacts.IsBinaryOperator(SyntaxKind.PlusToken));
        Assert.IsTrue(SyntaxFacts.IsBinaryOperator(SyntaxKind.AsteriskToken));
        Assert.IsTrue(SyntaxFacts.IsBinaryOperator(SyntaxKind.EqualsEqualsToken));
        Assert.IsTrue(SyntaxFacts.IsBinaryOperator(SyntaxKind.BarBarToken));
    }

    [TestMethod]
    public void IsBinaryOperator_FalseForNonBinaryOps()
    {
        Assert.IsFalse(SyntaxFacts.IsBinaryOperator(SyntaxKind.BangToken));
        Assert.IsFalse(SyntaxFacts.IsBinaryOperator(SyntaxKind.ColonToken));
    }

    // ================================================================
    // GetText
    // ================================================================

    [TestMethod]
    public void GetText_Punctuators()
    {
        Assert.AreEqual("{", SyntaxFacts.GetText(SyntaxKind.OpenBraceToken));
        Assert.AreEqual("}", SyntaxFacts.GetText(SyntaxKind.CloseBraceToken));
        Assert.AreEqual("(", SyntaxFacts.GetText(SyntaxKind.OpenParenToken));
        Assert.AreEqual(")", SyntaxFacts.GetText(SyntaxKind.CloseParenToken));
        Assert.AreEqual("[", SyntaxFacts.GetText(SyntaxKind.OpenBracketToken));
        Assert.AreEqual("]", SyntaxFacts.GetText(SyntaxKind.CloseBracketToken));
        Assert.AreEqual(",", SyntaxFacts.GetText(SyntaxKind.CommaToken));
        Assert.AreEqual(".", SyntaxFacts.GetText(SyntaxKind.DotToken));
        Assert.AreEqual(":", SyntaxFacts.GetText(SyntaxKind.ColonToken));
        Assert.AreEqual("|", SyntaxFacts.GetText(SyntaxKind.PipeToken));
        Assert.AreEqual("?", SyntaxFacts.GetText(SyntaxKind.QuestionToken));
        Assert.AreEqual("=>", SyntaxFacts.GetText(SyntaxKind.FatArrowToken));
        Assert.AreEqual(":=", SyntaxFacts.GetText(SyntaxKind.MapToPropertyToken));
        Assert.AreEqual("=:", SyntaxFacts.GetText(SyntaxKind.MapToDataToken));
        Assert.AreEqual("<=>", SyntaxFacts.GetText(SyntaxKind.MapTwoWayToken));
    }

    [TestMethod]
    public void GetText_Operators()
    {
        Assert.AreEqual("+", SyntaxFacts.GetText(SyntaxKind.PlusToken));
        Assert.AreEqual("-", SyntaxFacts.GetText(SyntaxKind.MinusToken));
        Assert.AreEqual("*", SyntaxFacts.GetText(SyntaxKind.AsteriskToken));
        Assert.AreEqual("==", SyntaxFacts.GetText(SyntaxKind.EqualsEqualsToken));
        Assert.AreEqual("!=", SyntaxFacts.GetText(SyntaxKind.BangEqualsToken));
        Assert.AreEqual("&&", SyntaxFacts.GetText(SyntaxKind.AmpersandAmpersandToken));
        Assert.AreEqual("||", SyntaxFacts.GetText(SyntaxKind.BarBarToken));
    }

    [TestMethod]
    public void GetText_Keywords()
    {
        Assert.AreEqual("import", SyntaxFacts.GetText(SyntaxKind.ImportKeyword));
        Assert.AreEqual("each", SyntaxFacts.GetText(SyntaxKind.EachKeyword));
        Assert.AreEqual("true", SyntaxFacts.GetText(SyntaxKind.TrueLiteralToken));
    }

    [TestMethod]
    public void GetText_ReturnsNull_ForDynamicTokens()
    {
        Assert.IsNull(SyntaxFacts.GetText(SyntaxKind.IdentifierToken));
        Assert.IsNull(SyntaxFacts.GetText(SyntaxKind.StringLiteralToken));
        Assert.IsNull(SyntaxFacts.GetText(SyntaxKind.IntegerLiteralToken));
    }

    // ================================================================
    // CanStartExpression
    // ================================================================

    [TestMethod]
    public void CanStartExpression_TrueForExpressionStarters()
    {
        Assert.IsTrue(SyntaxFacts.CanStartExpression(SyntaxKind.StringLiteralToken));
        Assert.IsTrue(SyntaxFacts.CanStartExpression(SyntaxKind.IntegerLiteralToken));
        Assert.IsTrue(SyntaxFacts.CanStartExpression(SyntaxKind.IdentifierToken));
        Assert.IsTrue(SyntaxFacts.CanStartExpression(SyntaxKind.GlobalRefToken));
        Assert.IsTrue(SyntaxFacts.CanStartExpression(SyntaxKind.AliasRefToken));
        Assert.IsTrue(SyntaxFacts.CanStartExpression(SyntaxKind.BangToken));
        Assert.IsTrue(SyntaxFacts.CanStartExpression(SyntaxKind.MinusToken));
        Assert.IsTrue(SyntaxFacts.CanStartExpression(SyntaxKind.OpenParenToken));
        Assert.IsTrue(SyntaxFacts.CanStartExpression(SyntaxKind.NewKeyword));
        Assert.IsTrue(SyntaxFacts.CanStartExpression(SyntaxKind.ImageKeyword));
        Assert.IsTrue(SyntaxFacts.CanStartExpression(SyntaxKind.EnumValueToken));
        Assert.IsTrue(SyntaxFacts.CanStartExpression(SyntaxKind.ComponentNameToken));
    }

    [TestMethod]
    public void CanStartExpression_FalseForNonExpressionTokens()
    {
        Assert.IsFalse(SyntaxFacts.CanStartExpression(SyntaxKind.CloseBraceToken));
        Assert.IsFalse(SyntaxFacts.CanStartExpression(SyntaxKind.ColonToken));
        Assert.IsFalse(SyntaxFacts.CanStartExpression(SyntaxKind.EndOfFileToken));
    }

    // ================================================================
    // CanStartMember
    // ================================================================

    [TestMethod]
    public void CanStartMember_TrueForMemberStarters()
    {
        Assert.IsTrue(SyntaxFacts.CanStartMember(SyntaxKind.IdentifierToken));
        Assert.IsTrue(SyntaxFacts.CanStartMember(SyntaxKind.EventRefToken));
        Assert.IsTrue(SyntaxFacts.CanStartMember(SyntaxKind.ParamKeyword));
        Assert.IsTrue(SyntaxFacts.CanStartMember(SyntaxKind.EventKeyword));
        Assert.IsTrue(SyntaxFacts.CanStartMember(SyntaxKind.EachKeyword));
        Assert.IsTrue(SyntaxFacts.CanStartMember(SyntaxKind.ComponentNameToken));
        Assert.IsTrue(SyntaxFacts.CanStartMember(SyntaxKind.AliasRefToken));
    }

    [TestMethod]
    public void CanStartMember_FalseForNonMemberTokens()
    {
        Assert.IsFalse(SyntaxFacts.CanStartMember(SyntaxKind.StringLiteralToken));
        Assert.IsFalse(SyntaxFacts.CanStartMember(SyntaxKind.OpenBraceToken));
        Assert.IsFalse(SyntaxFacts.CanStartMember(SyntaxKind.EndOfFileToken));
    }

    // ================================================================
    // IsTrivia
    // ================================================================

    [TestMethod]
    public void IsTrivia_TrueForTriviaKinds()
    {
        Assert.IsTrue(SyntaxFacts.IsTrivia(SyntaxKind.WhitespaceTrivia));
        Assert.IsTrue(SyntaxFacts.IsTrivia(SyntaxKind.EndOfLineTrivia));
        Assert.IsTrue(SyntaxFacts.IsTrivia(SyntaxKind.SingleLineCommentTrivia));
        Assert.IsTrue(SyntaxFacts.IsTrivia(SyntaxKind.DocumentationCommentTrivia));
        Assert.IsTrue(SyntaxFacts.IsTrivia(SyntaxKind.SkippedTokensTrivia));
    }

    [TestMethod]
    public void IsTrivia_FalseForNonTrivia()
    {
        Assert.IsFalse(SyntaxFacts.IsTrivia(SyntaxKind.IdentifierToken));
        Assert.IsFalse(SyntaxFacts.IsTrivia(SyntaxKind.OpenBraceToken));
    }
}
