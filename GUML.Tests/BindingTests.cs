using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using GUML.Binding;

namespace GUML.Tests;

#region Test Helpers

/// <summary>
/// Minimal INotifyPropertyChanged implementation for testing DependencyTracker and BindingExpression.
/// </summary>
internal sealed class TestNotifySource : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public void Raise(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

#endregion

#region DependencyTrackerTests

[TestClass]
public class DependencyTrackerTests
{
    [TestMethod]
    public void Subscribe_FiltersPropertyName()
    {
        var source = new TestNotifySource();
        var tracker = new DependencyTracker();
        int callCount = 0;

        tracker.Subscribe(source, new HashSet<string> { "Name" }, () => callCount++);

        source.Raise("Name");
        Assert.AreEqual(1, callCount);

        // "Age" is not tracked, should not trigger callback
        source.Raise("Age");
        Assert.AreEqual(1, callCount);

        source.Raise("Name");
        Assert.AreEqual(2, callCount);

        tracker.Dispose();
    }

    [TestMethod]
    public void Subscribe_EmptyFilter_TriggersOnAnyChange()
    {
        var source = new TestNotifySource();
        var tracker = new DependencyTracker();
        int callCount = 0;

        tracker.Subscribe(source, new HashSet<string>(), () => callCount++);

        source.Raise("Anything");
        Assert.AreEqual(1, callCount);

        source.Raise("SomethingElse");
        Assert.AreEqual(2, callCount);

        tracker.Dispose();
    }

    [TestMethod]
    public void Dispose_UnsubscribesHandler()
    {
        var source = new TestNotifySource();
        var tracker = new DependencyTracker();
        int callCount = 0;

        tracker.Subscribe(source, new HashSet<string> { "Name" }, () => callCount++);

        source.Raise("Name");
        Assert.AreEqual(1, callCount);

        tracker.Dispose();

        // After dispose, changes should not trigger callback
        source.Raise("Name");
        Assert.AreEqual(1, callCount);
    }

    [TestMethod]
    public void Dispose_Idempotent()
    {
        var tracker = new DependencyTracker();
        tracker.Dispose();
        tracker.Dispose(); // Should not throw
    }

    [TestMethod]
    public void Subscribe_AfterDispose_ThrowsObjectDisposed()
    {
        var tracker = new DependencyTracker();
        tracker.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            tracker.Subscribe(new TestNotifySource(), new HashSet<string>(), () => { }));
    }
}

#endregion

#region BindingExpressionTests

[TestClass]
public class BindingExpressionTests
{
    // NOTE: BindingExpression requires a Godot Control reference.
    // The setter-based path does NOT access the Control, so passing null! is safe for those tests.

    [TestMethod]
    public void Apply_WithSetter_InvokesSetter()
    {
        object? captured = "initial";
        var binding = new BindingExpression(
            null!,
            (val) => captured = val,
            () => "hello",
            null,
            new HashSet<string>());

        binding.Apply();

        Assert.AreEqual("hello", captured);
    }

    [TestMethod]
    public void Apply_WithSetter_PassesValueFactoryResult()
    {
        int counter = 0;
        var values = new[] { "first", "second", "third" };

        object? captured = null;
        var binding = new BindingExpression(
            null!,
            (val) => captured = val,
            () => values[counter++],
            null,
            new HashSet<string>());

        binding.Apply();
        Assert.AreEqual("first", captured);

        binding.Apply();
        Assert.AreEqual("second", captured);

        binding.Apply();
        Assert.AreEqual("third", captured);
    }

    [TestMethod]
    public void Activate_EvaluatesInitialValue()
    {
        object? captured = null;
        var binding = new BindingExpression(
            null!,
            (val) => captured = val,
            () => 42,
            null,
            new HashSet<string>());

        binding.Activate();

        Assert.AreEqual(42, captured);
        binding.Dispose();
    }

    [TestMethod]
    public void Activate_SubscribesPropertyChanged()
    {
        var source = new TestNotifySource();
        int applyCount = 0;

        var binding = new BindingExpression(
            null!,
            (_) => applyCount++,
            () => "value",
            source,
            new HashSet<string> { "Name" });

        binding.Activate();
        Assert.AreEqual(1, applyCount, "Activate should call Apply once for initial value.");

        source.Raise("Name");
        Assert.AreEqual(2, applyCount, "PropertyChanged for tracked property should trigger Apply.");

        source.Raise("Untracked");
        Assert.AreEqual(2, applyCount, "PropertyChanged for untracked property should not trigger Apply.");

        binding.Dispose();
    }

    [TestMethod]
    public void Dispose_StopsUpdates()
    {
        var source = new TestNotifySource();
        int applyCount = 0;

        var binding = new BindingExpression(
            null!,
            (_) => applyCount++,
            () => "value",
            source,
            new HashSet<string> { "Name" });

        binding.Activate();
        Assert.AreEqual(1, applyCount);

        binding.Dispose();

        source.Raise("Name");
        Assert.AreEqual(1, applyCount, "After Dispose, changes should not trigger Apply.");
    }

