﻿using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace GUML.SourceGenerator.Tests;

[TestClass]
public class GumlCodeEmitterTests
{
    private static GumlDoc ParseGuml(string code)
    {
        var parser = new GumlParser();
        parser.WithConverter(new KeyConverter());
        return parser.Parse(code);
    }

    [TestMethod]
    public void NestedEach_GeneratesEachScopeAndRenderItem()
    {
        string guml = @"Control {
    each $controller.YPos { |yIndex, yValue|
        Control {
            each $controller.XPos { |xIndex, xValue|
                Label {
                    text: ""x index: "" + xIndex + "", y index: "" + yIndex + yValue
            }
        }
    }
}
}";
        var doc = ParseGuml(guml);
        string code = GumlCodeEmitter.Emit("nested_each.guml", doc);

        // Should use EachScope for scope chain
        Assert.Contains("new EachScope(", code,
            "Expected EachScope construction for each blocks.");
        // Should use renderItem delegates
        Assert.Contains("createItem_0", code,
            "Expected createItem_0 delegate for outer each.");
        Assert.Contains("createItem_1", code,
            "Expected createItem_1 delegate for inner each.");
        // Should use refreshEach delegates
        Assert.Contains("Reconcile", code,
            "Expected Reconcile call for outer each.");
        // Inner each should reference outer scope as parent
        Assert.Contains("Lookup(\"yIndex\")", code,
            "Expected EachScope.Lookup for outer variable yIndex in inner each.");
        Assert.Contains("Lookup(\"yValue\")", code,
            "Expected EachScope.Lookup for outer variable yValue in inner each.");
        // Should use incremental Add
        // Actually, with Reconcile we might not explicitly check ListChangedType in the emitted code if the listener is generic,
        // but the current implementation still does "new ListBinding". Let's check that.
        Assert.Contains("new ListBinding", code,
            "Expected ListBinding.");
    }

    [TestMethod]
    public void SingleEach_GeneratesEachScopeWithNullParent()
    {
        string guml = @"Panel {
    each({cache: 5}) $controller.Items { |idx, val|
        Label {
            text: val
        }
    }
}";
        var doc = ParseGuml(guml);
        string code = GumlCodeEmitter.Emit("single_each.guml", doc);

        Assert.Contains("new EachScope(null,", code,
            "Top-level each should use null as parent scope.");
        Assert.Contains("createItem_0", code);
        Assert.Contains("Reconcile", code);
        Assert.Contains("Lookup(\"val\")", code,
            "Expected EachScope.Lookup for each variable.");
    }

    [TestMethod]
    public void SimplePanel_GeneratesCorrectClass()
    {
        string guml = @"Panel {
    size: vec2(640, 480)
}";
        var doc = ParseGuml(guml);
        string code = GumlCodeEmitter.Emit("test.guml", doc);

        Assert.Contains("public partial class TestGumlView", code);
        Assert.Contains("new Panel()", code);
        Assert.Contains("new BindingScope(", code);
        Assert.Contains("new Vector2(640, 480)", code);
    }

    [TestMethod]
    public void NestedComponents_GeneratesChildAdditions()
    {
        string guml = @"Panel {
    Label {
        text: ""hello""
    }
}";
        var doc = ParseGuml(guml);
        string code = GumlCodeEmitter.Emit("main.guml", doc);

        Assert.Contains("public partial class MainGumlView", code);
        Assert.Contains("new Label()", code);
        Assert.Contains("AddChild(", code);
        Assert.Contains(".Text = \"hello\"", code);
    }

    [TestMethod]
    public void SignalBinding_GeneratesEventSubscription()
    {
        string guml = @"Panel {
    Button {
        text: ""click"",
        #pressed: $controller.OnPressed
    }
}";
        var doc = ParseGuml(guml);
        string code = GumlCodeEmitter.Emit("signal_test.guml", doc);

        Assert.Contains(".Pressed += _controller.OnPressed", code);
    }

    [TestMethod]
    public void DataBinding_GeneratesBindingExpression()
    {
        string guml = @"Panel {
    Label {
        text:= $controller.Name
    }
}";
        var doc = ParseGuml(guml);
        string code = GumlCodeEmitter.Emit("binding_test.guml", doc);

        Assert.Contains("BindingExpression", code);
        Assert.Contains("BindingScope", code);
        Assert.Contains("_controller.Name", code);
    }

    [TestMethod]
    public void DataBinding_WithoutScanner_GeneratesReflectionFallback()
    {
        string guml = @"Panel {
    Label {
        text:= $controller.Name
    }
}";
        var doc = ParseGuml(guml);
        // Pass scanner = null explicitly — should fall back to string property name (reflection path)
        string code = GumlCodeEmitter.Emit("binding_test.guml", doc);

        Assert.Contains("\"Text\"", code,
            "Expected string property name 'Text' for reflection fallback.");
        Assert.DoesNotContain("(val) =>", code,
            "Should NOT contain setter lambda when scanner is null.");
    }

    [TestMethod]
    public void DataBinding_WithScanner_GeneratesZeroReflectionSetter()
    {
        string guml = @"Panel {
    Label {
        text:= $controller.Name
    }
}";
        var doc = ParseGuml(guml);

        // Build a compilation with synthetic Godot types so the scanner can resolve Label.Text
        var compilation = CreateGodotCompilation();
        var scanner = new CompilationApiScanner(compilation);

        Assert.IsTrue(scanner.IsAvailable, "Scanner should be available with synthetic Godot types.");

        string code = GumlCodeEmitter.Emit("binding_test.guml", doc, null, scanner);

        Assert.Contains("(val) =>", code,
            "Expected setter lambda for zero-reflection binding.");
        Assert.Contains("(string)val", code,
            "Expected (string)val cast for Label.Text property.");
        Assert.DoesNotContain("\"Text\"", code,
            "Should NOT contain string property name when scanner resolves the type.");
    }

    [TestMethod]
    public void ColorValue_GeneratesCorrectExpression()
    {
        string guml = @"Panel {
    modulate: color(1.0, 0.5, 0.0, 1.0)
}";
        var doc = ParseGuml(guml);
        string code = GumlCodeEmitter.Emit("color_test.guml", doc);

        Assert.Contains("new Color(", code);
    }

    [TestMethod]
    public void Vector2Value_GeneratesCorrectExpression()
    {
        string guml = @"Panel {
    size: vec2(100, 200)
}";
        var doc = ParseGuml(guml);
        string code = GumlCodeEmitter.Emit("vec_test.guml", doc);

        Assert.Contains("new Vector2(100, 200)", code);
    }

    [TestMethod]
    public void BooleanValue_GeneratesCorrectExpression()
    {
        string guml = @"Panel {
    visible: true
}";
        var doc = ParseGuml(guml);
        string code = GumlCodeEmitter.Emit("bool_test.guml", doc);

        Assert.Contains("= true;", code);
    }

    [TestMethod]
    public void IntegerValue_GeneratesCorrectExpression()
    {
        string guml = @"Panel {
    z_index: 5
}";
        var doc = ParseGuml(guml);
        string code = GumlCodeEmitter.Emit("int_test.guml", doc);

        Assert.Contains("= 5;", code);
    }

    [TestMethod]
    public void Vec2iValue_GeneratesCorrectExpression()
    {
        string guml = @"Panel {
    position: vec2i(10, 20)
}";
        var doc = ParseGuml(guml);
        string code = GumlCodeEmitter.Emit("vec2i_test.guml", doc);

        Assert.Contains("new Vector2I(10, 20)", code);
    }

    [TestMethod]
    public void Vec3Value_GeneratesCorrectExpression()
    {
        string guml = @"Panel {
    some_prop: vec3(1, 2, 3)
}";
        var doc = ParseGuml(guml);
        string code = GumlCodeEmitter.Emit("vec3_test.guml", doc);

        Assert.Contains("new Vector3(1, 2, 3)", code);
    }

    [TestMethod]
    public void Rect2Value_GeneratesCorrectExpression()
    {
        string guml = @"Panel {
    some_prop: rect2(0, 0, 100, 200)
}";
        var doc = ParseGuml(guml);
        string code = GumlCodeEmitter.Emit("rect2_test.guml", doc);

        Assert.Contains("new Rect2(0, 0, 100, 200)", code);
    }

    [TestMethod]
    public void EnumValue_GeneratesCorrectExpression()
    {
        string guml = @"Panel {
    horizontal_alignment: .Center
}";
        var doc = ParseGuml(guml);
        string code = GumlCodeEmitter.Emit("enum_test.guml", doc);

        Assert.Contains("Center", code);
    }

    [TestMethod]
    public void NewObj_GeneratesCorrectExpression()
    {
        string guml = @"Label {
    label_settings: new LabelSettings { font_size: 24, outline_size: 2 }
}";
        var doc = ParseGuml(guml);
        string code = GumlCodeEmitter.Emit("newobj_test.guml", doc);

        Assert.Contains("new LabelSettings()", code);
        Assert.Contains("FontSize = 24", code);
        Assert.Contains("OutlineSize = 2", code);
    }

    [TestMethod]
    public void NewObj_Empty_GeneratesCorrectExpression()
    {
        string guml = @"Label {
    label_settings: new LabelSettings { }
}";
        var doc = ParseGuml(guml);
        string code = GumlCodeEmitter.Emit("newobj_empty_test.guml", doc);

        Assert.Contains("new LabelSettings()", code);
    }

    [TestMethod]
    public void Emit_WithAdditionalNamespaces_GeneratesUsingDirectives()
    {
        string guml = "Panel { }";
        var doc = ParseGuml(guml);
        string[] namespaces = new[] { "MyGame.GUI", "MyGame.Widgets" };

        string code = GumlCodeEmitter.Emit("test.guml", doc, namespaces);

        Assert.Contains("using MyGame.GUI;", code,
            "Expected 'using MyGame.GUI;' in generated code.");
        Assert.Contains("using MyGame.Widgets;", code,
            "Expected 'using MyGame.Widgets;' in generated code.");
        // Default usings should still be present
        Assert.Contains("using Godot;", code);
        Assert.Contains("using GUML;", code);
    }

    [TestMethod]
    public void Emit_WithEmptyNamespaces_OnlyDefaultUsings()
    {
        string guml = "Panel { }";
        var doc = ParseGuml(guml);

        string code = GumlCodeEmitter.Emit("test.guml", doc, Array.Empty<string>());

        Assert.Contains("using Godot;", code);
        Assert.Contains("using GUML;", code);
        Assert.DoesNotContain("using MyGame", code,
            "Should not contain custom namespaces when none provided.");
    }

    [TestMethod]
    public void Emit_WithNullNamespaces_OnlyDefaultUsings()
    {
        string guml = "Panel { }";
        var doc = ParseGuml(guml);

        string code = GumlCodeEmitter.Emit("test.guml", doc);

        Assert.Contains("using Godot;", code);
        Assert.DoesNotContain("using MyGame", code);
    }

    [TestMethod]
    public void Emit_FiltersBlankNamespaceEntries()
    {
        string guml = "Panel { }";
        var doc = ParseGuml(guml);
        string[] namespaces = ["MyGame.GUI", "", "  ", "MyGame.Widgets"];

        string code = GumlCodeEmitter.Emit("test.guml", doc, namespaces);

        Assert.Contains("using MyGame.GUI;", code);
        Assert.Contains("using MyGame.Widgets;", code);
        // Should not produce "using ;" or "using    ;"
        Assert.DoesNotContain("using ;", code);
        Assert.DoesNotContain("using  ", code);
    }

    [TestMethod]
    public void EmitImage_GeneratesLoadImage()
    {
        string guml = "Panel { icon: image(\"res://icon.png\") }";
        var doc = ParseGuml(guml);
        string code = GumlCodeEmitter.Emit("res_test.guml", doc);

        Assert.Contains("Guml.ResourceProvider.LoadImage(\"res://icon.png\"", code);
    }

    [TestMethod]
    public void EmitFont_GeneratesLoadFont()
    {
        string guml = "Panel { myFont: font(\"fonts/main.ttf\") }";
        var doc = ParseGuml(guml);
        string code = GumlCodeEmitter.Emit("font_test.guml", doc);

        Assert.Contains("Guml.ResourceProvider.LoadFont(\"fonts/main.ttf\"", code);
    }

    [TestMethod]
    public void EmitAudio_GeneratesLoadAudio()
    {
        string guml = "Panel { clip: audio(\"sfx/click.ogg\") }";
        var doc = ParseGuml(guml);
        string code = GumlCodeEmitter.Emit("audio_test.guml", doc);

        Assert.Contains("Guml.ResourceProvider.LoadAudio(\"sfx/click.ogg\"", code);
    }

    [TestMethod]
    public void EmitVideo_GeneratesLoadVideo()
    {
        string guml = "Panel { stream: video(\"video/intro.ogv\") }";
        var doc = ParseGuml(guml);
        string code = GumlCodeEmitter.Emit("video_test.guml", doc);

        Assert.Contains("Guml.ResourceProvider.LoadVideo(\"video/intro.ogv\"", code);
    }

    [TestMethod]
    public void Emit_WithControllerTypeName_UsesProvidedType()
    {
        string guml = "Panel { }";
        var doc = ParseGuml(guml);

        string code = GumlCodeEmitter.Emit("test.guml", doc, controllerTypeName: "MyCustomController");

        Assert.Contains("MyCustomController _controller", code,
            "Expected custom controller type name in generated code.");
        Assert.Contains("Build(MyCustomController controller)", code);
    }

    [TestMethod]
    public void Emit_ControllerTypeName_OverridesConvention()
    {
        // File is "test.guml" → convention would be "TestController"
        // But we pass "OverrideController" explicitly
        string guml = "Panel { }";
        var doc = ParseGuml(guml);

        string code = GumlCodeEmitter.Emit("test.guml", doc, controllerTypeName: "OverrideController");

        Assert.Contains("OverrideController _controller", code);
        Assert.DoesNotContain("TestController", code,
            "Should NOT use convention-derived controller name when controllerTypeName is provided.");
    }

    [TestMethod]
    public void Emit_WithRegistryKey_GeneratesModuleInitializer()
    {
        string guml = "Panel { }";
        var doc = ParseGuml(guml);

        string code = GumlCodeEmitter.Emit("test.guml", doc,
            controllerTypeName: "TestController",
            gumlRegistryKey: "gui/test.guml");

        Assert.Contains("[System.Runtime.CompilerServices.ModuleInitializer]", code);
        Assert.Contains("internal static void Register()", code);
        Assert.Contains("Guml.ViewRegistry[\"gui/test.guml\"]", code);
    }

    [TestMethod]
    public void Emit_WithoutRegistryKey_NoModuleInitializer()
    {
        string guml = "Panel { }";
        var doc = ParseGuml(guml);

        string code = GumlCodeEmitter.Emit("test.guml", doc);

        Assert.DoesNotContain("ModuleInitializer", code);
        Assert.DoesNotContain("Register()", code);
    }

    [TestMethod]
    public void Emit_AliasNode_GeneratesViewAssignment()
    {
        string guml = @"Panel {
    @hello: Label {
        text: ""world""
    }
}";
        var doc = ParseGuml(guml);

        // Without controllerTypeName - should assign View property but NOT controller property
        string code = GumlCodeEmitter.Emit("test.guml", doc);

        Assert.Contains("this.Hello =", code,
            "Expected alias assignment to View property.");
        Assert.DoesNotContain("_controller.Hello =", code,
            "Without explicit controllerTypeName, should not assign to controller.");
    }

    [TestMethod]
    public void Emit_AliasNode_WithControllerTypeName_GeneratesBothAssignments()
    {
        string guml = @"Panel {
    @hello: Label {
        text: ""world""
    }
}";
        var doc = ParseGuml(guml);

        string code = GumlCodeEmitter.Emit("test.guml", doc, controllerTypeName: "TestController");

        Assert.Contains("this.Hello =", code,
            "Expected alias assignment to View property.");
        Assert.Contains("_controller.Hello =", code,
            "Expected alias assignment to Controller property.");
    }

    [TestMethod]
    public void EmitControllerPartial_GeneratesTypedProperties()
    {
        string guml = @"Panel {
    @hello: Label {
        text: ""world""
    }
    @myBtn: Button {
        text: ""click""
    }
}";
        var doc = ParseGuml(guml);

        string? code = GumlCodeEmitter.EmitControllerPartial("TestController", null, doc);

        Assert.IsNotNull(code, "Expected non-null output for doc with aliases.");
        Assert.Contains("public partial class TestController", code);
        Assert.Contains("public Label Hello { get; internal set; }", code);
        Assert.Contains("public Button MyBtn { get; internal set; }", code);
    }

    [TestMethod]
    public void EmitControllerPartial_NoAliases_ReturnsNull()
    {
        string guml = @"Panel {
    Label {
        text: ""world""
    }
}";
        var doc = ParseGuml(guml);

        string? code = GumlCodeEmitter.EmitControllerPartial("TestController", null, doc);

        Assert.IsNull(code, "Expected null for doc without aliases.");
    }

    [TestMethod]
    public void EmitControllerPartial_WithNamespace_WrapsInNamespace()
    {
        string guml = @"Panel {
    @hello: Label {
        text: ""world""
    }
}";
        var doc = ParseGuml(guml);

        string? code = GumlCodeEmitter.EmitControllerPartial("TestController", "MyGame.Controllers", doc);

        Assert.IsNotNull(code);
        Assert.Contains("namespace MyGame.Controllers;", code);
        Assert.Contains("public partial class TestController", code);
    }

    [TestMethod]
    public void EmitControllerPartial_GlobalNamespace_OmitsNamespace()
    {
        string guml = @"Panel {
    @hello: Label {
        text: ""world""
    }
}";
        var doc = ParseGuml(guml);

        string? code = GumlCodeEmitter.EmitControllerPartial("TestController", null, doc);

        Assert.IsNotNull(code);
        Assert.DoesNotContain("namespace", code);
    }

    /// <summary>
    /// Creates a CSharpCompilation with synthetic Godot types (GodotObject, Node, CanvasItem, Control, Label)
    /// so that <see cref="CompilationApiScanner"/> can resolve property types.
    /// </summary>
    private static CSharpCompilation CreateGodotCompilation()
    {
        string godotStubs = @"
namespace Godot
{
    public class GodotObject { }
    public class Node : GodotObject { }
    public class CanvasItem : Node { }
    public class Control : CanvasItem
    {
        public bool Visible { get; set; }
        public float Size { get; set; }
    }
    public class Label : Control
    {
        public string Text { get; set; }
        public bool ClipText { get; set; }
    }
    public class Button : Control
    {
        public string Text { get; set; }
    }
    public class Panel : Control { }
}
";
        var syntaxTree = CSharpSyntaxTree.ParseText(godotStubs);
        return CSharpCompilation.Create("GodotStubs",
            new[] { syntaxTree },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}

[TestClass]
public class GumlSourceGeneratorTests
{
    /// <summary>
    /// Creates a compilation containing the GumlControllerAttribute and Godot stubs,
    /// plus optional user source code with a specified file path.
    /// </summary>
    private static CSharpCompilation CreateControllerCompilation(
        string userSource, string userSourceFilePath, string? godotStubs = null)
    {
        string attrSource = @"
using System;
namespace GUML.Shared
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class GumlControllerAttribute : Attribute
    {
        public string GumlPath { get; }
        public GumlControllerAttribute(string gumlPath) { GumlPath = gumlPath; }
    }
}

namespace GUML
{
    public abstract class GuiController : System.ComponentModel.INotifyPropertyChanged
    {
        public Godot.Control GumlRootNode;
        internal object RootBindingScope { get; set; }
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        public virtual void Created() { }
    }

    public class BindingScope : IDisposable
    {
        public BindingScope(Godot.Control target) { }
        public void Add(object binding) { }
        public void Dispose() { }
    }

    public class BindingExpression
    {
        public BindingExpression(Godot.Control target, string propertyName, Func<object> valueGetter, object controller, System.Collections.Generic.HashSet<string> deps) { }
        public BindingExpression(Godot.Control target, Action<object> setter, Func<object> valueGetter, object controller, System.Collections.Generic.HashSet<string> deps) { }
        public void Activate() { }
    }

    public static class Guml
    {
        public static readonly System.Collections.Generic.Dictionary<string, Func<Godot.Node, GuiController>> ViewRegistry = new();
        public static object ResourceProvider;
        public static readonly System.Collections.Generic.Dictionary<string, object> GlobalRefs = new();
    }
}
";
        godotStubs ??= @"
namespace Godot
{
    public class GodotObject { }
    public class Node : GodotObject { }
    public class CanvasItem : Node
    {
        public void AddChild(Node child) { }
    }
    public class Control : CanvasItem
    {
        public bool Visible { get; set; }
    }
    public class Label : Control
    {
        public string Text { get; set; }
        public bool ClipText { get; set; }
    }
    public class Button : Control
    {
        public string Text { get; set; }
    }
    public class Panel : Control { }
}
";

        var attrTree = CSharpSyntaxTree.ParseText(attrSource, path: "GUML/GumlControllerAttribute.cs");
        var godotTree = CSharpSyntaxTree.ParseText(godotStubs, path: "Godot/Stubs.cs");
        var userTree = CSharpSyntaxTree.ParseText(userSource, path: userSourceFilePath);

        return CSharpCompilation.Create("TestCompilation",
            new[] { attrTree, godotTree, userTree },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.ComponentModel.INotifyPropertyChanged).Assembly
                    .Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Finds generated source containing a specific string across all generated trees.
    /// </summary>
    private static string? FindGeneratedSource(GeneratorDriverRunResult results, string contains)
    {
        foreach (var tree in results.GeneratedTrees)
        {
            string source = tree.GetText().ToString();
            if (source.Contains(contains))
            {
                return source;
            }
        }

        return null;
    }

    [TestMethod]
    public void Generator_WithGumlControllerAttribute_EmitsViewClass()
    {
        string gumlContent = @"Panel {
    size: vec2(640, 480)
}";
        string controllerSource = @"
using GUML;
using GUML.Shared;
[GumlController(""../../gui/test.guml"")]
public partial class TestController : GuiController { }
";
        // Controller is at project/scripts/controllers/TestController.cs
        // guml path resolves to project/gui/test.guml
        string controllerPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "project", "scripts", "controllers",
            "TestController.cs"));
        string gumlAbsolutePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "project", "gui", "test.guml"));

        var compilation = CreateControllerCompilation(controllerSource, controllerPath);
        var additionalText = new InMemoryAdditionalText(gumlAbsolutePath, gumlContent);

        var generator = new GumlSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator)
            .AddAdditionalTexts(new[] { (AdditionalText)additionalText }.ToImmutableArray());

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var results = driver.GetRunResult();

        string? viewSource = FindGeneratedSource(results, "TestGumlView");
        Assert.IsNotNull(viewSource, "Expected TestGumlView to be generated.");
        Assert.Contains("public partial class TestGumlView", viewSource);
        Assert.Contains("TestController _controller", viewSource);
        Assert.Contains("Build(TestController controller)", viewSource);
    }

    [TestMethod]
    public void Generator_WithAttribute_GeneratesRegisterMethod()
    {
        string gumlContent = @"Panel { }";
        string controllerSource = @"
using GUML;
using GUML.Shared;
[GumlController(""../../gui/test.guml"")]
public partial class TestController : GuiController { }
";
        string controllerPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "project", "scripts", "controllers",
            "TestController.cs"));
        string gumlAbsolutePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "project", "gui", "test.guml"));

        var compilation = CreateControllerCompilation(controllerSource, controllerPath);
        var additionalText = new InMemoryAdditionalText(gumlAbsolutePath, gumlContent);

        var generator = new GumlSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator)
            .AddAdditionalTexts(new[] { (AdditionalText)additionalText }.ToImmutableArray());

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var results = driver.GetRunResult();

        string? viewSource = FindGeneratedSource(results, "TestGumlView");
        Assert.IsNotNull(viewSource, "Expected TestGumlView to be generated.");
        Assert.Contains("[System.Runtime.CompilerServices.ModuleInitializer]", viewSource);
        Assert.Contains("internal static void Register()", viewSource);
        Assert.Contains("Guml.ViewRegistry[", viewSource);
        Assert.Contains("new TestController()", viewSource);
        Assert.Contains("new TestGumlView()", viewSource);
    }

    [TestMethod]
    public void Generator_WithAttribute_GeneratesControllerPartial()
    {
        string gumlContent = @"Panel {
    @hello: Label {
        text: ""world""
    }
}";
        string controllerSource = @"
