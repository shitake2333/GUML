using System.ComponentModel;

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

/// <summary>
/// Concrete GuiController subclass for testing BindingContext.
/// The base constructor does not call Godot APIs, so this is safe in pure .NET tests.
/// </summary>
internal sealed class TestController : GuiController
{
    private string _name = "";

    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged();
        }
    }

    private int _count;

    public int Count
    {
        get => _count;
        set
        {
            _count = value;
            OnPropertyChanged();
        }
    }
}

#endregion

#region BindingContextTests

[TestClass]
public class BindingContextTests
{
    [TestMethod]
    public void Constructor_InitializesProperties()
    {
        var controller = new TestController();
        var doc = new GumlDoc();
        var globals = new Dictionary<string, object> { ["$controller"] = controller };

        var ctx = new BindingContext(controller, doc, globals);

        Assert.AreSame(controller, ctx.Controller);
        Assert.AreEqual(doc, ctx.Document);
        Assert.AreSame(globals, ctx.GlobalRefs);
        Assert.AreEqual(0, ctx.LocalStack.Count);
    }

    [TestMethod]
    public void LocalStack_PushPop_MaintainsScope()
    {
        var ctx = new BindingContext(new TestController(), new GumlDoc(), new Dictionary<string, object>());

        var scope1 = new Dictionary<string, object?> { ["item"] = "a" };
        var scope2 = new Dictionary<string, object?> { ["item"] = "b" };

        ctx.LocalStack.Push(scope1);
        ctx.LocalStack.Push(scope2);

        Assert.AreEqual(2, ctx.LocalStack.Count);
        Assert.AreSame(scope2, ctx.LocalStack.Peek());

        ctx.LocalStack.Pop();
        Assert.AreEqual(1, ctx.LocalStack.Count);
        Assert.AreSame(scope1, ctx.LocalStack.Peek());
    }

    [TestMethod]
    public void Snapshot_CopiesGlobalRefs()
    {
        var globals = new Dictionary<string, object> { ["key1"] = "val1" };
        var ctx = new BindingContext(new TestController(), new GumlDoc(), globals);

        var snapshot = ctx.Snapshot();

        // Snapshot should have a copy, not the same reference
        Assert.AreNotSame(globals, snapshot.GlobalRefs);
        Assert.AreEqual("val1", snapshot.GlobalRefs["key1"]);

        // Mutating original does not affect snapshot
        globals["key2"] = "val2";
        Assert.IsFalse(snapshot.GlobalRefs.ContainsKey("key2"));
    }

    [TestMethod]
    public void Snapshot_CopiesLocalStack()
    {
        var ctx = new BindingContext(new TestController(), new GumlDoc(), new Dictionary<string, object>());
        ctx.LocalStack.Push(new Dictionary<string, object?> { ["x"] = 42 });

        var snapshot = ctx.Snapshot();

        // Snapshot should have its own stack
        Assert.AreEqual(1, snapshot.LocalStack.Count);
        Assert.AreEqual(42, snapshot.LocalStack.Peek()["x"]);

        // Mutating original stack does not affect snapshot
        ctx.LocalStack.Push(new Dictionary<string, object?> { ["y"] = 99 });
        Assert.AreEqual(1, snapshot.LocalStack.Count);
    }

    [TestMethod]
    public void Snapshot_SharesControllerReference()
    {
        var controller = new TestController();
        var ctx = new BindingContext(controller, new GumlDoc(), new Dictionary<string, object>());

        var snapshot = ctx.Snapshot();

        Assert.AreSame(controller, snapshot.Controller);
    }

    [TestMethod]
    public void Snapshot_IndependentDocument()
    {
        var doc1 = new GumlDoc { Redirect = "original" };
        var ctx = new BindingContext(new TestController(), doc1, new Dictionary<string, object>());

        var snapshot = ctx.Snapshot();

        // Reassigning Document on snapshot should not affect original (GumlDoc is a struct, so assignment copies)
        var doc2 = new GumlDoc { Redirect = "changed" };
        snapshot.Document = doc2;

        Assert.AreEqual("original", ctx.Document.Redirect);
        Assert.AreEqual("changed", snapshot.Document.Redirect);
    }
}

