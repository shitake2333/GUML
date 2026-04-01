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

    // Use unique sentinel types as keys to avoid cross-test interference
    // through the static ConcurrentDictionary, even if TestInitialize is skipped.
    private sealed class RegistryKey_TryAdd;
    private sealed class RegistryKey_Duplicate;

    [TestMethod]
    public void ControllerRegistry_TryAdd_Succeeds()
    {
        GuiController Factory(Node _) => null!;

        bool added = Guml.ControllerRegistry.TryAdd(typeof(RegistryKey_TryAdd), Factory);

        Assert.IsTrue(added);
        Assert.IsTrue(Guml.ControllerRegistry.ContainsKey(typeof(RegistryKey_TryAdd)));
    }

    [TestMethod]
    public void ControllerRegistry_DuplicateKey_DoesNotOverwrite()
    {
        Func<Node, GuiController> factory1 = _ => null!;
        Func<Node, GuiController> factory2 = _ => null!;

        bool firstAdd = Guml.ControllerRegistry.TryAdd(typeof(RegistryKey_Duplicate), factory1);
        Assert.IsTrue(firstAdd, "First TryAdd should succeed on a clean registry.");

        bool secondAdd = Guml.ControllerRegistry.TryAdd(typeof(RegistryKey_Duplicate), factory2);

        Assert.IsFalse(secondAdd);
        Assert.AreSame(factory1, Guml.ControllerRegistry[typeof(RegistryKey_Duplicate)]);
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