using GUML;
using GUML.Shared;
[GumlController(""../../gui/main.guml"")]
public partial class MainController : GuiController { }
";
        string controllerPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "project", "scripts", "controllers",
            "MainController.cs"));
        string gumlAbsolutePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "project", "gui", "main.guml"));

        var compilation = CreateControllerCompilation(controllerSource, controllerPath);
        var additionalText = new InMemoryAdditionalText(gumlAbsolutePath, gumlContent);

        var generator = new GumlSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator)
            .AddAdditionalTexts(new[] { (AdditionalText)additionalText }.ToImmutableArray());

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var results = driver.GetRunResult();

        // Check controller partial class
        string? partialSource = FindGeneratedSource(results, "public partial class MainController");
        Assert.IsNotNull(partialSource, "Expected MainController partial class to be generated.");
        Assert.Contains("public Label Hello { get; internal set; }", partialSource);
        Assert.Contains("Named node '@hello'", partialSource);
    }

    [TestMethod]
    public void Generator_AliasNode_GeneratesAssignmentInView()
    {
        string gumlContent = @"Panel {
    @hello: Label {
        text: ""world""
    }
}";
        string controllerSource = @"
using GUML;
using GUML.Shared;
[GumlController(""../../gui/main.guml"")]
public partial class MainController : GuiController { }
";
        string controllerPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "project", "scripts", "controllers",
            "MainController.cs"));
        string gumlAbsolutePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "project", "gui", "main.guml"));

        var compilation = CreateControllerCompilation(controllerSource, controllerPath);
        var additionalText = new InMemoryAdditionalText(gumlAbsolutePath, gumlContent);

        var generator = new GumlSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator)
            .AddAdditionalTexts(new[] { (AdditionalText)additionalText }.ToImmutableArray());

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var results = driver.GetRunResult();

        string? viewSource = FindGeneratedSource(results, "MainGumlView");
        Assert.IsNotNull(viewSource, "Expected MainGumlView to be generated.");
        // View should assign alias to both View property and Controller property
        Assert.Contains("this.Hello =", viewSource,
            "Expected alias assignment to View property.");
        Assert.Contains("_controller.Hello =", viewSource,
            "Expected alias assignment to Controller property.");
    }

    [TestMethod]
    public void Generator_GumlFileNotFound_ReportsError()
    {
        string controllerSource = @"
using GUML;
using GUML.Shared;
[GumlController(""../../gui/missing.guml"")]
public partial class MissingController : GuiController { }
";
        string controllerPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "project", "scripts", "controllers",
            "MissingController.cs"));

        var compilation = CreateControllerCompilation(controllerSource, controllerPath);
        // No additional files provided - the .guml file is missing

        var generator = new GumlSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        // Should report GUML005 error
        bool hasGuml005 = false;
        foreach (var diag in diagnostics)
        {
            if (diag.Id == "GUML005")
            {
                hasGuml005 = true;
                Assert.AreEqual(DiagnosticSeverity.Error, diag.Severity);
                break;
            }
        }

        Assert.IsTrue(hasGuml005, "Expected GUML005 diagnostic for missing .guml file.");
    }

    [TestMethod]
    public void Generator_NonPartialController_ReportsWarning()
    {
        string gumlContent = @"Panel {
    @hello: Label {
        text: ""world""
    }
}";
        // Note: NOT partial
        string controllerSource = @"
