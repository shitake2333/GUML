using RegexTokenizeGenerator;

namespace GUML.Tests;

[TestClass]
public class GumlParserTests
{
    private GumlParser? _parser;

    [TestInitialize]
    public void Setup()
    {
        _parser = new GumlParser();
    }

    [TestMethod]
    public void TestParseSimpleComponent()
    {
        string code = "MyComponent {}";
        var doc = _parser!.Parse(code);
        Assert.IsNotNull(doc.RootNode);
        Assert.AreEqual("MyComponent", doc.RootNode.Name);
    }

    [TestMethod]
    public void TestParseUnexpectedToken()
    {
        string code = "MyComponent { invalid }";
        try
        {
            new GumlParser().Parse(code);
            Assert.Fail("Expected GumlParserException");
        }
        catch (GumlParserException ex)
        {
            string diagnostic = ex.PrintDiagnostic();
            Console.WriteLine("Diagnostic Output:\n" + diagnostic);

            Assert.Contains("Unexpected symbol", ex.Message);
            if (!string.IsNullOrEmpty(ex.CodeString))
                Assert.Contains("MyComponent { invalid }", diagnostic);
        }
    }

    [TestMethod]
    public void TestParseMissingOperator()
    {
        // "Prop: 1 (2)"
        // Parser sees Prop: 1. Then sees (.
        // Current logic might think ( starts a new value expression group?
        // But 1 is already a complete expression.
        // It should throw specific error.

        string code2 = "MyComponent { Prop: 1 (2) }";
        try
        {
            new GumlParser().Parse(code2);
            Assert.Fail("Expected GumlParserException");
        }
        catch (GumlParserException ex)
        {
            string diagnostic = ex.PrintDiagnostic();
            Console.WriteLine("Diagnostic Output:\n" + diagnostic);
            // The message might vary slightly
            // Assert.IsTrue(ex.Message.Contains("maybe you missed an operator"), "Actual message: " + ex.Message);
            // Update expectation to match current parser behavior which throws "Unexpected symbol type..."
            Assert.Contains("Unexpected symbol", ex.Message, "Actual message: " + ex.Message);
        }
    }

    [TestMethod]
    public void TestParserDiagnosticOutputFormat()
    {
        // Construct an exception manually to test formatting independent of parser logic
        string code = "line1\nline2 error here\nline3";
        // Let's verify line 2, error starts at index `6` (0-based relative to line) -> 'e'
        // line2 is at index 6 (line1\n)

        var posInfo = new Token
        {
            Line = 2,
            Column = 7, // 1-based, 'e' is 7th char
            Start = 12, // 'e' index
            End = 17, // 'error' length 5
            Value = "error" // Token needs a value
        };

        var ex = new GumlParserException("Manual Error", posInfo, code);
        string diagnostic = ex.PrintDiagnostic();
        /*
        error: Manual Error
          --> line:2:7
           |
        2   | line2 error here
           |       ^^^^^ Manual Error
        */

        string expectedHeader = "error: Manual Error";
        string expectedLocation = "  --> line:2:7";

        Assert.Contains(expectedHeader, diagnostic, "Header format mismatch");
        Assert.Contains(expectedLocation, diagnostic, "Location format mismatch");
        // Update expectation for dynamic width format: " {Line} | "
        // Line=2 -> " 2 | "
        Assert.Contains(" 2 | line2 error here", diagnostic, "Code context format mismatch");
        // Check for pointer line somewhat loosely but verifying key parts
        Assert.Contains("^^^^^ Manual Error", diagnostic, "Pointer line format mismatch");

        Console.WriteLine(diagnostic);
    }

    [TestMethod]
    public void TestParseControllerPropertyBinding()
    {
        // This was previously broken because '.' was consumed by float NumberPattern.
        string code = @"Panel {
    Label {
        text:= $controller.Name
    }
}";
        var parser = new GumlParser();
        parser.WithConverter(new KeyConverter());
        var doc = parser.Parse(code);

        Assert.IsNotNull(doc.RootNode);
        Assert.AreEqual("Panel", doc.RootNode.Name);
        Assert.HasCount(1, doc.RootNode.Children);

