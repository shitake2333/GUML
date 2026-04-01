using GUML.Shared.Syntax;
using GUML.Shared.Syntax.Nodes;

namespace GUML.SourceGenerator.Tests;

[TestClass]
public class EachPropertyAccessTests
{
    private static GumlDocumentSyntax ParseGuml(string code)
    {
        var result = GumlSyntaxTree.Parse(code);
        return result.Root;
    }

    [TestMethod]
    public void EachLoop_PropertyAccessOnItem_UsesDynamicLookup()
    {
        string guml = @"Control {
    each $controller.Items { |i, item|
        Label {
            text: item.Title
        }
    }
}";
        var doc = ParseGuml(guml);
        string code = GumlCodeEmitter.Emit("each_prop_test.guml", doc);

        // Verify that the lookup is cast to dynamic to allow property access on 'object' return type
        // It should look like: ((dynamic)__scope_0.Lookup("item")).Title
        StringAssert.Contains(code, "((dynamic)");
        StringAssert.Contains(code, ".Lookup(\"item\"))");
        StringAssert.Contains(code, ".Title");
    }
}