using GUML;
using GUML.Shared;
[GumlController(""../../gui/main.guml"")]
public class NonPartialController : GuiController { }
";
        string controllerPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "project", "scripts", "controllers",
            "NonPartialController.cs"));
        string gumlAbsolutePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "project", "gui", "main.guml"));

        var compilation = CreateControllerCompilation(controllerSource, controllerPath);
        var additionalText = new InMemoryAdditionalText(gumlAbsolutePath, gumlContent);

        var generator = new GumlSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator)
            .AddAdditionalTexts(new[] { (AdditionalText)additionalText }.ToImmutableArray());

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        // Should report GUML007 warning
        bool hasGuml007 = false;
        foreach (var diag in diagnostics)
        {
            if (diag.Id == "GUML007")
            {
                hasGuml007 = true;
                Assert.AreEqual(DiagnosticSeverity.Warning, diag.Severity);
                break;
            }
        }

        Assert.IsTrue(hasGuml007, "Expected GUML007 diagnostic for non-partial controller.");

        // View class should still be generated
        var results = driver.GetRunResult();
        string? viewSource = FindGeneratedSource(results, "MainGumlView");
        Assert.IsNotNull(viewSource, "Expected view class to still be generated for non-partial controller.");
    }

    [TestMethod]
    public void ParseNamespaces_SplitsSemicolonSeparatedValues()
    {
        string[] result = GumlSourceGenerator.ParseNamespaces("MyGame.GUI;MyGame.Widgets");
        Assert.HasCount(2, result);
        Assert.AreEqual("MyGame.GUI", result[0]);
        Assert.AreEqual("MyGame.Widgets", result[1]);
    }

    [TestMethod]
    public void ParseNamespaces_FiltersEmptyAndWhitespaceEntries()
    {
        string[] result = GumlSourceGenerator.ParseNamespaces("MyGame.GUI;;  ; MyGame.Widgets ; ");
        Assert.HasCount(2, result);
        Assert.AreEqual("MyGame.GUI", result[0]);
        Assert.AreEqual("MyGame.Widgets", result[1]);
    }

    [TestMethod]
    public void ParseNamespaces_ReturnsEmptyForNullOrWhitespace()
    {
        Assert.IsEmpty(GumlSourceGenerator.ParseNamespaces(""));
        Assert.IsEmpty(GumlSourceGenerator.ParseNamespaces("   "));
    }

    [TestMethod]
    public void NormalizeRegistryKey_NormalizesPathSeparators()
    {
        string result = GumlSourceGenerator.NormalizeRegistryKey(@"C:\project\gui\subdir\test.guml", @"C:\project");
        Assert.AreEqual("gui/subdir/test.guml", result);
    }

    [TestMethod]
    public void NormalizeRegistryKey_LowercasesPath()
    {
        string result = GumlSourceGenerator.NormalizeRegistryKey("C:/Project/GUI/Test.guml", "C:/Project");
        Assert.AreEqual("gui/test.guml", result);
    }

    [TestMethod]
    public void NormalizeRegistryKey_FallsBackWhenNoProjectDir()
    {
        string result = GumlSourceGenerator.NormalizeRegistryKey(@"D:\full\path\test.guml", "");
        Assert.AreEqual("d:/full/path/test.guml", result);
    }
}