#endregion

#region DependencyTrackerTests

[TestClass]
public class DependencyTrackerTests
{
    /// <summary>
    /// Builds: $controller.Name (GlobalRef -> PropertyRef chain)
    /// </summary>
    private static GumlValueNode MakeControllerPropertyRef(string propertyName)
    {
        var globalRef = new GumlValueNode
        {
            ValueType = GumlValueType.Ref,
            RefType = RefType.GlobalRef,
            RefName = "$controller"
        };
        return new GumlValueNode
        {
            ValueType = GumlValueType.Ref,
            RefType = RefType.PropertyRef,
            RefName = propertyName,
            RefNode = globalRef
        };
    }

    /// <summary>
    /// Builds a simple literal int node.
    /// </summary>
    private static GumlValueNode MakeLiteral(int value)
    {
        return new GumlValueNode
        {
            ValueType = GumlValueType.Int,
            IntValue = value
        };
    }

    [TestMethod]
    public void CollectDependencies_GlobalPropertyRef_ReturnsDependency()
    {
        // $controller.Name
        var node = MakeControllerPropertyRef("Name");
        var deps = DependencyTracker.CollectDependencies(node);

        Assert.AreEqual(1, deps.Count);
        Assert.IsTrue(deps.Contains("Name"));
    }

    [TestMethod]
    public void CollectDependencies_LocalRef_ReturnsEmpty()
    {
        // local variable reference (not a property of global ref)
        var localRef = new GumlValueNode
        {
            ValueType = GumlValueType.Ref,
            RefType = RefType.LocalRef,
            RefName = "item"
        };

        var deps = DependencyTracker.CollectDependencies(localRef);
        Assert.AreEqual(0, deps.Count);
    }

    [TestMethod]
    public void CollectDependencies_InfixOp_CollectsBothSides()
    {
        // $controller.A + $controller.B
        var left = MakeControllerPropertyRef("A");
        var right = MakeControllerPropertyRef("B");
        var infix = new InfixOpNode { Op = "+" };
        infix.Left = left;
        infix.Right = right;

        var deps = DependencyTracker.CollectDependencies(infix);

        Assert.AreEqual(2, deps.Count);
        Assert.IsTrue(deps.Contains("A"));
        Assert.IsTrue(deps.Contains("B"));
    }

    [TestMethod]
    public void CollectDependencies_PrefixOp_CollectsOperand()
    {
        // !$controller.Flag
        var operand = MakeControllerPropertyRef("Flag");
        var prefix = new PrefixOpNode { Op = "!" };
        prefix.Right = operand;

        var deps = DependencyTracker.CollectDependencies(prefix);

        Assert.AreEqual(1, deps.Count);
        Assert.IsTrue(deps.Contains("Flag"));
    }

    [TestMethod]
    public void CollectDependencies_NestedVector2_CollectsAll()
    {
        // vec2($controller.X, $controller.Y)
        var xRef = MakeControllerPropertyRef("X");
        var yRef = MakeControllerPropertyRef("Y");
        var vec2Node = new GumlValueNode
        {
            ValueType = GumlValueType.Vector2,
            Vector2XNode = xRef,
            Vector2YNode = yRef
        };

        var deps = DependencyTracker.CollectDependencies(vec2Node);

        Assert.AreEqual(2, deps.Count);
        Assert.IsTrue(deps.Contains("X"));
        Assert.IsTrue(deps.Contains("Y"));
    }

    [TestMethod]
    public void CollectDependencies_NoDeps_ReturnsEmpty()
    {
        // literal 42
        var literal = MakeLiteral(42);
        var deps = DependencyTracker.CollectDependencies(literal);
        Assert.AreEqual(0, deps.Count);
    }

    [TestMethod]
    public void CollectDependencies_DuplicateRefs_Deduplicated()
    {
        // $controller.Name + $controller.Name → {"Name"} (not 2 entries)
        var left = MakeControllerPropertyRef("Name");
        var right = MakeControllerPropertyRef("Name");
        var infix = new InfixOpNode { Op = "+" };
        infix.Left = left;
        infix.Right = right;

        var deps = DependencyTracker.CollectDependencies(infix);
        Assert.AreEqual(1, deps.Count);
    }

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