    [TestMethod]
    public void Activate_AfterDispose_ThrowsObjectDisposed()
    {
        var binding = new BindingExpression(
            null!,
            (_) => { },
            () => null,
            null,
            new HashSet<string>());

        binding.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => binding.Activate());
    }

    [TestMethod]
    public void Apply_AfterDispose_IsNoOp()
    {
        object? captured = "initial";
        var binding = new BindingExpression(
            null!,
            (val) => captured = val,
            () => "changed",
            null,
            new HashSet<string>());

        binding.Dispose();

        // Apply after dispose should be a no-op
        binding.Apply();
        Assert.AreEqual("initial", captured);
    }

    [TestMethod]
    public void Activate_WithNullNotifySource_NoSubscription()
    {
        // notifySource=null, dependencies not empty — should still work without erroring
        int applyCount = 0;
        var binding = new BindingExpression(
            null!,
            (_) => applyCount++,
            () => "value",
            null,
            new HashSet<string> { "Name" });

        binding.Activate();
        Assert.AreEqual(1, applyCount, "Should evaluate initial value even without notifySource.");
        binding.Dispose();
    }

    [TestMethod]
    public void Activate_WithEmptyDependencies_NoSubscription()
    {
        // notifySource provided but dependencies empty — should not subscribe
        var source = new TestNotifySource();
        int applyCount = 0;

        var binding = new BindingExpression(
            null!,
            (_) => applyCount++,
            () => "value",
            source,
            new HashSet<string>());

        binding.Activate();
        Assert.AreEqual(1, applyCount);

        source.Raise("Anything");
        // Empty deps with non-null source → no subscription, count should stay 1
        Assert.AreEqual(1, applyCount);
        binding.Dispose();
    }

    [TestMethod]
    public void Dispose_Idempotent()
    {
        var binding = new BindingExpression(
            null!,
            (_) => { },
            () => null,
            null,
            new HashSet<string>());

        binding.Dispose();
        binding.Dispose(); // Should not throw
    }

    [TestMethod]
    public void Activate_WithSetter_NullValueFactory()
    {
        // valueFactory returns null
        object? captured = "initial";
        var binding = new BindingExpression(
            null!,
            (val) => captured = val,
            () => null,
            null,
            new HashSet<string>());

        binding.Activate();
        Assert.IsNull(captured);
        binding.Dispose();
    }
}

#endregion

#region ListBindingTests

[TestClass]
public class ListBindingTests
{
    [TestMethod]
    public void Constructor_SubscribesToStructureChanged()
    {
        var list = new ObservableCollection<string>();
        int callCount = 0;

        var binding = new ListBinding(list, () => callCount++, (_, _) => { });

        list.Add("item");
        Assert.AreEqual(1, callCount);

        binding.Dispose();
    }

    [TestMethod]
    public void Constructor_SubscribesToValueChanged()
    {
        var list = new ObservableCollection<string> { "original" };
        int callCount = 0;
        int? capturedIndex = null;
        object? capturedValue = null;

        var binding = new ListBinding(list, () => { }, (index, value) =>
        {
            callCount++;
            capturedIndex = index;
            capturedValue = value;
        });

        list[0] = "updated";
        Assert.AreEqual(1, callCount);
        Assert.AreEqual(0, capturedIndex);
        Assert.AreEqual("updated", capturedValue);

        binding.Dispose();
    }

    [TestMethod]
    public void Dispose_UnsubscribesFromStructureChanged()
    {
        var list = new ObservableCollection<string>();
        int callCount = 0;

        var binding = new ListBinding(list, () => callCount++, (_, _) => { });

        list.Add("item1");
        Assert.AreEqual(1, callCount);

        binding.Dispose();

        list.Add("item2");
        Assert.AreEqual(1, callCount, "After Dispose, events should not fire.");
    }

    [TestMethod]
    public void Dispose_UnsubscribesFromValueChanged()
    {
        var list = new ObservableCollection<string> { "a" };
        int callCount = 0;

        var binding = new ListBinding(list, () => { }, (_, _) => callCount++);

        list[0] = "b";
        Assert.AreEqual(1, callCount);

        binding.Dispose();

        list[0] = "c";
        Assert.AreEqual(1, callCount, "After Dispose, CollectionChanged (Replace) should not fire.");
    }

    [TestMethod]
    public void Dispose_Idempotent()
    {
        var list = new ObservableCollection<string>();
        var binding = new ListBinding(list, () => { }, (_, _) => { });

        binding.Dispose();
        binding.Dispose(); // Should not throw
    }

    [TestMethod]
    public void StructureHandler_CalledOnAdd()
    {
        var list = new ObservableCollection<int>();
        int callCount = 0;

        var binding = new ListBinding(list, () => callCount++, (_, _) => { });

        list.Add(42);

        Assert.AreEqual(1, callCount);

        binding.Dispose();
    }

    [TestMethod]
    public void ValueHandler_CalledOnIndexerSet()
    {
        var list = new ObservableCollection<int> { 10, 20, 30 };
        int structureCount = 0;
        int? capturedIndex = null;
        object? capturedValue = null;

        var binding = new ListBinding(list, () => structureCount++, (index, value) =>
        {
            capturedIndex = index;
            capturedValue = value;
        });

        list[1] = 99;

        Assert.AreEqual(0, structureCount, "Indexer set should not trigger structure handler.");
        Assert.AreEqual(1, capturedIndex);
        Assert.AreEqual(99, capturedValue);

        binding.Dispose();
    }

    [TestMethod]
    public void Update_SwitchesSource_TriggersStructureHandler()
    {
        var list1 = new ObservableCollection<string> { "a" };
        var list2 = new ObservableCollection<string> { "x", "y" };
        INotifyCollectionChanged current = list1;

        int structureCount = 0;
        var binding = new ListBinding(() => current, () => structureCount++, (_, _) => { });

        // Initial subscription fires nothing; update with same source is no-op
        binding.Update();
        Assert.AreEqual(0, structureCount);

        // Switch to list2 — should fire structure handler once
        current = list2;
        binding.Update();
        Assert.AreEqual(1, structureCount);

        // Old source events should be ignored
        list1.Add("b");
        Assert.AreEqual(1, structureCount);

        // New source events should fire
        list2.Add("z");
        Assert.AreEqual(2, structureCount);

        binding.Dispose();
    }
}

#endregion
