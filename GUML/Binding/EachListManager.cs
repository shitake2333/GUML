using Godot;

namespace GUML.Binding;

/// <summary>
/// Manages the reconciliation and caching of list items for an 'each' block.
/// Separates the list management logic from the variable scope logic (EachScope).
/// </summary>
public sealed class EachListManager
{
    private readonly List<List<Node>> _activeItems = new();
    private readonly List<List<Node>> _cachedItems = new();

    public int CacheCount { get; }

    public EachListManager(int cacheCount = 0)
    {
        CacheCount = cacheCount;
    }

    /// <summary>
    /// Recursively sets the active state of a node and optionally all its children.
    /// </summary>
    public static void SetActive(Node? node, bool active, bool affectChildren = true)
    {
        if (node == null) return;

        node.ProcessMode = active
            ? Node.ProcessModeEnum.Inherit
            : Node.ProcessModeEnum.Disabled;

        if (node is CanvasItem canvasItem)
        {
            canvasItem.Visible = active;
        }

        if (node is Node3D node3D)
        {
            node3D.Visible = active;
        }

        if (node is CollisionObject2D co2D)
        {
            co2D.InputPickable = active;
        }

        if (node is CollisionObject3D co3D)
        {
            co3D.InputRayPickable = active;
        }

        if (!affectChildren) return;

        foreach (Node child in node.GetChildren())
        {
            SetActive(child, active);
        }
    }

    /// <summary>
    /// Reconciles the active node list with the given data source.
    /// Reuses existing nodes where possible, pulls from or pushes to cache,
    /// and creates or destroys nodes as needed.
    /// </summary>
    /// <param name="source">The data source list to reconcile against.</param>
    /// <param name="parent">The parent node that owns the child nodes.</param>
    /// <param name="createItem">Factory that creates a list of nodes for a data item at the given index.</param>
    /// <param name="updateItem">Callback that updates an existing node with new data at the given index.</param>
    /// <param name="startIndex">The child index offset in the parent node (for preceding static children).</param>
    public void Reconcile(
        System.Collections.IList source,
        Node parent,
        Func<int, object, List<Node>> createItem,
        Action<Node, int, object> updateItem,
        int startIndex = 0)
    {
        int dataCount = source.Count;

        // 1. Recycle/Update existing items
        int reuseCount = Math.Min(_activeItems.Count, dataCount);
        int nodeOffset = 0;

        for (int i = 0; i < reuseCount; i++)
        {
            var nodes = _activeItems[i];
            object? datum = source[i];
            foreach (var node in nodes)
            {
                if (datum != null)
                {
                    updateItem(node, i, datum);
                }

                parent.MoveChild(node, startIndex + nodeOffset);
                nodeOffset++;
            }
        }

        // 2. Add new items (from cache or create)
        if (dataCount > _activeItems.Count)
        {
            int itemsToAdd = dataCount - _activeItems.Count;
            for (int i = 0; i < itemsToAdd; i++)
            {
                int newIndex = reuseCount + i; // Fixed: stable index regardless of conditional list growth
                object? datum = source[newIndex];

                List<Node> nodes;
                if (_cachedItems.Count > 0)
                {
                    // LIFO Reuse: Take from tail (Stack behavior)
                    int lastCacheIndex = _cachedItems.Count - 1;
                    nodes = _cachedItems[lastCacheIndex];
                    _cachedItems.RemoveAt(lastCacheIndex);

                    foreach (var node in nodes)
                    {
                        parent.AddChild(node);
                        SetActive(node, true);
                        if (datum != null)
                        {
                            updateItem(node, newIndex, datum);
                        }
                    }

                    _activeItems.Add(nodes);
                }
                else
                {
                    // datum may be null for IList entries; pass as-is and let the
                    // generated createItem callback handle null values.
                    nodes = createItem(newIndex, datum ?? newIndex);
                    foreach (var node in nodes)
                    {
                        parent.AddChild(node);
                    }
                    _activeItems.Add(nodes);
                }
            }

            // Re-position all nodes from the first new item onward
            nodeOffset = 0;
            foreach (var t in _activeItems)
            {
                foreach (var node in t)
                {
                    parent.MoveChild(node, startIndex + nodeOffset);
                    nodeOffset++;
                }
            }
        }
        // 3. Remove excess items (to cache or destroy)
        else if (dataCount < _activeItems.Count)
        {
            int itemsToRemove = _activeItems.Count - dataCount;
            int freeSlots = Math.Max(0, CacheCount - _cachedItems.Count);
            int itemsToCache = Math.Min(itemsToRemove, freeSlots);
            int itemsToDiscard = itemsToRemove - itemsToCache;

            // Cache the most-recently-used items first (from tail)
            for (int i = 0; i < itemsToCache; i++)
            {
                int indexToRemove = _activeItems.Count - 1;
                var nodes = _activeItems[indexToRemove];
                _activeItems.RemoveAt(indexToRemove);

                foreach (var node in nodes)
                {
                    SetActive(node, false);
                    parent.RemoveChild(node);
                }
                _cachedItems.Add(nodes);
            }

            // Destroy the rest
            for (int i = 0; i < itemsToDiscard; i++)
            {
                int indexToRemove = _activeItems.Count - 1;
                var nodes = _activeItems[indexToRemove];
                _activeItems.RemoveAt(indexToRemove);

                foreach (var node in nodes)
                {
                    // Clean up binding scopes before queue free
                    if (node.HasMeta(BindingScope.MetaKey))
                    {
                        var bs = node.GetMeta(BindingScope.MetaKey).As<BindingScope>();
                        bs.Dispose();
                    }
                    SetActive(node, false);
                    node.QueueFree();
                }
            }
        }
    }

    /// <summary>
    /// Updates a single item at the given index without full reconciliation.
    /// Used when <see cref="System.Collections.Specialized.INotifyCollectionChanged.CollectionChanged"/> fires with a Replace action.
    /// </summary>
    /// <param name="index">The index of the item to update.</param>
    /// <param name="data">The new data for the item.</param>
    /// <param name="updateItem">Callback that updates an existing node with new data at the given index.</param>
    public void UpdateSingleItem(int index, object data, Action<Node, int, object> updateItem)
    {
        if (index < 0 || index >= _activeItems.Count) return;

        var nodes = _activeItems[index];
        foreach (var node in nodes)
        {
            updateItem(node, index, data);
        }
    }
}