    // ========================================================================
    // AST path coverage: Color, Object, Resource, StyleBox nodes
    // ========================================================================

    [TestMethod]
    public void CollectDependencies_ColorNode_CollectsAllComponents()
    {
        // color($controller.R, $controller.G, $controller.B, $controller.A)
        var node = new GumlValueNode
        {
            ValueType = GumlValueType.Color,
            ColorRNode = MakeControllerPropertyRef("R"),
            ColorGNode = MakeControllerPropertyRef("G"),
            ColorBNode = MakeControllerPropertyRef("B"),
            ColorANode = MakeControllerPropertyRef("A"),
        };

        var deps = DependencyTracker.CollectDependencies(node);

        Assert.AreEqual(4, deps.Count);
        Assert.IsTrue(deps.Contains("R"));
        Assert.IsTrue(deps.Contains("G"));
        Assert.IsTrue(deps.Contains("B"));
        Assert.IsTrue(deps.Contains("A"));
    }

    [TestMethod]
    public void CollectDependencies_ObjectNode_CollectsAllValues()
    {
        // { key1: $controller.X, key2: $controller.Y }
        var node = new GumlValueNode
        {
            ValueType = GumlValueType.Object,
            ObjectValue = new Dictionary<string, GumlExprNode>
            {
                ["key1"] = MakeControllerPropertyRef("X"),
                ["key2"] = MakeControllerPropertyRef("Y"),
            }
        };

        var deps = DependencyTracker.CollectDependencies(node);

        Assert.AreEqual(2, deps.Count);
        Assert.IsTrue(deps.Contains("X"));
        Assert.IsTrue(deps.Contains("Y"));
    }

    [TestMethod]
    public void CollectDependencies_ResourceNode_CollectsInner()
    {
        // resource($controller.Path)
        var node = new GumlValueNode
        {
            ValueType = GumlValueType.Resource,
            ResourceNode = MakeControllerPropertyRef("Path"),
        };

        var deps = DependencyTracker.CollectDependencies(node);

        Assert.AreEqual(1, deps.Count);
        Assert.IsTrue(deps.Contains("Path"));
    }

    [TestMethod]
    public void CollectDependencies_StyleBoxNode_CollectsInner()
    {
        // style_box_flat($controller.Style)
        var node = new GumlValueNode
        {
            ValueType = GumlValueType.StyleBox,
            StyleNode = MakeControllerPropertyRef("Style"),
        };

        var deps = DependencyTracker.CollectDependencies(node);

        Assert.AreEqual(1, deps.Count);
        Assert.IsTrue(deps.Contains("Style"));
    }

    [TestMethod]
    public void CollectDependencies_NestedInfix_InVector2()
    {
        // vec2($controller.X + 1, $controller.Y)
        var xInfix = new InfixOpNode { Op = "+" };
        xInfix.Left = MakeControllerPropertyRef("X");
        xInfix.Right = MakeLiteral(1);

        var node = new GumlValueNode
        {
            ValueType = GumlValueType.Vector2,
            Vector2XNode = xInfix,
            Vector2YNode = MakeControllerPropertyRef("Y"),
        };

        var deps = DependencyTracker.CollectDependencies(node);

        Assert.AreEqual(2, deps.Count);
        Assert.IsTrue(deps.Contains("X"));
        Assert.IsTrue(deps.Contains("Y"));
    }

    [TestMethod]
    public void CollectDependencies_PropertyRefWithLocalRoot_NoDeps()
    {
        // item.Name (LocalRef root, not GlobalRef) — should NOT produce dependency
        var localRoot = new GumlValueNode
        {
            ValueType = GumlValueType.Ref,
            RefType = RefType.LocalRef,
            RefName = "item"
        };
        var propRef = new GumlValueNode
        {
            ValueType = GumlValueType.Ref,
            RefType = RefType.PropertyRef,
            RefName = "Name",
            RefNode = localRoot
        };

        var deps = DependencyTracker.CollectDependencies(propRef);
        Assert.AreEqual(0, deps.Count);
    }
}

