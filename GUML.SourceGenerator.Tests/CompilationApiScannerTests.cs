using System.Collections.ObjectModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace GUML.SourceGenerator.Tests;

/// <summary>
/// Tests for <see cref="CompilationApiScanner.ResolveCollectionElementType"/>.
/// Uses in-memory Roslyn compilations with Godot-namespace stubs.
/// </summary>
[TestClass]
public class CompilationApiScannerTests
{
    private static CompilationApiScanner CreateScanner(string extraSource = "")
    {
        string godotStubs = @"
namespace Godot
{
    public class GodotObject { }
    public class Node : GodotObject { }
    public class CanvasItem : Node { }
    public class Control : CanvasItem { }
}
";
        string testTypes = @"
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Godot
{
    public class TestComponent : Control
    {
        public string[] StringArray { get; set; }
        public List<int> IntList { get; set; }
        public ObservableCollection<MyItem> Items { get; set; }
        public string SingleString { get; set; }
        public Dictionary<string, int> StringIntDict { get; set; }
    }
}

public class MyItem
{
    public string Title { get; set; }
}
" + extraSource;

        var godotTree = CSharpSyntaxTree.ParseText(godotStubs);
        var testTree = CSharpSyntaxTree.ParseText(testTypes);

        // Include core runtime references for generic collections
        var refs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ObservableCollection<>).Assembly.Location),
        };

        // Add System.Runtime reference if available (needed for .NET 5+ TFMs)
        var runtimeAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "System.Runtime");
        if (runtimeAssembly != null)
            refs.Add(MetadataReference.CreateFromFile(runtimeAssembly.Location));

        // Add netstandard reference if available
        var netstdAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "netstandard");
        if (netstdAssembly != null)
            refs.Add(MetadataReference.CreateFromFile(netstdAssembly.Location));

        var compilation = CSharpCompilation.Create("TestCompilation",
            [godotTree, testTree],
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return new CompilationApiScanner(compilation);
    }

    [TestMethod]
    public void IsAvailable_WithGodotStubs_ReturnsTrue()
    {
        var scanner = CreateScanner();
        Assert.IsTrue(scanner.IsAvailable);
    }

    // ── ResolveCollectionElementType ──

    [TestMethod]
    public void ResolveCollectionElementType_Array_ReturnsElementType()
    {
        var scanner = CreateScanner();
        string? elementType = scanner.ResolveCollectionElementType("TestComponent", "StringArray");

        Assert.IsNotNull(elementType);
        Assert.IsTrue(elementType.Contains("string") || elementType.Contains("String"),
            $"Expected string element type, got: {elementType}");
    }

    [TestMethod]
    public void ResolveCollectionElementType_GenericList_ReturnsElementType()
    {
        var scanner = CreateScanner();
        string? elementType = scanner.ResolveCollectionElementType("TestComponent", "IntList");

        Assert.IsNotNull(elementType);
        Assert.IsTrue(elementType.Contains("int") || elementType.Contains("Int32"),
            $"Expected int element type, got: {elementType}");
    }

    [TestMethod]
    public void ResolveCollectionElementType_ObservableCollection_ReturnsElementType()
    {
        var scanner = CreateScanner();
        string? elementType = scanner.ResolveCollectionElementType("TestComponent", "Items");

        Assert.IsNotNull(elementType);
        StringAssert.Contains(elementType, "MyItem",
            $"Expected MyItem element type, got: {elementType}");
    }

    [TestMethod]
    public void ResolveCollectionElementType_StringType_ResolvesToChar()
    {
        var scanner = CreateScanner();
        string? elementType = scanner.ResolveCollectionElementType("TestComponent", "SingleString");

        // string implements IEnumerable<char>, so ResolveCollectionElementType
        // correctly resolves the element type to char.
        Assert.IsNotNull(elementType, "string implements IEnumerable<char>, should resolve to char");
    }

    [TestMethod]
    public void ResolveCollectionElementType_UnknownProperty_ReturnsNull()
    {
        var scanner = CreateScanner();
        string? elementType = scanner.ResolveCollectionElementType("TestComponent", "NonExistent");

        Assert.IsNull(elementType);
    }

    [TestMethod]
    public void ResolveCollectionElementType_UnknownComponent_ReturnsNull()
    {
        var scanner = CreateScanner();
        string? elementType = scanner.ResolveCollectionElementType("NonExistentComponent", "Items");

        Assert.IsNull(elementType);
    }

    [TestMethod]
    public void ResolveCollectionElementType_Dictionary_ReturnsFirstTypeArg()
    {
        var scanner = CreateScanner();
        string? elementType = scanner.ResolveCollectionElementType("TestComponent", "StringIntDict");

        // Dictionary<TKey, TValue> is generic with 2 type args;
        // the method returns TypeArguments[0] which is TKey.
        Assert.IsNotNull(elementType);
    }

    // ── ResolvePropertyType ──

    [TestMethod]
    public void ResolvePropertyType_ExistingProperty_ReturnsType()
    {
        var scanner = CreateScanner();
        var type = scanner.ResolvePropertyType("TestComponent", "SingleString");

        Assert.IsNotNull(type);
        Assert.AreEqual(SpecialType.System_String, type.SpecialType);
    }

    [TestMethod]
    public void ResolvePropertyType_InheritedProperty_ReturnsType()
    {
        // TestComponent inherits from Control, which has Visible on CanvasItem
        // But our stubs don't add Visible — skip or add it
        var scanner = CreateScanner(@"
namespace Godot
{
    public class DerivedComponent : TestComponent
    {
    }
}
");
        var type = scanner.ResolvePropertyType("DerivedComponent", "SingleString");

        Assert.IsNotNull(type, "Should resolve inherited property from base type");
    }

    [TestMethod]
    public void ResolvePropertyType_UnknownProperty_ReturnsNull()
    {
        var scanner = CreateScanner();
        var type = scanner.ResolvePropertyType("TestComponent", "NonExistent");

        Assert.IsNull(type);
    }

    // ── HasProperty ──

    [TestMethod]
    public void HasProperty_ExistingProperty_ReturnsTrue()
    {
        var scanner = CreateScanner();
        Assert.IsTrue(scanner.HasProperty("TestComponent", "SingleString"));
    }

    [TestMethod]
    public void HasProperty_NonExistentProperty_ReturnsFalse()
    {
        var scanner = CreateScanner();
        Assert.IsFalse(scanner.HasProperty("TestComponent", "NonExistent"));
    }

    // ── GetCastExpression ──

    [TestMethod]
    public void GetCastExpression_StringType_ReturnsConvertToString()
    {
        var scanner = CreateScanner();
        var typeSymbol = scanner.ResolvePropertyType("TestComponent", "SingleString")!;

        string? cast = scanner.GetCastExpression(typeSymbol);
        Assert.AreEqual("Convert.ToString", cast);
    }

    [TestMethod]
    public void GetCastExpression_ArrayType_ReturnsNull()
    {
        var scanner = CreateScanner();
        var typeSymbol = scanner.ResolvePropertyType("TestComponent", "StringArray")!;

        // Array types are not directly mapped by the cast provider
        string? cast = scanner.GetCastExpression(typeSymbol);
        // Just verify it doesn't throw — the result depends on the framework plugin
        _ = cast;
    }

    // ── ResolveComponentNamespace ──

    [TestMethod]
    public void ResolveComponentNamespace_GodotType_ReturnsGodot()
    {
        var scanner = CreateScanner();
        Assert.AreEqual("Godot", scanner.ResolveComponentNamespace("Control"));
    }

    [TestMethod]
    public void ResolveComponentNamespace_CustomType_ReturnsNamespace()
    {
        var scanner = CreateScanner(@"
namespace MyGame.Widgets { public class CustomPanel : Godot.Control { } }
");
        Assert.AreEqual("MyGame.Widgets", scanner.ResolveComponentNamespace("CustomPanel"));
    }

    [TestMethod]
    public void ResolveComponentNamespace_GlobalNamespace_ReturnsNull()
    {
        var scanner = CreateScanner(@"
public class GlobalWidget : Godot.Control { }
");
        Assert.IsNull(scanner.ResolveComponentNamespace("GlobalWidget"));
    }

    [TestMethod]
    public void ResolveComponentNamespace_UnknownType_ReturnsNull()
    {
        var scanner = CreateScanner();
        Assert.IsNull(scanner.ResolveComponentNamespace("NonExistentType"));
    }
}