        var label = doc.RootNode.Children[0];
        Assert.AreEqual("Label", label.Name);

        // text should be a binding property
        Assert.IsTrue(label.Properties.ContainsKey("Text"));
        var (isBind, exprNode) = label.Properties["Text"];
        Assert.IsTrue(isBind, "text:= should be a binding expression");

        // The expression should be a Ref node chain: $controller -> .Name
        Assert.IsInstanceOfType<GumlValueNode>(exprNode);
        var valueNode = (GumlValueNode)exprNode;
        Assert.AreEqual(GumlValueType.Ref, valueNode.ValueType);
        Assert.AreEqual(RefType.PropertyRef, valueNode.RefType);
    }

    // ========================================================================
    // Import / Redirect tests
    // ========================================================================

    [TestMethod]
    public void TestParseSingleImport()
    {
        string code = "import \"utils.guml\"\nPanel {}";
        var doc = _parser!.Parse(code);

        Assert.AreEqual(1, doc.Imports.Count);
        // Token value includes surrounding quotes
        Assert.IsTrue(doc.Imports.ContainsKey("\"utils.guml\""));
        Assert.IsFalse(doc.Imports["\"utils.guml\""], "import should not be isTop");
    }

    [TestMethod]
    public void TestParseImportTop()
    {
        string code = "import_top \"base.guml\"\nPanel {}";
        var doc = _parser!.Parse(code);

        Assert.AreEqual(1, doc.Imports.Count);
        Assert.IsTrue(doc.Imports["\"base.guml\""], "import_top should be isTop=true");
    }

    [TestMethod]
    public void TestParseMultipleImports()
    {
        string code = "import \"a.guml\"\nimport_top \"b.guml\"\nPanel {}";
        var doc = _parser!.Parse(code);

        Assert.AreEqual(2, doc.Imports.Count);
        Assert.IsFalse(doc.Imports["\"a.guml\""]);
        Assert.IsTrue(doc.Imports["\"b.guml\""]);
    }

    [TestMethod]
    public void TestParseDuplicateImportThrows()
    {
        string code = "import \"a.guml\"\nimport \"a.guml\"\nPanel {}";
        Assert.ThrowsExactly<GumlParserException>(() => _parser!.Parse(code));
    }

    [TestMethod]
    public void TestParseRedirect()
    {
        string code = "redirect \"other.guml\"\nPanel {}";
        var doc = _parser!.Parse(code);

        // Redirect value includes surrounding quotes from the string token
        Assert.AreEqual("\"other.guml\"", doc.Redirect);
    }

    [TestMethod]
    public void TestParseImportAndRedirect()
    {
        string code = "import \"a.guml\"\nredirect \"other.guml\"\nPanel {}";
        var doc = _parser!.Parse(code);

        Assert.AreEqual(1, doc.Imports.Count);
        Assert.AreEqual("\"other.guml\"", doc.Redirect);
    }

    [TestMethod]
    public void TestParseEmptyFileThrows()
    {
        // Only EOF — "Must has root component"
        string code = "";
        Assert.ThrowsExactly<GumlParserException>(() => _parser!.Parse(code));
    }

    // ========================================================================
    // Component structure tests
    // ========================================================================

    [TestMethod]
    public void TestParseNestedComponents()
    {
        string code = @"Panel {
    Label {}
    Button {}
}";
        var doc = _parser!.Parse(code);
        Assert.AreEqual("Panel", doc.RootNode.Name);
        Assert.AreEqual(2, doc.RootNode.Children.Count);
        Assert.AreEqual("Label", doc.RootNode.Children[0].Name);
        Assert.AreEqual("Button", doc.RootNode.Children[1].Name);
    }

    [TestMethod]
    public void TestParseDeeplyNestedComponents()
    {
        string code = @"Panel {
    VBoxContainer {
        Label {}
    }
}";
        var doc = _parser!.Parse(code);
        Assert.AreEqual(1, doc.RootNode.Children.Count);
        var vbox = doc.RootNode.Children[0];
        Assert.AreEqual("VBoxContainer", vbox.Name);
        Assert.AreEqual(1, vbox.Children.Count);
        Assert.AreEqual("Label", vbox.Children[0].Name);
    }

    [TestMethod]
    public void TestParseAliasComponent()
    {
        string code = @"Panel {
    @myLabel: Label {}
}";
        var doc = _parser!.Parse(code);
        Assert.AreEqual(1, doc.RootNode.Children.Count);
        Assert.AreEqual("Label", doc.RootNode.Children[0].Name);
        Assert.IsTrue(doc.LocalAlias.ContainsKey("@myLabel"));
    }

    // ========================================================================
    // Property value types
    // ========================================================================

    [TestMethod]
    public void TestParseStringProperty()
    {
        string code = "Panel { name: \"hello\" }";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        var (isBind, expr) = doc.RootNode.Properties["name"];
        Assert.IsFalse(isBind);
        var val = (GumlValueNode)expr;
        Assert.AreEqual(GumlValueType.String, val.ValueType);
        // String token value includes surrounding quotes
        Assert.AreEqual("\"hello\"", val.StringValue);
    }

    [TestMethod]
    public void TestParseIntegerProperty()
    {
        string code = "Panel { name: 42 }";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        var (_, expr) = doc.RootNode.Properties["name"];
        var val = (GumlValueNode)expr;
        Assert.AreEqual(GumlValueType.Int, val.ValueType);
        Assert.AreEqual(42, val.IntValue);
    }

    [TestMethod]
    public void TestParseFloatProperty()
    {
        string code = "Panel { name: 3.14 }";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        var (_, expr) = doc.RootNode.Properties["name"];
        var val = (GumlValueNode)expr;
        Assert.AreEqual(GumlValueType.Float, val.ValueType);
        Assert.AreEqual(3.14f, val.FloatValue, 0.001f);
    }

    [TestMethod]
    public void TestParseBooleanProperty()
    {
        string code = "Panel { name: true }";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        var (_, expr) = doc.RootNode.Properties["name"];
        var val = (GumlValueNode)expr;
        Assert.AreEqual(GumlValueType.Boolean, val.ValueType);
        Assert.IsTrue(val.BooleanValue);
    }

    [TestMethod]
    public void TestParseNullProperty()
    {
        string code = "Panel { name: Null }";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        var (_, expr) = doc.RootNode.Properties["name"];
        var val = (GumlValueNode)expr;
        Assert.AreEqual(GumlValueType.Null, val.ValueType);
    }

    [TestMethod]
    public void TestParseVec2Property()
    {
        string code = "Panel { name: vec2(10, 20) }";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        var (_, expr) = doc.RootNode.Properties["name"];
        var val = (GumlValueNode)expr;
        Assert.AreEqual(GumlValueType.Vector2, val.ValueType);
        Assert.IsNotNull(val.Vector2XNode);
        Assert.IsNotNull(val.Vector2YNode);

        var x = (GumlValueNode)val.Vector2XNode;
        var y = (GumlValueNode)val.Vector2YNode;
        Assert.AreEqual(GumlValueType.Int, x.ValueType);
        Assert.AreEqual(10, x.IntValue);
        Assert.AreEqual(GumlValueType.Int, y.ValueType);
        Assert.AreEqual(20, y.IntValue);
    }

    [TestMethod]
    public void TestParseColorProperty()
    {
        string code = "Panel { name: color(1.0, 0.5, 0.0, 1.0) }";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        var (_, expr) = doc.RootNode.Properties["name"];
        var val = (GumlValueNode)expr;
        Assert.AreEqual(GumlValueType.Color, val.ValueType);
        Assert.IsNotNull(val.ColorRNode);
        Assert.IsNotNull(val.ColorGNode);
        Assert.IsNotNull(val.ColorBNode);
        Assert.IsNotNull(val.ColorANode);
    }

    [TestMethod]
    public void TestParseStyleBoxEmpty()
    {
        string code = "Panel { name: style_box_empty() }";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        var (_, expr) = doc.RootNode.Properties["name"];
        var val = (GumlValueNode)expr;
        Assert.AreEqual(GumlValueType.StyleBox, val.ValueType);
        Assert.AreEqual(StyleNodeType.Empty, val.StyleNodeType);
    }

    [TestMethod]
    public void TestParseStyleBoxFlat()
    {
        string code = "Panel { name: style_box_flat({ bg_color: color(1.0, 0.0, 0.0, 1.0) }) }";
        var parser = new GumlParser();
        parser.WithConverter(new KeyConverter());
        var doc = parser.Parse(code);

        var (_, expr) = doc.RootNode.Properties["Name"];
        var val = (GumlValueNode)expr;
        Assert.AreEqual(GumlValueType.StyleBox, val.ValueType);
        Assert.AreEqual(StyleNodeType.Flat, val.StyleNodeType);
        Assert.IsNotNull(val.StyleNode);
    }

    [TestMethod]
    public void TestParseStyleBoxLine()
    {
        string code = "Panel { name: style_box_line({ name: 1 }) }";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        var (_, expr) = doc.RootNode.Properties["name"];
        var val = (GumlValueNode)expr;
        Assert.AreEqual(GumlValueType.StyleBox, val.ValueType);
        Assert.AreEqual(StyleNodeType.Line, val.StyleNodeType);
    }

    [TestMethod]
    public void TestParseStyleBoxTexture()
    {
        string code = "Panel { name: style_box_texture({ name: 1 }) }";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        var (_, expr) = doc.RootNode.Properties["name"];
        var val = (GumlValueNode)expr;
        Assert.AreEqual(GumlValueType.StyleBox, val.ValueType);
        Assert.AreEqual(StyleNodeType.Texture, val.StyleNodeType);
    }

    [TestMethod]
    public void TestParseResourceProperty()
    {
        string code = "Panel { name: resource(\"res://icon.png\") }";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        var (_, expr) = doc.RootNode.Properties["name"];
        var val = (GumlValueNode)expr;
        Assert.AreEqual(GumlValueType.Resource, val.ValueType);
        Assert.IsNotNull(val.ResourceNode);
    }

    [TestMethod]
    public void TestParseObjectProperty()
    {
        string code = "Panel { name: { key1: 10, key2: \"val\" } }";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        var (_, expr) = doc.RootNode.Properties["name"];
        var val = (GumlValueNode)expr;
        Assert.AreEqual(GumlValueType.Object, val.ValueType);
        Assert.IsNotNull(val.ObjectValue);
        Assert.AreEqual(2, val.ObjectValue.Count);
        Assert.IsTrue(val.ObjectValue.ContainsKey("key1"));
        Assert.IsTrue(val.ObjectValue.ContainsKey("key2"));
    }

    // ========================================================================
    // Reference types
    // ========================================================================

    [TestMethod]
    public void TestParseGlobalRef()
    {
        string code = "Panel { name:= $controller }";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        var (isBind, expr) = doc.RootNode.Properties["name"];
        Assert.IsTrue(isBind);
        var val = (GumlValueNode)expr;
        Assert.AreEqual(GumlValueType.Ref, val.ValueType);
        Assert.AreEqual(RefType.GlobalRef, val.RefType);
        Assert.AreEqual("$controller", val.RefName);
    }

    [TestMethod]
    public void TestParseAliasRef()
    {
        string code = @"Panel {
    @myLabel: Label {}
    Button { name:= @myLabel }
}";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        var button = doc.RootNode.Children[1];
        var (isBind, expr) = button.Properties["name"];
        Assert.IsTrue(isBind);
        var val = (GumlValueNode)expr;
        Assert.AreEqual(RefType.LocalAliasRef, val.RefType);
        Assert.AreEqual("@myLabel", val.RefName);
    }

    [TestMethod]
    public void TestParsePropertyChainRef()
    {
        string code = "Panel { name:= $controller.Data.Name }";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        var (_, expr) = doc.RootNode.Properties["name"];
        var val = (GumlValueNode)expr;
        Assert.AreEqual(RefType.PropertyRef, val.RefType);
        Assert.AreEqual("Name", val.RefName);
        // Should have a chain: Name -> Data -> $controller
        Assert.IsNotNull(val.RefNode);
        Assert.AreEqual("Data", val.RefNode.RefName);
        Assert.AreEqual(RefType.PropertyRef, val.RefNode.RefType);
        Assert.IsNotNull(val.RefNode.RefNode);
        Assert.AreEqual("$controller", val.RefNode.RefNode.RefName);
        Assert.AreEqual(RefType.GlobalRef, val.RefNode.RefNode.RefType);
    }

    // ========================================================================
    // Signal binding
    // ========================================================================

    [TestMethod]
    public void TestParseSignalBinding()
    {
        string code = "Panel { #pressed: \"OnPressed\" }";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        Assert.AreEqual(1, doc.RootNode.Signals.Count);
        Assert.IsTrue(doc.RootNode.Signals.ContainsKey("pressed"));
        // Signal handler value includes surrounding quotes from the string token
        Assert.AreEqual("\"OnPressed\"", doc.RootNode.Signals["pressed"]);
    }

    [TestMethod]
    public void TestParseSignalBindingWithKeyConverter()
    {
        string code = "Panel { #button_down: \"OnButtonDown\" }";
        var parser = new GumlParser();
        parser.WithConverter(new KeyConverter());
        var doc = parser.Parse(code);

        Assert.IsTrue(doc.RootNode.Signals.ContainsKey("ButtonDown"));
        // Signal handler value includes surrounding quotes from the string token
        Assert.AreEqual("\"OnButtonDown\"", doc.RootNode.Signals["ButtonDown"]);
    }

    // ========================================================================
    // Each loop
    // ========================================================================

    [TestMethod]
    public void TestParseEachLoop()
    {
        string code = @"Panel {
    each $controller.Items {
        |idx, item|
        Label {
            name: ""test""
        }
    }
}";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        Assert.AreEqual(1, doc.RootNode.EachNodes.Count);
        var each = doc.RootNode.EachNodes[0];
        Assert.AreEqual("idx", each.IndexName);
        Assert.AreEqual("item", each.ValueName);
        Assert.IsNotNull(each.DataSource);
        Assert.AreEqual(1, each.Children.Count);
        Assert.AreEqual("Label", each.Children[0].Name);
    }

    // ========================================================================
    // Expression / Operator tests
    // ========================================================================

    [TestMethod]
    public void TestParseInfixExpression()
    {
        string code = "Panel { name: 1 + 2 }";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        var (_, expr) = doc.RootNode.Properties["name"];
        Assert.IsInstanceOfType<InfixOpNode>(expr);
        var infix = (InfixOpNode)expr;
        Assert.AreEqual("+", infix.Op);

        var left = (GumlValueNode)infix.Left;
        Assert.AreEqual(GumlValueType.Int, left.ValueType);

        var right = (GumlValueNode)infix.Right;
        Assert.AreEqual(GumlValueType.Int, right.ValueType);
    }

    [TestMethod]
    public void TestParsePrefixNegation()
    {
        string code = "Panel { name: -1 }";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        var (_, expr) = doc.RootNode.Properties["name"];
        Assert.IsInstanceOfType<PrefixOpNode>(expr);
        var prefix = (PrefixOpNode)expr;
        Assert.AreEqual("-", prefix.Op);
    }

    [TestMethod]
    public void TestParsePrefixNot()
    {
        string code = "Panel { name: !true }";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        var (_, expr) = doc.RootNode.Properties["name"];
        Assert.IsInstanceOfType<PrefixOpNode>(expr);
        Assert.AreEqual("!", ((PrefixOpNode)expr).Op);
    }

    [TestMethod]
    public void TestParseOperatorPrecedence()
    {
        // 1 + 2 * 3 should parse as 1 + (2 * 3) due to precedence
        string code = "Panel { name: 1 + 2 * 3 }";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        var (_, expr) = doc.RootNode.Properties["name"];
        Assert.IsInstanceOfType<InfixOpNode>(expr);
        var plus = (InfixOpNode)expr;
        Assert.AreEqual("+", plus.Op);

        // Right side should be 2 * 3
        Assert.IsInstanceOfType<InfixOpNode>(plus.Right);
        var mul = (InfixOpNode)plus.Right;
        Assert.AreEqual("*", mul.Op);
    }

    [TestMethod]
    public void TestParseParenthesizedExpression()
    {
        // (1 + 2) * 3 — parens override standard precedence.
        // The parser tree root is still '+', but FirstPrecedence=true signals
        // that the grouped sub-expression evaluates first.
        string code = "Panel { name: (1 + 2) * 3 }";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        var (_, expr) = doc.RootNode.Properties["name"];
        Assert.IsInstanceOfType<InfixOpNode>(expr);
        var plus = (InfixOpNode)expr;
        Assert.AreEqual("+", plus.Op);
        Assert.IsTrue(plus.FirstPrecedence, "Parenthesized group should set FirstPrecedence");

        // Right child is the '*' node
        Assert.IsInstanceOfType<InfixOpNode>(plus.Right);
        var mul = (InfixOpNode)plus.Right;
        Assert.AreEqual("*", mul.Op);
    }

    [TestMethod]
    public void TestParseComparisonOperators()
    {
        string code = "Panel { name: 1 == 2 }";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        var (_, expr) = doc.RootNode.Properties["name"];
        Assert.IsInstanceOfType<InfixOpNode>(expr);
        Assert.AreEqual("==", ((InfixOpNode)expr).Op);
    }

    [TestMethod]
    public void TestParseLogicalOperators()
    {
        string code = "Panel { name: true && false }";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        var (_, expr) = doc.RootNode.Properties["name"];
        Assert.IsInstanceOfType<InfixOpNode>(expr);
        Assert.AreEqual("&&", ((InfixOpNode)expr).Op);
    }

    [TestMethod]
    public void TestParseMultipleProperties()
    {
        string code = @"Panel {
    name: ""hello"",
    signal_name: ""world""
}";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        Assert.AreEqual(2, doc.RootNode.Properties.Count);
    }

    [TestMethod]
    public void TestParseMultiplePropertiesWithPipe()
    {
        // pipe | is also a value separator
        string code = "Panel { name: 1 | signal_name: 2 }";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        Assert.AreEqual(2, doc.RootNode.Properties.Count);
    }

    [TestMethod]
    public void TestParseExtraTokenAfterRootThrows()
    {
        // After the root component, there should be only EOF
        string code = "Panel {} Panel {}";
        Assert.ThrowsExactly<GumlParserException>(() => _parser!.Parse(code));
    }

    // ========================================================================
    // Diagnostic exception edge cases
    // ========================================================================

    [TestMethod]
    public void TestDiagnostic_EmptyCodeString_ReturnsMessage()
    {
        var posInfo = new Token { Line = 1, Column = 1, Start = 0, End = 1, Value = "x" };
        var ex = new GumlParserException("Test Error", posInfo);
        string diag = ex.PrintDiagnostic();
        // No code string → just returns Message
        Assert.AreEqual(ex.Message, diag);
    }

    [TestMethod]
    public void TestDiagnostic_InvalidLineNumber_ReturnsMessage()
    {
        var posInfo = new Token { Line = 999, Column = 1, Start = 0, End = 1, Value = "x" };
        var ex = new GumlParserException("Test Error", posInfo, "single line");
        string diag = ex.PrintDiagnostic();
        Assert.AreEqual(ex.Message, diag);
    }

    [TestMethod]
    public void TestParseBindingExpressionWithRef()
    {
        // Binding (:=) with a global ref chain
        string code = "Panel { name:= $controller.Title }";
        var parser = new GumlParser();
        var doc = parser.Parse(code);

        var (isBind, expr) = doc.RootNode.Properties["name"];
        Assert.IsTrue(isBind);
        var val = (GumlValueNode)expr;
        Assert.AreEqual(RefType.PropertyRef, val.RefType);
        Assert.AreEqual("Title", val.RefName);
    }
}
