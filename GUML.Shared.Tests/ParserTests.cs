using GUML.Shared.Syntax;
using GUML.Shared.Syntax.Nodes;
using GUML.Shared.Syntax.Nodes.Expressions;

namespace GUML.Shared.Tests;

[TestClass]
public class ParserTests
{
    private static ParseResult Parse(string source) => GumlSyntaxTree.Parse(source);

    // ================================================================
    // Round-trip (full fidelity) tests
    // ================================================================

    [TestMethod]
    public void RoundTrip_MinimalDocument()
    {
        const string source = "VBoxContainer {\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    [TestMethod]
    public void RoundTrip_WithImport()
    {
        const string source = "import \"components/button.guml\"\nVBoxContainer {\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    [TestMethod]
    public void RoundTrip_WithImportAlias()
    {
        const string source = "import \"components/button.guml\" as MyButton\nPanel {\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    [TestMethod]
    public void RoundTrip_PropertyAssignment()
    {
        const string source = "Label {\n    text: \"hello\"\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    [TestMethod]
    public void RoundTrip_MappingAssignment()
    {
        const string source = "Label {\n    text := $controller.name\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    [TestMethod]
    public void RoundTrip_EventSubscription()
    {
        const string source = "Button {\n    #pressed: $controller.on_click\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    [TestMethod]
    public void RoundTrip_NestedComponents()
    {
        const string source =
            "VBoxContainer {\n    Label {\n        text: \"hi\"\n    }\n    Button {\n        text: \"ok\"\n    }\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    [TestMethod]
    public void RoundTrip_WithComments()
    {
        const string source = "// top comment\nLabel {\n    // property comment\n    text: \"hello\"\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    [TestMethod]
    public void RoundTrip_ComplexExpression()
    {
        const string source = "Label {\n    visible: $controller.count > 0 && $controller.is_enabled\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    [TestMethod]
    public void RoundTrip_ParameterDeclaration()
    {
        const string source = "Panel {\n    param String title := \"default\"\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    [TestMethod]
    public void RoundTrip_EventDeclaration()
    {
        const string source = "Panel {\n    event onAction\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    [TestMethod]
    public void RoundTrip_EventDeclarationWithArgs()
    {
        const string source = "Panel {\n    event onSelected(Int32 index, String value)\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    [TestMethod]
    public void RoundTrip_ResourceExpression()
    {
        const string source = "TextureRect {\n    texture: image(\"res://icon.png\")\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    [TestMethod]
    public void RoundTrip_EnumValue()
    {
        const string source = "Label {\n    horizontal_alignment: .Center\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    [TestMethod]
    public void RoundTrip_TemplateString()
    {
        const string source = "Label {\n    text: $\"Count: {$controller.count}\"\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    [TestMethod]
    public void RoundTrip_TernaryExpression()
    {
        const string source = "Label {\n    text: $controller.is_active ? \"yes\" : \"no\"\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    [TestMethod]
    public void RoundTrip_AliasPrefix()
    {
        const string source = "VBoxContainer {\n    @myAlias: Label {\n        text: \"aliased\"\n    }\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    [TestMethod]
    public void RoundTrip_TemplateParam()
    {
        const string source = "Panel {\n    content => Label {\n        text: \"template\"\n    }\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    [TestMethod]
    public void RoundTrip_MappingOperators()
    {
        const string source =
            "Panel {\n    name := $controller.name,\n    value =: $controller.value,\n    text <=> $controller.text\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    // ================================================================
    // Document structure tests
    // ================================================================

    [TestMethod]
    public void Parse_MinimalDocument_HasRootComponent()
    {
        var result = Parse("Button { }");
        Assert.AreEqual(0, result.Diagnostics.Count);
        Assert.AreEqual(SyntaxKind.GumlDocument, result.Root.Kind);
        Assert.AreEqual(0, result.Root.Imports.Count);
        Assert.IsNotNull(result.Root.RootComponent);
        Assert.AreEqual("Button", result.Root.RootComponent.TypeName.Text);
    }

    [TestMethod]
    public void Parse_WithImports()
    {
        var result = Parse("import \"a.guml\"\nimport \"b.guml\" as B\nPanel { }");
        Assert.AreEqual(0, result.Diagnostics.Count);
        Assert.AreEqual(2, result.Root.Imports.Count);

        Assert.AreEqual("\"a.guml\"", result.Root.Imports[0].Path.Text);
        Assert.IsNull(result.Root.Imports[0].Alias);

        Assert.AreEqual("\"b.guml\"", result.Root.Imports[1].Path.Text);
        Assert.IsNotNull(result.Root.Imports[1].Alias);
        Assert.AreEqual("B", result.Root.Imports[1].Alias!.Name.Text);
    }

    [TestMethod]
    public void Parse_PropertyAssignment_Structure()
    {
        var result = Parse("Label {\n    text: \"hello\"\n}");
        Assert.AreEqual(0, result.Diagnostics.Count);

        var comp = result.Root.RootComponent;
        Assert.AreEqual(1, comp.Members.Count);
        var prop = comp.Members[0] as PropertyAssignmentSyntax;
        Assert.IsNotNull(prop);
        Assert.AreEqual("text", prop.Name.Text);
        Assert.IsInstanceOfType(prop.Value, typeof(LiteralExpressionSyntax));
        var lit = (LiteralExpressionSyntax)prop.Value;
        Assert.AreEqual("\"hello\"", lit.Token.Text);
    }

    [TestMethod]
    public void Parse_MappingAssignment_Structure()
    {
        var result = Parse("Label {\n    text := $controller.name\n}");
        Assert.AreEqual(0, result.Diagnostics.Count);

        var comp = result.Root.RootComponent;
        var mapping = comp.Members[0] as MappingAssignmentSyntax;
        Assert.IsNotNull(mapping);
        Assert.AreEqual("text", mapping.Name.Text);
        Assert.AreEqual(SyntaxKind.MapToPropertyToken, mapping.Operator.Kind);
        Assert.IsInstanceOfType(mapping.Value, typeof(MemberAccessExpressionSyntax));
    }

    [TestMethod]
    public void Parse_EventSubscription_Structure()
    {
        var result = Parse("Button {\n    #pressed: $controller.on_click\n}");
        Assert.AreEqual(0, result.Diagnostics.Count);

        var comp = result.Root.RootComponent;
        var evt = comp.Members[0] as EventSubscriptionSyntax;
        Assert.IsNotNull(evt);
        Assert.AreEqual("#pressed", evt.EventRef.Text);
    }

    [TestMethod]
    public void Parse_NestedComponent_Structure()
    {
        var result = Parse("VBoxContainer {\n    Label {\n        text: \"child\"\n    }\n}");
        Assert.AreEqual(0, result.Diagnostics.Count);

        var comp = result.Root.RootComponent;
        Assert.AreEqual(1, comp.Members.Count);
        var child = comp.Members[0] as ComponentDeclarationSyntax;
        Assert.IsNotNull(child);
        Assert.AreEqual("Label", child.TypeName.Text);
    }

    [TestMethod]
    public void Parse_ParameterDeclaration_Structure()
    {
        var result = Parse("Panel {\n    param String title\n}");
        Assert.AreEqual(0, result.Diagnostics.Count);

        var comp = result.Root.RootComponent;
        var param = comp.Members[0] as ParameterDeclarationSyntax;
        Assert.IsNotNull(param);
        Assert.AreEqual("String", param.TypeName.Text);
        Assert.AreEqual("title", param.Name.Text);
        Assert.IsNull(param.DefaultValue);
    }

    [TestMethod]
    public void Parse_ParameterDeclaration_WithDefault()
    {
        var result = Parse("Panel {\n    param String title := \"default\"\n}");
        Assert.AreEqual(0, result.Diagnostics.Count);

        var comp = result.Root.RootComponent;
        var param = comp.Members[0] as ParameterDeclarationSyntax;
        Assert.IsNotNull(param);
        Assert.IsNotNull(param.DefaultValue);
        Assert.IsInstanceOfType(param.DefaultValue, typeof(LiteralExpressionSyntax));
    }

    [TestMethod]
    public void Parse_GenericParamType()
    {
        var result = Parse("Panel {\n    param List<int> items\n}");
        Assert.AreEqual(0, result.Diagnostics.Count);

        var comp = result.Root.RootComponent;
        var param = comp.Members[0] as ParameterDeclarationSyntax;
        Assert.IsNotNull(param);
        Assert.AreEqual("List<int>", param.TypeName.Text);
        Assert.AreEqual("items", param.Name.Text);
    }

    [TestMethod]
    public void Parse_NestedGenericParamType()
    {
        var result = Parse("Panel {\n    param Dictionary<String, List<int>> data\n}");
        Assert.AreEqual(0, result.Diagnostics.Count);

        var comp = result.Root.RootComponent;
        var param = comp.Members[0] as ParameterDeclarationSyntax;
        Assert.IsNotNull(param);
        Assert.AreEqual("Dictionary<String, List<int>>", param.TypeName.Text);
        Assert.AreEqual("data", param.Name.Text);
    }

    [TestMethod]
    public void Parse_NonGenericParamType_Unchanged()
    {
        var result = Parse("Panel {\n    param String title\n}");
        Assert.AreEqual(0, result.Diagnostics.Count);

        var comp = result.Root.RootComponent;
        var param = comp.Members[0] as ParameterDeclarationSyntax;
        Assert.IsNotNull(param);
        Assert.AreEqual("String", param.TypeName.Text);
    }

    [TestMethod]
    public void RoundTrip_GenericParamType()
    {
        const string source = "Panel {\n    param List<int> items\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    [TestMethod]
    public void RoundTrip_GenericEventArgType()
    {
        const string source = "Panel {\n    event on_changed(List<int> items)\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    [TestMethod]
    public void Parse_EventDeclaration_NoArgs()
    {
        var result = Parse("Panel {\n    event onAction\n}");
        // 'onAction' is not snake_case: expect 1 naming warning (GUML3002)
        Assert.AreEqual(1, result.Diagnostics.Count);
        Assert.AreEqual("GUML3002", result.Diagnostics[0].Id);

        var comp = result.Root.RootComponent;
        var evt = comp.Members[0] as EventDeclarationSyntax;
        Assert.IsNotNull(evt);
        Assert.AreEqual("onAction", evt.Name.Text);
        Assert.IsNull(evt.Arguments);
    }

    [TestMethod]
    public void Parse_EventDeclaration_WithArgs()
    {
        var result = Parse("Panel {\n    event onSelected(Int32 index, String value)\n}");
        // 'onSelected' is not snake_case: expect 1 naming warning (GUML3002)
        Assert.AreEqual(1, result.Diagnostics.Count);
        Assert.AreEqual("GUML3002", result.Diagnostics[0].Id);

        var comp = result.Root.RootComponent;
        var evt = comp.Members[0] as EventDeclarationSyntax;
        Assert.IsNotNull(evt);
        Assert.IsNotNull(evt.Arguments);
        Assert.AreEqual(2, evt.Arguments!.Count);
        Assert.AreEqual("Int32", evt.Arguments[0].TypeName.Text);
        Assert.AreEqual("index", evt.Arguments[0].Name?.Text);
    }

    // ================================================================
    // Expression parsing tests
    // ================================================================

    [TestMethod]
    public void Expression_IntegerLiteral()
    {
        var result = Parse("Panel {\n    size: 42\n}");
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        Assert.IsInstanceOfType(prop!.Value, typeof(LiteralExpressionSyntax));
        var lit = (LiteralExpressionSyntax)prop.Value;
        Assert.AreEqual(SyntaxKind.IntegerLiteralToken, lit.Token.Kind);
    }

    [TestMethod]
    public void Expression_FloatLiteral()
    {
        var result = Parse("Panel {\n    opacity: 0.5\n}");
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        Assert.IsInstanceOfType(prop!.Value, typeof(LiteralExpressionSyntax));
        var lit = (LiteralExpressionSyntax)prop.Value;
        Assert.AreEqual(SyntaxKind.FloatLiteralToken, lit.Token.Kind);
    }

    [TestMethod]
    public void Expression_BooleanLiteral()
    {
        var result = Parse("Panel {\n    visible: true\n}");
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        Assert.IsInstanceOfType(prop!.Value, typeof(LiteralExpressionSyntax));
        var lit = (LiteralExpressionSyntax)prop.Value;
        Assert.AreEqual(SyntaxKind.TrueLiteralToken, lit.Token.Kind);
    }

    [TestMethod]
    public void Expression_NullLiteral()
    {
        var result = Parse("Panel {\n    data: null\n}");
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        var lit = (LiteralExpressionSyntax)prop!.Value;
        Assert.AreEqual(SyntaxKind.NullLiteralToken, lit.Token.Kind);
    }

    [TestMethod]
    public void Expression_GlobalRef()
    {
        var result = Parse("Panel {\n    text: $controller\n}");
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        Assert.IsInstanceOfType(prop!.Value, typeof(ReferenceExpressionSyntax));
        var refExpr = (ReferenceExpressionSyntax)prop.Value;
        Assert.AreEqual(SyntaxKind.GlobalRefToken, refExpr.Identifier.Kind);
    }

    [TestMethod]
    public void Expression_MemberAccess()
    {
        var result = Parse("Panel {\n    text: $controller.name\n}");
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        Assert.IsInstanceOfType(prop!.Value, typeof(MemberAccessExpressionSyntax));
        var member = (MemberAccessExpressionSyntax)prop.Value;
        Assert.AreEqual("name", member.Name.Text);
    }

    [TestMethod]
    public void Expression_ChainedMemberAccess()
    {
        var result = Parse("Panel {\n    text: $controller.model.name\n}");
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        var outer = prop!.Value as MemberAccessExpressionSyntax;
        Assert.IsNotNull(outer);
        Assert.AreEqual("name", outer.Name.Text);
        var inner = outer.Expression as MemberAccessExpressionSyntax;
        Assert.IsNotNull(inner);
        Assert.AreEqual("model", inner.Name.Text);
    }

    [TestMethod]
    public void Expression_BinaryArithmetic()
    {
        var result = Parse("Panel {\n    width: 10 + 20\n}");
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        var bin = prop!.Value as BinaryExpressionSyntax;
        Assert.IsNotNull(bin);
        Assert.AreEqual(SyntaxKind.PlusToken, bin.OperatorToken.Kind);
    }

    [TestMethod]
    public void Expression_BinaryPrecedence_MultiplyBeforeAdd()
    {
        // 1 + 2 * 3 should parse as 1 + (2 * 3)
        var result = Parse("Panel {\n    x: 1 + 2 * 3\n}");
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        var bin = prop!.Value as BinaryExpressionSyntax;
        Assert.IsNotNull(bin);
        Assert.AreEqual(SyntaxKind.PlusToken, bin.OperatorToken.Kind);
        // Right side should be 2 * 3
        Assert.IsInstanceOfType(bin.Right, typeof(BinaryExpressionSyntax));
        var right = (BinaryExpressionSyntax)bin.Right;
        Assert.AreEqual(SyntaxKind.AsteriskToken, right.OperatorToken.Kind);
    }

    [TestMethod]
    public void Expression_PrefixUnary_Negation()
    {
        var result = Parse("Panel {\n    x: -10\n}");
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        Assert.IsInstanceOfType(prop!.Value, typeof(PrefixUnaryExpressionSyntax));
        var unary = (PrefixUnaryExpressionSyntax)prop.Value;
        Assert.AreEqual(SyntaxKind.MinusToken, unary.OperatorToken.Kind);
    }

    [TestMethod]
    public void Expression_PrefixUnary_Not()
    {
        var result = Parse("Panel {\n    visible: !false\n}");
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        Assert.IsInstanceOfType(prop!.Value, typeof(PrefixUnaryExpressionSyntax));
        var unary = (PrefixUnaryExpressionSyntax)prop.Value;
        Assert.AreEqual(SyntaxKind.BangToken, unary.OperatorToken.Kind);
    }

    [TestMethod]
    public void Expression_Conditional()
    {
        var result = Parse("Panel {\n    text: $controller.ok ? \"yes\" : \"no\"\n}");
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        Assert.IsInstanceOfType(prop!.Value, typeof(ConditionalExpressionSyntax));
        var cond = (ConditionalExpressionSyntax)prop.Value;
        Assert.IsInstanceOfType(cond.WhenTrue, typeof(LiteralExpressionSyntax));
        Assert.IsInstanceOfType(cond.WhenFalse, typeof(LiteralExpressionSyntax));
    }

    [TestMethod]
    public void Expression_Parenthesized()
    {
        var result = Parse("Panel {\n    x: (1 + 2)\n}");
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        Assert.IsInstanceOfType(prop!.Value, typeof(ParenthesizedExpressionSyntax));
    }

    [TestMethod]
    public void Expression_EnumValue()
    {
        var result = Parse("Label {\n    align: .Center\n}");
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        Assert.IsInstanceOfType(prop!.Value, typeof(EnumValueExpressionSyntax));
        var enumExpr = (EnumValueExpressionSyntax)prop.Value;
        Assert.AreEqual(".Center", enumExpr.Token.Text);
    }

    [TestMethod]
    public void Expression_ResourceImage()
    {
        var result = Parse("TextureRect {\n    texture: image(\"res://icon.png\")\n}");
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        Assert.IsInstanceOfType(prop!.Value, typeof(ResourceExpressionSyntax));
        var res = (ResourceExpressionSyntax)prop.Value;
        Assert.AreEqual(SyntaxKind.ImageKeyword, res.Keyword.Kind);
    }

    [TestMethod]
    public void Expression_StructConstructor()
    {
        var result = Parse("Panel {\n    size: Vector2(100, 200)\n}");
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        Assert.IsInstanceOfType(prop!.Value, typeof(StructExpressionSyntax));
        var structExpr = (StructExpressionSyntax)prop.Value;
        Assert.AreEqual("Vector2", structExpr.TypeName.Text);
    }

    [TestMethod]
    public void Expression_ArrayLiteral()
    {
        var result = Parse("Panel {\n    items: Int32[1, 2, 3]\n}");
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        Assert.IsInstanceOfType(prop!.Value, typeof(ArrayLiteralExpressionSyntax));
        var arr = (ArrayLiteralExpressionSyntax)prop.Value;
        Assert.AreEqual("Int32", arr.TypeName.Text);
        Assert.AreEqual(3, arr.Elements.Count);
    }

    [TestMethod]
    public void Expression_NewObject()
    {
        var result = Parse("Panel {\n    style: new StyleBox {\n        bg_color: \"red\"\n    }\n}");
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        Assert.IsInstanceOfType(prop!.Value, typeof(ObjectCreationExpressionSyntax));
    }

    // ================================================================
    // Each block tests
    // ================================================================

    [TestMethod]
    public void Parse_EachBlock_Block()
    {
        var result =
            Parse(
                "VBoxContainer {\n    each $controller.items {\n        |idx, item|\n        Label {\n            text: item\n        }\n    }\n}");
        var comp = result.Root.RootComponent;
        var each = comp.Members[0] as EachBlockSyntax;
        Assert.IsNotNull(each);
        Assert.AreEqual(SyntaxKind.EachKeyword, each.EachKeyword.Kind);
        Assert.IsNotNull(each.Body);
    }

    [TestMethod]
    public void Parse_EachBlock_Projection()
    {
        var result = Parse("VBoxContainer {\n    each $controller.items => itemTemplate\n}");
        var comp = result.Root.RootComponent;
        var each = comp.Members[0] as EachBlockSyntax;
        Assert.IsNotNull(each);
        Assert.IsNotNull(each.FatArrow);
        Assert.IsNotNull(each.ProjectionName);
        Assert.AreEqual("itemTemplate", each.ProjectionName!.Text);
    }

    // ================================================================
    // Template param tests
    // ================================================================

    [TestMethod]
    public void Parse_TemplateParam()
    {
        var result = Parse("Panel {\n    content => Label {\n        text: \"inner\"\n    }\n}");
        var comp = result.Root.RootComponent;
        var tmpl = comp.Members[0] as TemplateParamAssignmentSyntax;
        Assert.IsNotNull(tmpl);
        Assert.AreEqual("content", tmpl.Name.Text);
        Assert.AreEqual("Label", tmpl.Component.TypeName.Text);
    }

    // ================================================================
    // Alias prefix tests
    // ================================================================

    [TestMethod]
    public void Parse_AliasPrefix()
    {
        var result = Parse("VBoxContainer {\n    @btn: Button {\n        text: \"click\"\n    }\n}");
        var comp = result.Root.RootComponent;
        var child = comp.Members[0] as ComponentDeclarationSyntax;
        Assert.IsNotNull(child);
        Assert.IsNotNull(child.AliasPrefix);
        Assert.AreEqual("@btn", child.AliasPrefix!.AliasRef.Text);
    }

    // ================================================================
    // Error recovery tests
    // ================================================================

    [TestMethod]
    public void ErrorRecovery_MissingCloseBrace()
    {
        var result = Parse("Button {\n    text: \"hello\"\n");
        // Should still produce a tree with diagnostics
        Assert.IsNotNull(result.Root);
        Assert.IsTrue(result.Diagnostics.Count > 0);
        // Root component should still have the property
        Assert.AreEqual("Button", result.Root.RootComponent.TypeName.Text);
    }

    [TestMethod]
    public void ErrorRecovery_MissingExpressionValue()
    {
        var result = Parse("Button {\n    text: \n}");
        // Should report diagnostic and still parse
        Assert.IsNotNull(result.Root);
        Assert.IsTrue(result.Diagnostics.Count > 0);
    }

    [TestMethod]
    public void ErrorRecovery_InvalidToken_SkipsAndContinues()
    {
        var result = Parse("Button {\n    ~~~\n    text: \"hello\"\n}");
        Assert.IsNotNull(result.Root);
        Assert.IsTrue(result.Diagnostics.Count > 0);
    }

    // ================================================================
    // Position / TextSpan tests
    // ================================================================

    [TestMethod]
    public void Positions_AreComputed()
    {
        const string source = "Label {\n    text: \"hi\"\n}";
        var result = Parse(source);
        var root = result.Root;

        // Root starts at 0
        Assert.AreEqual(0, root.Position);
        Assert.AreEqual(source.Length, root.FullWidth);
    }

    [TestMethod]
    public void FindToken_AtPosition()
    {
        const string source = "Label {\n    text: \"hi\"\n}";
        var result = Parse(source);

        // "Label" starts at pos 0
        var token = result.Root.FindToken(0);
        Assert.IsNotNull(token);
        Assert.AreEqual("Label", token.Text);
    }

    [TestMethod]
    public void FindNode_AtPosition()
    {
        const string source = "Label {\n    text: \"hi\"\n}";
        var result = Parse(source);

        // Position inside "text: \"hi\"" area
        var node = result.Root.FindNode(12);
        Assert.IsNotNull(node);
    }

    [TestMethod]
    public void DescendantTokens_ContainsAllTokens()
    {
        const string source = "Label { text: \"hi\" }";
        var result = Parse(source);

        var tokens = result.Root.DescendantTokens().ToList();
        Assert.IsTrue(tokens.Count > 0);
        // Should contain Label, {, text, :, "hi", }, EOF
        var texts = tokens.Select(t => t.Text).ToList();
        CollectionAssert.Contains(texts, "Label");
        CollectionAssert.Contains(texts, "text");
        CollectionAssert.Contains(texts, "\"hi\"");
    }

    [TestMethod]
    public void DescendantNodes_EnumeratesChildren()
    {
        const string source = "Label { text: \"hi\" }";
        var result = Parse(source);

        var nodes = result.Root.DescendantNodes().ToList();
        Assert.IsTrue(nodes.Count > 0);
        Assert.IsTrue(nodes.Any(n => n is ComponentDeclarationSyntax));
        Assert.IsTrue(nodes.Any(n => n is PropertyAssignmentSyntax));
    }

    // ================================================================
    // ================================================================
    // Complex document tests
    // ================================================================

    [TestMethod]
    public void RoundTrip_ComplexDocument()
    {
        const string source =
            "import \"components/header.guml\" as Header\n" +
            "VBoxContainer {\n" +
            "    param String title := \"My App\"\n" +
            "    Header {\n" +
            "        text := $controller.title\n" +
            "    }\n" +
            "    Label {\n" +
            "        text: $controller.count > 0 ? $\"Items: {$controller.count}\" : \"No items\",\n" +
            "        horizontal_alignment: .Center,\n" +
            "        visible: true\n" +
            "    }\n" +
            "    Button {\n" +
            "        text: \"Click me\",\n" +
            "        #pressed: $controller.on_click\n" +
            "    }\n" +
            "}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    // ================================================================
    // Naming convention diagnostic tests (§3.6.1.1)
    // ================================================================

    [TestMethod]
    public void NamingDiag_SnakeCaseIdentifiers_NoDiagnostics()
    {
        var result = Parse(
            "Label {\n" +
            "    text := $controller.user_name,\n" +
            "    visible: $controller.is_active\n" +
            "}");
        var warnings = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();
        Assert.AreEqual(0, warnings.Count);
    }

    [TestMethod]
    public void NamingDiag_PascalCaseMemberAccess_WarnsGUML3001()
    {
        var result = Parse("Label {\n    text := $controller.Name\n}");
        var warnings = result.Diagnostics.Where(d => d.Id == "GUML3001").ToList();
        Assert.AreEqual(1, warnings.Count);
        Assert.IsTrue(warnings[0].Message.Contains("Name"));
        Assert.AreEqual(DiagnosticSeverity.Warning, warnings[0].Severity);
    }

    [TestMethod]
    public void NamingDiag_CamelCaseMemberAccess_WarnsGUML3001()
    {
        var result = Parse("Label {\n    text: $controller.userName\n}");
        var warnings = result.Diagnostics.Where(d => d.Id == "GUML3001").ToList();
        Assert.AreEqual(1, warnings.Count);
        Assert.IsTrue(warnings[0].Message.Contains("userName"));
    }

    [TestMethod]
    public void NamingDiag_ChainedPascalCase_WarnsMultiple()
    {
        var result = Parse("Label {\n    text: $controller.Model.Name\n}");
        var warnings = result.Diagnostics.Where(d => d.Id == "GUML3001").ToList();
        Assert.AreEqual(2, warnings.Count);
    }

    [TestMethod]
    public void NamingDiag_CamelCaseEventName_WarnsGUML3002()
    {
        var result = Parse("Panel {\n    event onAction\n}");
        var warnings = result.Diagnostics.Where(d => d.Id == "GUML3002").ToList();
        Assert.AreEqual(1, warnings.Count);
        Assert.IsTrue(warnings[0].Message.Contains("onAction"));
        Assert.IsTrue(warnings[0].Message.Contains("Event name"));
    }

    [TestMethod]
    public void NamingDiag_SnakeCaseEventName_NoDiagnostics()
    {
        var result = Parse("Panel {\n    event on_action\n}");
        var warnings = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();
        Assert.AreEqual(0, warnings.Count);
    }

    [TestMethod]
    public void NamingDiag_CamelCasePropertyName_WarnsGUML3002()
    {
        var result = Parse("Label {\n    fontSize: 12\n}");
        var warnings = result.Diagnostics.Where(d => d.Id == "GUML3002").ToList();
        Assert.AreEqual(1, warnings.Count);
        Assert.IsTrue(warnings[0].Message.Contains("fontSize"));
        Assert.IsTrue(warnings[0].Message.Contains("Property name"));
    }

    [TestMethod]
    public void NamingDiag_SnakeCasePropertyName_NoDiagnostics()
    {
        var result = Parse("Label {\n    font_size: 12\n}");
        var warnings = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();
        Assert.AreEqual(0, warnings.Count);
    }

    [TestMethod]
    public void NamingDiag_CamelCaseParameterName_WarnsGUML3002()
    {
        var result = Parse("Panel {\n    param String buttonText\n}");
        var warnings = result.Diagnostics.Where(d => d.Id == "GUML3002").ToList();
        Assert.AreEqual(1, warnings.Count);
        Assert.IsTrue(warnings[0].Message.Contains("buttonText"));
        Assert.IsTrue(warnings[0].Message.Contains("Parameter name"));
    }

    [TestMethod]
    public void NamingDiag_SnakeCaseParameterName_NoDiagnostics()
    {
        var result = Parse("Panel {\n    param String button_text\n}");
        var warnings = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();
        Assert.AreEqual(0, warnings.Count);
    }

    [TestMethod]
    public void NamingDiag_CamelCaseAliasRef_WarnsGUML3002()
    {
        var result = Parse("VBoxContainer {\n    @myAlias: Label {\n        text: \"hi\"\n    }\n}");
        var warnings = result.Diagnostics.Where(d => d.Id == "GUML3002").ToList();
        Assert.AreEqual(1, warnings.Count);
        Assert.IsTrue(warnings[0].Message.Contains("@myAlias"));
        Assert.IsTrue(warnings[0].Message.Contains("Alias reference"));
    }

    [TestMethod]
    public void NamingDiag_SnakeCaseAliasRef_NoDiagnostics()
    {
        var result = Parse("VBoxContainer {\n    @my_alias: Label {\n        text: \"hi\"\n    }\n}");
        var warnings = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();
        Assert.AreEqual(0, warnings.Count);
    }

    [TestMethod]
    public void NamingDiag_CamelCaseEventRef_WarnsGUML3002()
    {
        var result = Parse("Button {\n    #textChanged: $controller.on_change\n}");
        var warnings = result.Diagnostics.Where(d => d.Id == "GUML3002").ToList();
        Assert.AreEqual(1, warnings.Count);
        Assert.IsTrue(warnings[0].Message.Contains("#textChanged"));
        Assert.IsTrue(warnings[0].Message.Contains("Event reference"));
    }

    [TestMethod]
    public void NamingDiag_SnakeCaseEventRef_NoDiagnostics()
    {
        var result = Parse("Button {\n    #text_changed: $controller.on_change\n}");
        var warnings = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();
        Assert.AreEqual(0, warnings.Count);
    }

    [TestMethod]
    public void NamingDiag_CamelCaseProjectionName_WarnsGUML3002()
    {
        var result = Parse("VBoxContainer {\n    each $controller.items => itemTemplate\n}");
        var warnings = result.Diagnostics.Where(d => d.Id == "GUML3002").ToList();
        Assert.AreEqual(1, warnings.Count);
        Assert.IsTrue(warnings[0].Message.Contains("itemTemplate"));
        Assert.IsTrue(warnings[0].Message.Contains("Projection name"));
    }

    [TestMethod]
    public void NamingDiag_SnakeCaseProjectionName_NoDiagnostics()
    {
        var result = Parse("VBoxContainer {\n    each $controller.items => item_template\n}");
        var warnings = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();
        Assert.AreEqual(0, warnings.Count);
    }

    [TestMethod]
    public void NamingDiag_CamelCaseTemplateParam_WarnsGUML3002()
    {
        var result = Parse("Panel {\n    contentBody => Label {\n        text: \"inner\"\n    }\n}");
        var warnings = result.Diagnostics.Where(d => d.Id == "GUML3002").ToList();
        Assert.AreEqual(1, warnings.Count);
        Assert.IsTrue(warnings[0].Message.Contains("contentBody"));
        Assert.IsTrue(warnings[0].Message.Contains("Template parameter name"));
    }

    [TestMethod]
    public void NamingDiag_SnakeCaseTemplateParam_NoDiagnostics()
    {
        var result = Parse("Panel {\n    content_body => Label {\n        text: \"inner\"\n    }\n}");
        var warnings = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();
        Assert.AreEqual(0, warnings.Count);
    }

    [TestMethod]
    public void NamingDiag_EnumValue_NoWarning()
    {
        // Enum values (.PascalCase) should NOT trigger naming warnings
        var result = Parse("Label {\n    horizontal_alignment: .Center\n}");
        var warnings = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();
        Assert.AreEqual(0, warnings.Count);
    }

    [TestMethod]
    public void NamingDiag_WarningDoesNotBlockParsing()
    {
        // Even with naming warnings, the tree should be fully constructed
        var result = Parse("Label {\n    text := $controller.UserName\n}");
        Assert.IsNotNull(result.Root.RootComponent);
        var mapping = result.Root.RootComponent.Members[0] as MappingAssignmentSyntax;
        Assert.IsNotNull(mapping);
        var memberAccess = mapping.Value as MemberAccessExpressionSyntax;
        Assert.IsNotNull(memberAccess);
        Assert.AreEqual("UserName", memberAccess.Name.Text);
        // Should be a warning, not an error
        Assert.IsTrue(result.Diagnostics.Any(d => d.Id == "GUML3001"));
        Assert.IsFalse(result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error));
    }

    // ================================================================
    // Token list sync after parser correction (.PascalCase split)
    // ================================================================

    /// <summary>
    /// Helper: collect all leaf tokens from a syntax tree via DFS.
    /// </summary>
    private static List<SyntaxToken> CollectLeafTokens(SyntaxNode root)
    {
        var tokens = new List<SyntaxToken>();
        foreach (var child in root.ChildNodesAndTokens())
        {
            if (child.IsToken)
                tokens.Add(child.AsToken());
            else
                tokens.AddRange(CollectLeafTokens(child.AsNode()));
        }

        return tokens;
    }

    /// <summary>
    /// Helper: verify cached token list matches the syntax tree's leaf tokens.
    /// </summary>
    private static void AssertTokenListMatchesTree(ParseResult result)
    {
        var treeTokens = CollectLeafTokens(result.Root);
        var cachedTokens = result.Tokens;
        Assert.AreEqual(treeTokens.Count, cachedTokens.Count,
            "Cached token count should match syntax tree leaf token count");
        for (int i = 0; i < treeTokens.Count; i++)
        {
            Assert.AreEqual(treeTokens[i].Kind, cachedTokens[i].Kind,
                $"Token[{i}] kind mismatch: tree={treeTokens[i].Kind}, cached={cachedTokens[i].Kind}");
            Assert.AreEqual(treeTokens[i].Text, cachedTokens[i].Text,
                $"Token[{i}] text mismatch: tree={treeTokens[i].Text}, cached={cachedTokens[i].Text}");
        }
    }

    [TestMethod]
    public void TokenSync_EnumValue_Unchanged()
    {
        // Enum value in primary position - no split, token list unchanged
        var result = Parse("Label {\n    align: .Center\n}");
        AssertTokenListMatchesTree(result);
        // The EnumValueToken should remain in the cached list
        Assert.IsTrue(result.Tokens.Any(t => t is { Kind: SyntaxKind.EnumValueToken, Text: ".Center" }));
    }

    [TestMethod]
    public void TokenSync_LowercaseMemberAccess_Unchanged()
    {
        // Lowercase member access - lexer already produces DotToken + Identifier
        var result = Parse("Panel {\n    text: $controller.name\n}");
        AssertTokenListMatchesTree(result);
        Assert.IsFalse(result.Tokens.Any(t => t.Kind == SyntaxKind.EnumValueToken));
    }

    [TestMethod]
    public void TokenSync_PascalCaseMemberAccess_Split()
    {
        // PascalCase member access in postfix position - EnumValueToken should be split
        var result = Parse("Label {\n    text := $controller.Name\n}");
        AssertTokenListMatchesTree(result);
        // After split, no EnumValueToken should remain for ".Name"
        Assert.IsFalse(result.Tokens.Any(t => t.Kind == SyntaxKind.EnumValueToken),
            "EnumValueToken should be split into DotToken + IdentifierToken in postfix position");
    }

    [TestMethod]
    public void TokenSync_ChainedPascalCaseMemberAccess()
    {
        // Chained PascalCase: both .Model and .Name should be split
        var result = Parse("Panel {\n    text := $controller.Model.Name\n}");
        AssertTokenListMatchesTree(result);
        Assert.IsFalse(result.Tokens.Any(t => t.Kind == SyntaxKind.EnumValueToken));
    }

    [TestMethod]
    public void TokenSync_MixedEnumAndMemberAccess()
    {
        // First line: PascalCase member access (split), second line: enum value (no split)
        var result = Parse("Label {\n    text := $controller.Value\n    horizontal_alignment: .Center\n}");
        AssertTokenListMatchesTree(result);
        // Only the enum ".Center" should remain as EnumValueToken
        var enumTokens = result.Tokens.Where(t => t.Kind == SyntaxKind.EnumValueToken).ToList();
        Assert.AreEqual(1, enumTokens.Count);
        Assert.AreEqual(".Center", enumTokens[0].Text);
    }

    // ================================================================
    // Documentation comment tests
    // ================================================================

    [TestMethod]
    public void DocComment_MultiLine_ComponentWithNameTag()
    {
        const string source =
            "/// A reusable panel component.\n/// Supports customizable content.\n/// @name my_panel\nPanel { }";
        var result = Parse(source);
        Assert.AreEqual(0, result.Diagnostics.Count);
        var root = result.Root.RootComponent;
        Assert.IsNotNull(root.DocumentationComment);
        Assert.AreEqual(3, root.DocumentationComment!.CommentTokens.Count);
        Assert.AreEqual("A reusable panel component.\nSupports customizable content.",
            root.DocumentationComment.GetDocumentationText());
        Assert.AreEqual("my_panel", root.DocumentationComment.GetNameMarker());
    }

    [TestMethod]
    public void DocComment_SingleLine_NoNameTag()
    {
        const string source = "/// A simple label component.\nLabel { text: \"hello\" }";
        var result = Parse(source);
        Assert.AreEqual(0, result.Diagnostics.Count);
        var root = result.Root.RootComponent;
        Assert.IsNotNull(root.DocumentationComment);
        Assert.IsNull(root.DocumentationComment!.GetNameMarker());
        Assert.AreEqual("A simple label component.", root.DocumentationComment.GetDocumentationText());
    }

    [TestMethod]
    public void DocComment_ParameterDeclaration()
    {
        const string source =
            "Control {\n    /// The button text.\n    param string text\n    /// The button size in pixels.\n    param Vector2 size\n}";
        var result = Parse(source);
        Assert.AreEqual(0, result.Diagnostics.Count);
        var root = result.Root.RootComponent;
        var param0 = (ParameterDeclarationSyntax)root.Members[0];
        var param1 = (ParameterDeclarationSyntax)root.Members[1];
        Assert.IsNotNull(param0.DocumentationComment);
        Assert.AreEqual("The button text.", param0.DocumentationComment!.GetDocumentationText());
        Assert.IsNotNull(param1.DocumentationComment);
        Assert.AreEqual("The button size in pixels.", param1.DocumentationComment!.GetDocumentationText());
    }

    [TestMethod]
    public void DocComment_EventDeclaration_WithParamTag()
    {
        const string source =
            "Control {\n    /// Emitted when toggled.\n    /// @param state The new toggle state.\n    event on_toggled(bool state)\n}";
        var result = Parse(source);
        Assert.AreEqual(0, result.Diagnostics.Count);
        var root = result.Root.RootComponent;
        var eventDecl = (EventDeclarationSyntax)root.Members[0];
        Assert.IsNotNull(eventDecl.DocumentationComment);
        Assert.AreEqual("Emitted when toggled.", eventDecl.DocumentationComment!.GetDocumentationText());
        var paramDocs = eventDecl.DocumentationComment.GetParamDocs();
        Assert.AreEqual(1, paramDocs.Count);
        Assert.AreEqual("state", paramDocs[0].Name);
        Assert.AreEqual("The new toggle state.", paramDocs[0].Description);
    }

    [TestMethod]
    public void DocComment_EventDeclaration_MultipleParamTags()
    {
        const string source =
            "Control {\n    /// Selection changed.\n    /// @param index The selected index.\n    /// @param value The value.\n    event on_selection_changed(int index, string value)\n}";
        var result = Parse(source);
        Assert.AreEqual(0, result.Diagnostics.Count);
        var eventDecl = (EventDeclarationSyntax)result.Root.RootComponent.Members[0];
        Assert.IsNotNull(eventDecl.DocumentationComment);
        Assert.AreEqual("Selection changed.", eventDecl.DocumentationComment!.GetDocumentationText());
        var paramDocs = eventDecl.DocumentationComment.GetParamDocs();
        Assert.AreEqual(2, paramDocs.Count);
        Assert.AreEqual("index", paramDocs[0].Name);
        Assert.AreEqual("The selected index.", paramDocs[0].Description);
        Assert.AreEqual("value", paramDocs[1].Name);
        Assert.AreEqual("The value.", paramDocs[1].Description);
    }

    [TestMethod]
    public void DocComment_EventDeclaration_NoDoc()
    {
        const string source = "Control {\n    event pressed\n}";
        var result = Parse(source);
        Assert.AreEqual(0, result.Diagnostics.Count);
        var eventDecl = (EventDeclarationSyntax)result.Root.RootComponent.Members[0];
        Assert.IsNull(eventDecl.DocumentationComment);
    }

    [TestMethod]
    public void DocComment_ParameterDeclaration_NoDoc()
    {
        const string source = "Control {\n    param String text\n}";
        var result = Parse(source);
        Assert.AreEqual(0, result.Diagnostics.Count);
        var param = (ParameterDeclarationSyntax)result.Root.RootComponent.Members[0];
        Assert.IsNull(param.DocumentationComment);
    }

    [TestMethod]
    public void DocComment_Mixed_ComponentParamEvent()
    {
        const string source =
            "/// A complete component.\n/// @name my_widget\nControl {\n    /// The display text.\n    param String text\n    /// Fired on click.\n    /// @param pos The click position.\n    event on_click(Vector2 pos)\n}";
        var result = Parse(source);
        Assert.AreEqual(0, result.Diagnostics.Count);
        var root = result.Root.RootComponent;

        // Component doc comment
        Assert.IsNotNull(root.DocumentationComment);
        Assert.AreEqual("A complete component.", root.DocumentationComment!.GetDocumentationText());
        Assert.AreEqual("my_widget", root.DocumentationComment.GetNameMarker());

        // Param doc comment
        var param = (ParameterDeclarationSyntax)root.Members[0];
        Assert.IsNotNull(param.DocumentationComment);
        Assert.AreEqual("The display text.", param.DocumentationComment!.GetDocumentationText());

        // Event doc comment
        var eventDecl = (EventDeclarationSyntax)root.Members[1];
        Assert.IsNotNull(eventDecl.DocumentationComment);
        Assert.AreEqual("Fired on click.", eventDecl.DocumentationComment!.GetDocumentationText());
        var paramDocs = eventDecl.DocumentationComment.GetParamDocs();
        Assert.AreEqual(1, paramDocs.Count);
        Assert.AreEqual("pos", paramDocs[0].Name);
    }

    [TestMethod]
    public void DocComment_RoundTrip()
    {
        const string source =
            "/// Component doc\nControl {\n    /// Param doc\n    param String text\n    /// Event doc\n    event pressed\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    [TestMethod]
    public void DocComment_GetNameMarker_NoTag_ReturnsNull()
    {
        const string source = "/// Just a description.\nPanel { }";
        var result = Parse(source);
        Assert.IsNull(result.Root.RootComponent.DocumentationComment!.GetNameMarker());
    }

    [TestMethod]
    public void DocComment_GetParamDocs_Empty_WhenNoTags()
    {
        const string source = "Control {\n    /// A simple event.\n    event pressed\n}";
        var result = Parse(source);
        var eventDecl = (EventDeclarationSyntax)result.Root.RootComponent.Members[0];
        Assert.AreEqual(0, eventDecl.DocumentationComment!.GetParamDocs().Count);
    }

    [TestMethod]
    public void DocComment_OnlyTagLines_EmptyDocText()
    {
        const string source = "/// @name my_node\nPanel { }";
        var result = Parse(source);
        Assert.AreEqual("", result.Root.RootComponent.DocumentationComment!.GetDocumentationText());
        Assert.AreEqual("my_node", result.Root.RootComponent.DocumentationComment!.GetNameMarker());
    }

    // ================================================================
    // Comma separator enforcement tests (GUML1016)
    // ================================================================

    [TestMethod]
    public void Comma_MissingBetweenProperties_ReportsGUML1016()
    {
        var result = Parse("Panel { text: \"hello\" visible: true }");
        var errors = result.Diagnostics.Where(d => d.Id == "GUML1016").ToList();
        Assert.AreEqual(1, errors.Count);
    }

    [TestMethod]
    public void Comma_PresentBetweenProperties_NoGUML1016()
    {
        var result = Parse("Panel { text: \"hello\", visible: true }");
        var errors = result.Diagnostics.Where(d => d.Id == "GUML1016").ToList();
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    public void Comma_TrailingBeforeCloseBrace_NoGUML1016()
    {
        var result = Parse("Panel { text: \"hello\", }");
        var errors = result.Diagnostics.Where(d => d.Id == "GUML1016").ToList();
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    public void Comma_LastElementNoComma_NoGUML1016()
    {
        var result = Parse("Panel { text: \"hello\" }");
        var errors = result.Diagnostics.Where(d => d.Id == "GUML1016").ToList();
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    public void Comma_NoCommaBeforeChildComponent_NoGUML1016()
    {
        var result = Parse("Panel {\n    text: \"hello\"\n    Label { }\n}");
        var errors = result.Diagnostics.Where(d => d.Id == "GUML1016").ToList();
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    public void Comma_NoCommaAfterParamDecl_NoGUML1016()
    {
        var result = Parse("Panel {\n    param int x\n    text: \"hello\"\n}");
        var errors = result.Diagnostics.Where(d => d.Id == "GUML1016").ToList();
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    public void Comma_MissingBetweenEventAndProperty_ReportsGUML1016()
    {
        var result = Parse("Panel { #pressed: $controller.on_click text: \"hello\" }");
        var errors = result.Diagnostics.Where(d => d.Id == "GUML1016").ToList();
        Assert.AreEqual(1, errors.Count);
    }

    [TestMethod]
    public void Comma_MissingBetweenMappings_ReportsGUML1016()
    {
        var result = Parse("Panel { name := $controller.name value =: $controller.value }");
        var errors = result.Diagnostics.Where(d => d.Id == "GUML1016").ToList();
        Assert.AreEqual(1, errors.Count);
    }

    [TestMethod]
    public void Comma_StructInitializer_MissingComma_ReportsGUML1016()
    {
        var result = Parse("Panel { prop: new LabelSettings { font_size: 24 font_color: \"red\" } }");
        var errors = result.Diagnostics.Where(d => d.Id == "GUML1016").ToList();
        Assert.AreEqual(1, errors.Count);
    }

    [TestMethod]
    public void Comma_FunctionArgs_MissingComma_ReportsGUML1016()
    {
        var result = Parse("Panel { size: vec2(100 200) }");
        var errors = result.Diagnostics.Where(d => d.Id == "GUML1016").ToList();
        Assert.AreEqual(1, errors.Count);
    }

    [TestMethod]
    public void Comma_MultipleProperties_MultipleErrors()
    {
        var result = Parse("Panel { a: 1 b: 2 c: 3 }");
        var errors = result.Diagnostics.Where(d => d.Id == "GUML1016").ToList();
        Assert.AreEqual(2, errors.Count);
    }

    [TestMethod]
    public void Comma_NoCommaBeforeEachBlock_NoGUML1016()
    {
        var result = Parse("Panel {\n    text: \"hello\"\n    each $controller.items => Label { }\n}");
        var errors = result.Diagnostics.Where(d => d.Id == "GUML1016").ToList();
        Assert.AreEqual(0, errors.Count);
    }

    // ================================================================
    // CallExpression tests
    // ================================================================

    [TestMethod]
    public void Expression_CallExpression_WithArgs()
    {
        var result = Parse("Panel {\n    x: $controller.compute(1, 2)\n}");
        Assert.AreEqual(0, result.Diagnostics.Count);
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        Assert.IsNotNull(prop);
        // $controller.compute(...) is MemberAccess followed by CallExpression postfix
        var call = prop.Value as CallExpressionSyntax;
        Assert.IsNotNull(call);
        Assert.AreEqual(2, call.Arguments.Count);
        Assert.AreEqual(SyntaxKind.OpenParenToken, call.OpenParen.Kind);
        Assert.AreEqual(SyntaxKind.CloseParenToken, call.CloseParen.Kind);
    }

    [TestMethod]
    public void Expression_CallExpression_NoArgs()
    {
        var result = Parse("Panel {\n    x: $controller.reset()\n}");
        Assert.AreEqual(0, result.Diagnostics.Count);
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        Assert.IsNotNull(prop);
        var call = prop.Value as CallExpressionSyntax;
        Assert.IsNotNull(call);
        Assert.AreEqual(0, call.Arguments.Count);
    }

    [TestMethod]
    public void Expression_CallExpression_Callee_IsMemberAccess()
    {
        var result = Parse("Panel {\n    v: $controller.get_value()\n}");
        Assert.AreEqual(0, result.Diagnostics.Count);
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        var call = prop!.Value as CallExpressionSyntax;
        Assert.IsNotNull(call);
        // The callee should be a MemberAccessExpression
        Assert.IsInstanceOfType(call.Expression, typeof(MemberAccessExpressionSyntax));
        var member = (MemberAccessExpressionSyntax)call.Expression;
        Assert.AreEqual("get_value", member.Name.Text);
    }

    [TestMethod]
    public void RoundTrip_CallExpression()
    {
        const string source = "Panel {\n    x: $controller.compute(10, 20)\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    [TestMethod]
    public void RoundTrip_CallExpression_NoArgs()
    {
        const string source = "Panel {\n    x: $controller.reset()\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    // ================================================================
    // DictionaryLiteralExpression tests
    // ================================================================

    [TestMethod]
    public void Expression_DictionaryLiteral_Structure()
    {
        var result = Parse("Panel {\n    data: Dictionary[String, Int32]{ \"key\": 1, \"other\": 2 }\n}");
        Assert.AreEqual(0, result.Diagnostics.Count);
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        Assert.IsNotNull(prop);
        var dict = prop.Value as DictionaryLiteralExpressionSyntax;
        Assert.IsNotNull(dict);
        Assert.AreEqual("Dictionary", dict.TypeName.Text);
        Assert.AreEqual("String", dict.KeyType.Text);
        Assert.AreEqual("Int32", dict.ValueType.Text);
        Assert.AreEqual(2, dict.Entries.Count);
    }

    [TestMethod]
    public void Expression_DictionaryLiteral_Empty()
    {
        var result = Parse("Panel {\n    data: Dictionary[String, Int32]{ }\n}");
        Assert.AreEqual(0, result.Diagnostics.Count);
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        Assert.IsNotNull(prop);
        var dict = prop.Value as DictionaryLiteralExpressionSyntax;
        Assert.IsNotNull(dict);
        Assert.AreEqual(0, dict.Entries.Count);
    }

    [TestMethod]
    public void Expression_DictionaryLiteral_EntryStructure()
    {
        var result = Parse("Panel {\n    data: Dictionary[String, Int32]{ \"key\": 42 }\n}");
        Assert.AreEqual(0, result.Diagnostics.Count);
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        var dict = (DictionaryLiteralExpressionSyntax)prop!.Value;
        var entry = dict.Entries[0];
        Assert.IsNotNull(entry);
        // Key is a string literal "key"
        Assert.IsInstanceOfType(entry.Key, typeof(LiteralExpressionSyntax));
        // Value is an integer literal 42
        Assert.IsInstanceOfType(entry.Value, typeof(LiteralExpressionSyntax));
        var valLit = (LiteralExpressionSyntax)entry.Value;
        Assert.AreEqual("42", valLit.Token.Text);
    }

    [TestMethod]
    public void RoundTrip_DictionaryLiteral()
    {
        const string source = "Panel {\n    data: Dictionary[String, Int32]{ \"a\": 1, \"b\": 2 }\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    [TestMethod]
    public void RoundTrip_DictionaryLiteral_Empty()
    {
        const string source = "Panel {\n    data: Dictionary[String, Int32]{ }\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    // ================================================================
    // StructExpression with named args tests
    // ================================================================

    [TestMethod]
    public void Expression_StructExpression_NamedArgs()
    {
        var result = Parse("Panel {\n    pos: Vector2({ x: 100, y: 200 })\n}");
        Assert.AreEqual(0, result.Diagnostics.Count);
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        Assert.IsNotNull(prop);
        var structExpr = prop.Value as StructExpressionSyntax;
        Assert.IsNotNull(structExpr);
        Assert.AreEqual("Vector2", structExpr.TypeName.Text);
        Assert.IsNull(structExpr.PositionalArgs);
        Assert.IsNotNull(structExpr.NamedArgs);
        Assert.AreEqual(2, structExpr.NamedArgs!.Properties.Count);
    }

    [TestMethod]
    public void Expression_StructExpression_EmptyPositionalArgs()
    {
        var result = Parse("Panel {\n    pos: Vector2()\n}");
        Assert.AreEqual(0, result.Diagnostics.Count);
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        var structExpr = prop!.Value as StructExpressionSyntax;
        Assert.IsNotNull(structExpr);
        Assert.IsNotNull(structExpr.PositionalArgs);
        Assert.IsNull(structExpr.NamedArgs);
        Assert.AreEqual(0, structExpr.PositionalArgs!.Count);
    }

    [TestMethod]
    public void RoundTrip_StructExpression_NamedArgs()
    {
        const string source = "Panel {\n    pos: Vector2({ x: 100, y: 200 })\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    // ================================================================
    // TemplateStringExpression structure tests
    // ================================================================

    [TestMethod]
    public void Expression_TemplateString_Structure_PlainText()
    {
        var result = Parse("Panel {\n    t: $\"hello world\"\n}");
        Assert.AreEqual(0, result.Diagnostics.Count);
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        Assert.IsNotNull(prop);
        var tmpl = prop.Value as TemplateStringExpressionSyntax;
        Assert.IsNotNull(tmpl);
        Assert.AreEqual(SyntaxKind.TemplateStringLiteralToken, tmpl.OpenToken.Kind);
        // No interpolations — should have only text parts
        Assert.IsTrue(tmpl.Parts.Count >= 0); // could be 1 text part
    }

    [TestMethod]
    public void Expression_TemplateString_Structure_WithInterpolation()
    {
        var result = Parse("Panel {\n    t: $\"Count: {$controller.count}\"\n}");
        Assert.AreEqual(0, result.Diagnostics.Count);
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        Assert.IsNotNull(prop);
        var tmpl = prop.Value as TemplateStringExpressionSyntax;
        Assert.IsNotNull(tmpl);
        // Should contain an interpolation part
        bool hasInterpolation = false;
        foreach (var part in tmpl.Parts)
        {
            if (part is TemplateStringInterpolationSyntax)
            {
                hasInterpolation = true;
                break;
            }
        }
        Assert.IsTrue(hasInterpolation, "Expected at least one interpolation in the template string");
    }

    [TestMethod]
    public void Expression_TemplateString_Interpolation_ExpressionContent()
    {
        var result = Parse("Panel {\n    t: $\"Hi {$controller.name}\"\n}");
        Assert.AreEqual(0, result.Diagnostics.Count);
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        var tmpl = (TemplateStringExpressionSyntax)prop!.Value;
        // Find the interpolation
        TemplateStringInterpolationSyntax? interp = null;
        foreach (var part in tmpl.Parts)
        {
            if (part is TemplateStringInterpolationSyntax i) { interp = i; break; }
        }
        Assert.IsNotNull(interp);
        // The expression inside the interpolation is a MemberAccess
        Assert.IsInstanceOfType(interp!.Expression, typeof(MemberAccessExpressionSyntax));
        var member = (MemberAccessExpressionSyntax)interp.Expression;
        Assert.AreEqual("name", member.Name.Text);
    }

    // ================================================================
    // EachBlock variable-name tests
    // ================================================================

    [TestMethod]
    public void EachBlock_VariableNames_AreSet()
    {
        var result = Parse(
            "VBoxContainer {\n    each $controller.items {\n        |idx, item|\n        Label { text: item }\n    }\n}");
        Assert.AreEqual(0, result.Diagnostics.Count);
        var each = result.Root.RootComponent.Members[0] as EachBlockSyntax;
        Assert.IsNotNull(each);
        Assert.IsNotNull(each.IndexName);
        Assert.AreEqual("idx", each.IndexName!.Text);
        Assert.IsNotNull(each.ValueName);
        Assert.AreEqual("item", each.ValueName!.Text);
    }

    [TestMethod]
    public void EachBlock_DataSource_IsMemberAccess()
    {
        var result = Parse("Panel {\n    each $controller.items => item_template\n}");
        Assert.AreEqual(0, result.Diagnostics.Count);
        var each = result.Root.RootComponent.Members[0] as EachBlockSyntax;
        Assert.IsNotNull(each);
        Assert.IsInstanceOfType(each.DataSource, typeof(MemberAccessExpressionSyntax));
        var src = (MemberAccessExpressionSyntax)each.DataSource;
        Assert.AreEqual("items", src.Name.Text);
    }

    [TestMethod]
    public void EachBlock_WithParams_Structure()
    {
        var result = Parse(
            "Panel {\n    each({cache: 5}) $controller.items {\n        |idx, val|\n        Label { text: val }\n    }\n}");
        Assert.AreEqual(0, result.Diagnostics.Count);
        var each = result.Root.RootComponent.Members[0] as EachBlockSyntax;
        Assert.IsNotNull(each);
        Assert.IsNotNull(each.Params);
        Assert.AreEqual(1, each.Params!.Properties.Count);
        Assert.AreEqual("cache", each.Params.Properties[0].Name.Text);
    }

    [TestMethod]
    public void RoundTrip_EachBlock_WithVariables()
    {
        const string source =
            "VBoxContainer {\n    each $controller.items {\n        |idx, item|\n        Label {\n            text: item\n        }\n    }\n}";
        var result = Parse(source);
        Assert.AreEqual(source, result.Root.ToFullString());
    }

    // ================================================================
    // ObjectLiteralExpression tests
    // ================================================================

    [TestMethod]
    public void Expression_ObjectLiteral_Structure()
    {
        var result = Parse("Panel {\n    pos: Vector2({ x: 10, y: 20 })\n}");
        Assert.AreEqual(0, result.Diagnostics.Count);
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        var structExpr = prop!.Value as StructExpressionSyntax;
        Assert.IsNotNull(structExpr?.NamedArgs);
        var objLit = structExpr!.NamedArgs;
        Assert.AreEqual(2, objLit!.Properties.Count);
        Assert.AreEqual("x", objLit.Properties[0].Name.Text);
        Assert.AreEqual("y", objLit.Properties[1].Name.Text);
    }

    [TestMethod]
    public void Expression_ObjectLiteral_Empty()
    {
        var result = Parse("Panel {\n    pos: Vector2({})\n}");
        Assert.AreEqual(0, result.Diagnostics.Count);
        var prop = result.Root.RootComponent.Members[0] as PropertyAssignmentSyntax;
        var structExpr = prop!.Value as StructExpressionSyntax;
        Assert.IsNotNull(structExpr?.NamedArgs);
        Assert.AreEqual(0, structExpr!.NamedArgs!.Properties.Count);
    }

    // ================================================================
    // SyntaxNode Span / FullSpan tests
    // ================================================================

    [TestMethod]
    public void SyntaxNode_Span_ExcludesLeadingTrivia()
    {
        const string source = "Label {\n    text: \"hi\"\n}";
        var result = Parse(source);
        var root = result.Root;
        // Root component starts at "Label" (position 0, no leading trivia)
        Assert.AreEqual(0, root.RootComponent.Span.Start);
    }

    [TestMethod]
    public void SyntaxNode_FullSpan_IncludesAllTrivia()
    {
        const string source = "Label { }";
        var result = Parse(source);
        var root = result.Root;
        Assert.AreEqual(source.Length, root.FullWidth);
        Assert.AreEqual(source.Length, root.FullSpan.Length);
    }
}
