namespace GUML.Analyzer.Tests;

/// <summary>
/// Tests for <see cref="GodotDocParser.ParseClassFile(string)"/>.
/// Uses temporary XML files that mimic the Godot doc/classes format.
/// </summary>
[TestClass]
public class GodotDocParserTests
{
    private string? _tempFile;

    [TestCleanup]
    public void Cleanup()
    {
        if (_tempFile != null && File.Exists(_tempFile))
            File.Delete(_tempFile);
    }

    private string WriteTempXml(string content)
    {
        _tempFile = Path.GetTempFileName();
        // Rename to .xml so it's clear, but GetTempFileName already creates the file
        File.WriteAllText(_tempFile, content);
        return _tempFile;
    }

    [TestMethod]
    public void ParseClassFile_ExtractsInherits()
    {
        string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <class name="Label" inherits="Control">
                <brief_description>A text label.</brief_description>
            </class>
            """;
        string path = WriteTempXml(xml);
        var info = GodotDocParser.ParseClassFile(path);

        Assert.IsNotNull(info);
        Assert.AreEqual("Control", info.Inherits);
    }

    [TestMethod]
    public void ParseClassFile_ExtractsBriefDescription()
    {
        string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <class name="Label" inherits="Control">
                <brief_description>A text label.</brief_description>
            </class>
            """;
        string path = WriteTempXml(xml);
        var info = GodotDocParser.ParseClassFile(path);

        Assert.IsNotNull(info);
        Assert.AreEqual("A text label.", info.BriefDescription);
    }

    [TestMethod]
    public void ParseClassFile_ExtractsProperties()
    {
        string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <class name="Label" inherits="Control">
                <brief_description/>
                <members>
                    <member name="text" type="String" setter="set_text" getter="get_text" default="&quot;&quot;">The label's text.</member>
                    <member name="clip_text" type="bool" setter="set_clip_text" getter="is_clipping_text" default="false">Whether to clip text.</member>
                </members>
            </class>
            """;
        string path = WriteTempXml(xml);
        var info = GodotDocParser.ParseClassFile(path);

        Assert.IsNotNull(info);
        Assert.HasCount(2, info.Properties);

        Assert.AreEqual("text", info.Properties[0].Name);
        Assert.AreEqual("String", info.Properties[0].Type);
        Assert.AreEqual("set_text", info.Properties[0].Setter);
        Assert.AreEqual("get_text", info.Properties[0].Getter);

        Assert.AreEqual("clip_text", info.Properties[1].Name);
        Assert.AreEqual("bool", info.Properties[1].Type);
    }

    [TestMethod]
    public void ParseClassFile_ExtractsSignals()
    {
        string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <class name="Button" inherits="Control">
                <brief_description/>
                <signals>
                    <signal name="pressed">
                        <description>Emitted when pressed.</description>
                    </signal>
                    <signal name="toggled">
                        <description>Emitted when toggled.</description>
                        <param index="0" name="toggled_on" type="bool"/>
                    </signal>
                </signals>
            </class>
            """;
        string path = WriteTempXml(xml);
        var info = GodotDocParser.ParseClassFile(path);

        Assert.IsNotNull(info);
        Assert.HasCount(2, info.Signals);

        Assert.AreEqual("pressed", info.Signals[0].Name);
        Assert.HasCount(0, info.Signals[0].Parameters);

        Assert.AreEqual("toggled", info.Signals[1].Name);
        Assert.HasCount(1, info.Signals[1].Parameters);
        Assert.AreEqual("toggled_on", info.Signals[1].Parameters[0].Name);
        Assert.AreEqual("bool", info.Signals[1].Parameters[0].Type);
    }

    [TestMethod]
    public void ParseClassFile_ExtractsThemeOverrides()
    {
        string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <class name="Label" inherits="Control">
                <brief_description/>
                <theme_items>
                    <theme_item name="font_color" data_type="color" type="Color" default="Color(1,1,1,1)">Font color.</theme_item>
                    <theme_item name="font_size" data_type="font_size" type="int" default="16">Size.</theme_item>
                </theme_items>
            </class>
            """;
        string path = WriteTempXml(xml);
        var info = GodotDocParser.ParseClassFile(path);

        Assert.IsNotNull(info);
        Assert.HasCount(2, info.ThemeOverrides);

        Assert.AreEqual("font_color", info.ThemeOverrides[0].Name);
        Assert.AreEqual("Color", info.ThemeOverrides[0].DataType);

        Assert.AreEqual("font_size", info.ThemeOverrides[1].Name);
        Assert.AreEqual("FontSize", info.ThemeOverrides[1].DataType);
    }

    [TestMethod]
    public void ParseClassFile_ExtractsEnums()
    {
        string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <class name="Control" inherits="CanvasItem">
                <brief_description/>
                <constants>
                    <constant name="ANCHOR_BEGIN" value="0" enum="Anchor">Start anchor.</constant>
                    <constant name="ANCHOR_END" value="1" enum="Anchor">End anchor.</constant>
                </constants>
            </class>
            """;
        string path = WriteTempXml(xml);
        var info = GodotDocParser.ParseClassFile(path);

        Assert.IsNotNull(info);
        Assert.HasCount(1, info.Enums);
        Assert.AreEqual("Anchor", info.Enums[0].Name);
        Assert.HasCount(2, info.Enums[0].Values);
        Assert.AreEqual("ANCHOR_BEGIN", info.Enums[0].Values[0].Name);
        Assert.AreEqual("0", info.Enums[0].Values[0].Value);
    }

    [TestMethod]
    public void ParseClassFile_ExtractsMethods()
    {
        string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <class name="Control" inherits="CanvasItem">
                <brief_description/>
                <methods>
                    <method name="get_rect">
                        <return type="Rect2"/>
                        <description>Returns the control's rect.</description>
                    </method>
                </methods>
            </class>
            """;
        string path = WriteTempXml(xml);
        var info = GodotDocParser.ParseClassFile(path);

        Assert.IsNotNull(info);
        Assert.HasCount(1, info.Methods);
        Assert.AreEqual("get_rect", info.Methods[0].Name);
        Assert.AreEqual("Rect2", info.Methods[0].ReturnType);
    }

    [TestMethod]
    public void ParseClassFile_InvalidRoot_ReturnsNull()
    {
        string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <notaclass name="Label"/>
            """;
        string path = WriteTempXml(xml);
        var info = GodotDocParser.ParseClassFile(path);

        Assert.IsNull(info);
    }
}