[TestClass]
public class CompilationApiScannerTests
{
    /// <summary>
    /// Creates a CSharpCompilation with synthetic Godot types for scanner testing.
    /// </summary>
    private static CSharpCompilation CreateGodotCompilation(string? extraSource = null)
    {
        string godotStubs = @"
namespace Godot
{
    public class GodotObject { }
    public class Node : GodotObject { }
    public class CanvasItem : Node { }
    public class Control : CanvasItem
    {
        public bool Visible { get; set; }
    }
    public class Label : Control
    {
        public string Text { get; set; }
        public bool ClipText { get; set; }
    }
    public class Button : Control
    {
        public string Text { get; set; }
    }
    public class Panel : Control { }
}
";
        var trees = new List<SyntaxTree> { CSharpSyntaxTree.ParseText(godotStubs) };
        if (extraSource != null)
        {
            trees.Add(CSharpSyntaxTree.ParseText(extraSource));
        }

        return CSharpCompilation.Create("GodotStubs",
            trees,
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    [TestMethod]
    public void IsAvailable_ReturnsTrueWhenGodotControlExists()
    {
        var compilation = CreateGodotCompilation();
        var scanner = new CompilationApiScanner(compilation);
        Assert.IsTrue(scanner.IsAvailable);
    }

    [TestMethod]
    public void IsAvailable_ReturnsFalseWhenNoGodotReference()
    {
        var compilation = CSharpCompilation.Create("Empty",
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var scanner = new CompilationApiScanner(compilation);
        Assert.IsFalse(scanner.IsAvailable);
    }

    [TestMethod]
    public void ResolvePropertyType_ReturnsStringForLabelText()
    {
        var compilation = CreateGodotCompilation();
        var scanner = new CompilationApiScanner(compilation);

        var typeSymbol = scanner.ResolvePropertyType("Label", "Text");
        Assert.IsNotNull(typeSymbol);
        Assert.AreEqual(SpecialType.System_String, typeSymbol.SpecialType);
    }

    [TestMethod]
    public void ResolvePropertyType_ReturnsBoolForLabelClipText()
    {
        var compilation = CreateGodotCompilation();
        var scanner = new CompilationApiScanner(compilation);

        var typeSymbol = scanner.ResolvePropertyType("Label", "ClipText");
        Assert.IsNotNull(typeSymbol);
        Assert.AreEqual(SpecialType.System_Boolean, typeSymbol.SpecialType);
    }

    [TestMethod]
    public void ResolvePropertyType_ReturnsInheritedProperty()
    {
        var compilation = CreateGodotCompilation();
        var scanner = new CompilationApiScanner(compilation);

        // Label inherits Visible from Control
        var typeSymbol = scanner.ResolvePropertyType("Label", "Visible");
        Assert.IsNotNull(typeSymbol);
        Assert.AreEqual(SpecialType.System_Boolean, typeSymbol.SpecialType);
    }

    [TestMethod]
    public void ResolvePropertyType_ReturnsNullForUnknownProperty()
    {
        var compilation = CreateGodotCompilation();
        var scanner = new CompilationApiScanner(compilation);

        var typeSymbol = scanner.ResolvePropertyType("Label", "NonExistent");
        Assert.IsNull(typeSymbol);
    }

    [TestMethod]
    public void ResolvePropertyType_ReturnsNullForUnknownComponent()
    {
        var compilation = CreateGodotCompilation();
        var scanner = new CompilationApiScanner(compilation);

        var typeSymbol = scanner.ResolvePropertyType("UnknownWidget", "Text");
        Assert.IsNull(typeSymbol);
    }

    [TestMethod]
    public void GetCastExpression_MapsStringType()
    {
        var compilation = CreateGodotCompilation();
        var scanner = new CompilationApiScanner(compilation);
        var typeSymbol = scanner.ResolvePropertyType("Label", "Text")!;

        string? cast = CompilationApiScanner.GetCastExpression(typeSymbol);
        Assert.AreEqual("(string)", cast);
    }

    [TestMethod]
    public void GetCastExpression_MapsBoolType()
    {
        var compilation = CreateGodotCompilation();
        var scanner = new CompilationApiScanner(compilation);
        var typeSymbol = scanner.ResolvePropertyType("Label", "ClipText")!;

        string? cast = CompilationApiScanner.GetCastExpression(typeSymbol);
        Assert.AreEqual("(bool)", cast);
    }
}

/// <summary>
/// In-memory implementation of AdditionalText for testing.
/// </summary>
internal sealed class InMemoryAdditionalText(string path, string text) : AdditionalText
{
    public override string Path { get; } = path;

    public override SourceText GetText(CancellationToken cancellationToken = default)
    {
        return SourceText.From(text, System.Text.Encoding.UTF8);
    }
}

internal static class ImmutableArrayExtensions
{
    public static ImmutableArray<T> ToImmutableArray<T>(this T[] array)
    {
        return [.. array];
    }
}
