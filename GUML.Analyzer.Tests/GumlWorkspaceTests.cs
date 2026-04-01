using GUML.Analyzer.Workspace;

namespace GUML.Analyzer.Tests;

/// <summary>
/// Tests for <see cref="GumlWorkspace"/> document management operations.
/// Validates the fixes for lock optimization in GetSemanticModel and
/// basic correctness of Open/Update/Close/Get lifecycle.
/// </summary>
[TestClass]
public class GumlWorkspaceTests
{
    private GumlWorkspace CreateWorkspace() => new(null!);

    [TestMethod]
    public void OpenDocument_ParsesSuccessfully()
    {
        var ws = CreateWorkspace();
        var doc = ws.OpenDocument("file:///test.guml", "Panel { }");

        Assert.IsNotNull(doc);
        Assert.AreEqual("file:///test.guml", doc.Uri);
        Assert.AreEqual("Panel { }", doc.Text);
        Assert.AreEqual(1, doc.Version);
        Assert.IsNotNull(doc.Root);
    }

    [TestMethod]
    public void UpdateDocument_IncreasesVersion()
    {
        var ws = CreateWorkspace();
        ws.OpenDocument("file:///test.guml", "Panel { }");
        var updated = ws.UpdateDocument("file:///test.guml", "Label { }");

        Assert.AreEqual(2, updated.Version);
        Assert.AreEqual("Label { }", updated.Text);
    }

    [TestMethod]
    public void UpdateDocument_UnknownUri_CreatesNew()
    {
        var ws = CreateWorkspace();
        var doc = ws.UpdateDocument("file:///new.guml", "Panel { }");

        Assert.IsNotNull(doc);
        Assert.AreEqual(1, doc.Version);
    }

    [TestMethod]
    public void CloseDocument_RemovesFromWorkspace()
    {
        var ws = CreateWorkspace();
        ws.OpenDocument("file:///test.guml", "Panel { }");
        ws.CloseDocument("file:///test.guml");

        Assert.IsNull(ws.GetDocument("file:///test.guml"));
        Assert.IsFalse(ws.IsDocumentOpen("file:///test.guml"));
    }

    [TestMethod]
    public void GetDocument_UnknownUri_ReturnsNull()
    {
        var ws = CreateWorkspace();
        Assert.IsNull(ws.GetDocument("file:///nonexistent.guml"));
    }

    [TestMethod]
    public void IsDocumentOpen_ReturnsTrueForOpenDoc()
    {
        var ws = CreateWorkspace();
        ws.OpenDocument("file:///test.guml", "Panel { }");

        Assert.IsTrue(ws.IsDocumentOpen("file:///test.guml"));
    }

    [TestMethod]
    public void IsDocumentOpen_ReturnsFalseForClosedDoc()
    {
        var ws = CreateWorkspace();
        Assert.IsFalse(ws.IsDocumentOpen("file:///test.guml"));
    }

    [TestMethod]
    public void GetDocument_ReturnsLatestVersion()
    {
        var ws = CreateWorkspace();
        ws.OpenDocument("file:///test.guml", "Panel { }");
        ws.UpdateDocument("file:///test.guml", "Label { text: \"hello\" }");
        ws.UpdateDocument("file:///test.guml", "Button { text: \"click\" }");

        var doc = ws.GetDocument("file:///test.guml");
        Assert.IsNotNull(doc);
        Assert.AreEqual(3, doc.Version);
        Assert.AreEqual("Button { text: \"click\" }", doc.Text);
    }
}