#endregion

#region BindingExpressionTests

[TestClass]
public class BindingExpressionTests
{
    // NOTE: BindingExpression requires a Godot Control reference.
    // The setter-based path does NOT access the Control, so passing null! is safe for those tests.
    // Reflection-based tests (string targetProperty) would require a real Control and are not tested here.

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
    public void Constructor_SubscribesToListChanged()
    {
        var list = new NotifyList<string>();
        int callCount = 0;

        var binding = new ListBinding(list, (_, _, _, _) => callCount++);

        list.Add("item");
        Assert.AreEqual(1, callCount);

        binding.Dispose();
    }

    [TestMethod]
    public void Dispose_UnsubscribesFromListChanged()
    {
        var list = new NotifyList<string>();
        int callCount = 0;

        var binding = new ListBinding(list, (_, _, _, _) => callCount++);

        list.Add("item1");
        Assert.AreEqual(1, callCount);

        binding.Dispose();

        list.Add("item2");
        Assert.AreEqual(1, callCount, "After Dispose, events should not fire.");
    }

    [TestMethod]
    public void Dispose_Idempotent()
    {
        var list = new NotifyList<string>();
        var binding = new ListBinding(list, (_, _, _, _) => { });

        binding.Dispose();
        binding.Dispose(); // Should not throw
    }

    [TestMethod]
    public void Handler_ReceivesCorrectArgs()
    {
        var list = new NotifyList<int>();
        ListChangedType? capturedType = null;
        int capturedIndex = -1;
        object? capturedObj = null;

        var binding = new ListBinding(list, (_, type, index, obj) =>
        {
            capturedType = type;
            capturedIndex = index;
            capturedObj = obj;
        });

        list.Add(42);

        Assert.AreEqual(ListChangedType.Add, capturedType);
        // After Add, Count=1, event fires with index=Count=1
        Assert.AreEqual(1, capturedIndex);
        Assert.AreEqual(42, capturedObj);

        binding.Dispose();
    }
}

#endregion

#region NotifyListTests

[TestClass]
public class NotifyListTests
{
    [TestMethod]
    public void Add_RaisesListChanged_WithAddType()
    {
        var list = new NotifyList<string>();
        var events = new List<(ListChangedType type, int index, object? obj)>();

        list.ListChanged += (_, type, index, obj) => events.Add((type, index, obj));

        list.Add("hello");

        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(ListChangedType.Add, events[0].type);
        Assert.AreEqual("hello", events[0].obj);
    }

    [TestMethod]
    public void Insert_RaisesListChanged_WithInsertType()
    {
        var list = new NotifyList<string>();
        list.Add("a");

        var events = new List<(ListChangedType type, int index, object? obj)>();
        list.ListChanged += (_, type, index, obj) => events.Add((type, index, obj));

        list.Insert(0, "b");

        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(ListChangedType.Insert, events[0].type);
        Assert.AreEqual(0, events[0].index);
        Assert.AreEqual("b", events[0].obj);
    }

    [TestMethod]
    public void Remove_RaisesListChanged_WithRemoveType()
    {
        var list = new NotifyList<string> { "a", "b", "c" };

        var events = new List<(ListChangedType type, int index, object? obj)>();
        list.ListChanged += (_, type, index, obj) => events.Add((type, index, obj));

        list.Remove("b");

        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(ListChangedType.Remove, events[0].type);
        Assert.AreEqual("b", events[0].obj);
    }

    [TestMethod]
    public void Clear_RaisesRemoveForEachItem()
    {
        var list = new NotifyList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);

        var events = new List<(ListChangedType type, int index, object? obj)>();
        list.ListChanged += (_, type, index, obj) => events.Add((type, index, obj));

        list.Clear();

