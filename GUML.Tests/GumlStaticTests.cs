using Godot;

namespace GUML.Tests;

/// <summary>
/// Tests for <see cref="Guml"/> static class members,
/// covering the fixes introduced by the code review (thread safety, null protection).
/// Tests modify global static state — must not run in parallel.
/// </summary>
[TestClass]
[DoNotParallelize]
public class GumlStaticTests
{
    [TestInitialize]
    public void Initialize()
    {
        // Ensure clean state before each test.
        Guml.ResourceProvider = null!;
        Guml.ControllerRegistry.Clear();
    }

    [TestCleanup]
    public void Cleanup()
    {
        Guml.ResourceProvider = null!;
        Guml.ControllerRegistry.Clear();
    }

    // ── ResourceProvider null protection ──

    [TestMethod]
    public void ResourceProvider_NotSet_ThrowsInvalidOperationException()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            _ = Guml.ResourceProvider;
        });
    }

    [TestMethod]
    public void ResourceProvider_AfterSet_ReturnsInstance()
    {
        var provider = new StubResourceProvider();
        Guml.ResourceProvider = provider;

        Assert.AreSame(provider, Guml.ResourceProvider);
    }

    // ── ControllerRegistry concurrency ──

    [TestMethod]
    public void ControllerRegistry_TryAdd_Succeeds()
    {
        GuiController Factory(Node _) => null!;

        bool added = Guml.ControllerRegistry.TryAdd(typeof(GumlStaticTests), Factory);

        Assert.IsTrue(added);
        Assert.IsTrue(Guml.ControllerRegistry.ContainsKey(typeof(GumlStaticTests)));
    }

    [TestMethod]
    public void ControllerRegistry_DuplicateKey_DoesNotOverwrite()
    {
        GuiController Factory1(Node _) => null!;
        GuiController Factory2(Node _) => null!;

        Guml.ControllerRegistry.TryAdd(typeof(GumlStaticTests), Factory1);
        bool secondAdd = Guml.ControllerRegistry.TryAdd(typeof(GumlStaticTests), Factory2);

        Assert.IsFalse(secondAdd);
        Assert.AreSame(Factory1, Guml.ControllerRegistry[typeof(GumlStaticTests)]);
    }

    // ── Load<T> registry miss ──

    [TestMethod]
    public void Load_UnregisteredType_ThrowsGumlRuntimeException()
    {
        Assert.ThrowsExactly<GumlRuntimeException>(() =>
        {
            Guml.Load<StubController>(null!);
        });
    }

    // ── Test helpers ──

    /// <summary>
    /// Minimal IResourceProvider stub for testing ResourceProvider assignment.
    /// </summary>
    private sealed class StubResourceProvider : IResourceProvider
    {
        public object LoadImage(string path, Node? consumer = null) => null!;
        public object LoadFont(string path, Node? consumer = null) => null!;
        public object LoadAudio(string path, Node? consumer = null) => null!;
        public object LoadVideo(string path, Node? consumer = null) => null!;
    }

    /// <summary>
    /// Minimal controller stub for testing Load&lt;T&gt; registry miss.
    /// </summary>
    internal sealed class StubController : GuiController;
}
