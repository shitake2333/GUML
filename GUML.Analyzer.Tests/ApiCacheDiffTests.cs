namespace GUML.Analyzer.Tests;

/// <summary>
/// Tests for <see cref="ApiCacheDiff"/> model.
/// </summary>
[TestClass]
public class ApiCacheDiffTests
{
    [TestMethod]
    public void IsEmpty_Default_ReturnsTrue()
    {
        var diff = new ApiCacheDiff();
        Assert.IsTrue(diff.IsEmpty);
    }

    [TestMethod]
    public void IsEmpty_WithUpdatedTypes_ReturnsFalse()
    {
        var diff = new ApiCacheDiff();
        diff.UpdatedTypes.Add("Label");

        Assert.IsFalse(diff.IsEmpty);
    }

    [TestMethod]
    public void IsEmpty_WithRemovedTypes_ReturnsFalse()
    {
        var diff = new ApiCacheDiff();
        diff.RemovedTypes.Add("OldWidget");

        Assert.IsFalse(diff.IsEmpty);
    }

    [TestMethod]
    public void IsEmpty_WithUpdatedControllers_ReturnsFalse()
    {
        var diff = new ApiCacheDiff();
        diff.UpdatedControllers.Add("main.guml");

        Assert.IsFalse(diff.IsEmpty);
    }

    [TestMethod]
    public void IsEmpty_WithRemovedControllers_ReturnsFalse()
    {
        var diff = new ApiCacheDiff();
        diff.RemovedControllers.Add("old.guml");

        Assert.IsFalse(diff.IsEmpty);
    }
}