        Assert.AreEqual(3, events.Count);
        Assert.IsTrue(events.TrueForAll(e => e.type == ListChangedType.Remove));
    }

    [TestMethod]
    public void AddRange_RaisesAddForEachItem()
    {
        var list = new NotifyList<string>();
        var events = new List<(ListChangedType type, object? obj)>();

        list.ListChanged += (_, type, _, obj) => events.Add((type, obj));

        list.AddRange(["x", "y", "z"]);

        Assert.AreEqual(3, events.Count);
        Assert.AreEqual("x", events[0].obj);
        Assert.AreEqual("y", events[1].obj);
        Assert.AreEqual("z", events[2].obj);
        Assert.IsTrue(events.TrueForAll(e => e.type == ListChangedType.Add));
    }

    [TestMethod]
    public void Indexer_Set_RaisesValueChanged()
    {
        var list = new NotifyList<string>();
        list.Add("original");

        int? changedIndex = null;
        object? changedValue = null;
        list.ValueChanged += (_, index, value) =>
        {
            changedIndex = index;
            changedValue = value;
        };

        list[0] = "updated";

        Assert.AreEqual(0, changedIndex);
        Assert.AreEqual("updated", changedValue);
        Assert.AreEqual("updated", list[0]);
    }

    [TestMethod]
    public void NoListeners_DoesNotThrow()
    {
        var list = new NotifyList<int>();

        // All operations should work without any subscribers
        list.Add(1);
        list.Insert(0, 2);
        list[0] = 3;
        list.Remove(1);
        list.AddRange([4, 5]);
        list.Clear();
    }

    [TestMethod]
    public void Add_MultipleItems_CorrectEventSequence()
    {
        var list = new NotifyList<string>();
        var indices = new List<int>();

        list.ListChanged += (_, _, index, _) => indices.Add(index);

        list.Add("a"); // Count becomes 1, event fires with index=Count=1
        list.Add("b"); // Count becomes 2, event fires with index=Count=2
        list.Add("c"); // Count becomes 3, event fires with index=Count=3

        Assert.AreEqual(3, indices.Count);
        Assert.AreEqual(1, indices[0]);
        Assert.AreEqual(2, indices[1]);
        Assert.AreEqual(3, indices[2]);
    }

    [TestMethod]
    public void Indexer_Get_ReturnsCorrectItem()
    {
        var list = new NotifyList<int>();
        list.Add(10);
        list.Add(20);

        Assert.AreEqual(10, list[0]);
        Assert.AreEqual(20, list[1]);
    }

    [TestMethod]
    public void Indexer_Set_UpdatesValue()
    {
        var list = new NotifyList<string>();
        list.Add("old");

        list[0] = "new";

        Assert.AreEqual("new", list[0]);
    }

    [TestMethod]
    public void Add_Event_SenderIsList()
    {
        var list = new NotifyList<int>();
        object? capturedSender = null;

        list.ListChanged += (sender, _, _, _) => capturedSender = sender;

        list.Add(1);

        Assert.AreSame(list, capturedSender);
    }

    [TestMethod]
    public void ValueChanged_Event_SenderIsList()
    {
        var list = new NotifyList<int>();
        list.Add(0);

        object? capturedSender = null;
        list.ValueChanged += (sender, _, _) => capturedSender = sender;

        list[0] = 5;

        Assert.AreSame(list, capturedSender);
    }

    [TestMethod]
    public void Insert_AtEnd_RaisesCorrectIndex()
    {
        var list = new NotifyList<string>();
        list.Add("a");

        int capturedIndex = -1;
        list.ListChanged += (_, _, index, _) => capturedIndex = index;

        list.Insert(1, "b");
        Assert.AreEqual(1, capturedIndex);
    }

    [TestMethod]
    public void Clear_EmptyList_NoEvents()
    {
        var list = new NotifyList<int>();
        int eventCount = 0;
        list.ListChanged += (_, _, _, _) => eventCount++;

        list.Clear();

        Assert.AreEqual(0, eventCount);
    }

    [TestMethod]
    public void BooleanFalse_Property_Parsed()
    {
        // Ensures NotifyList works with boolean type
        var list = new NotifyList<bool>();
        list.Add(true);
        list.Add(false);

        Assert.AreEqual(2, list.Count);
        Assert.IsTrue(list[0]);
        Assert.IsFalse(list[1]);
    }
}

#endregion
