using Godot;

namespace GUML;

/// <summary>
/// Identifies a cached resource by its category and path.
/// </summary>
internal enum ResourceCategory { Image, Font, Audio, Video }

/// <summary>
/// Tracks a cached resource instance and its consumer nodes.
/// </summary>
internal sealed class CachedResource
{
    public object Resource { get; }
    public HashSet<Node> Consumers { get; } = new();

    public CachedResource(object resource) => Resource = resource;
}

/// <summary>
/// Default resource provider with built-in caching and reference-counted
/// lifecycle management. Resources are cached by (category, path) and
/// automatically released when all consumer nodes exit the scene tree.
/// </summary>
public class DefaultResourceProvider : IResourceProvider
{
    private readonly Dictionary<(ResourceCategory, string), CachedResource> _cache = new();
    private readonly Lock _cacheLock = new();

    /// <inheritdoc />
    public virtual object LoadImage(string path, Node? consumer = null)
    {
        return GetOrLoad(ResourceCategory.Image, path, consumer, LoadImageCore);
    }

    /// <inheritdoc />
    public virtual object LoadFont(string path, Node? consumer = null)
    {
        return GetOrLoad(ResourceCategory.Font, path, consumer, LoadFontCore);
    }

    /// <inheritdoc />
    public virtual object LoadAudio(string path, Node? consumer = null)
    {
        return GetOrLoad(ResourceCategory.Audio, path, consumer, LoadAudioCore);
    }

    /// <inheritdoc />
    public virtual object LoadVideo(string path, Node? consumer = null)
    {
        return GetOrLoad(ResourceCategory.Video, path, consumer, LoadVideoCore);
    }

    // ── Core loading methods (override these for custom loading logic) ──

    /// <summary>
    /// Performs the actual image loading. Override for custom behavior.
    /// </summary>
    protected virtual object LoadImageCore(string path)
    {
        if (path.StartsWith("res://"))
            return GD.Load<Texture2D>(path);
        return ImageTexture.CreateFromImage(Image.LoadFromFile(path));
    }

    /// <summary>
    /// Performs the actual font loading. Override for custom behavior.
    /// </summary>
    protected virtual object LoadFontCore(string path)
    {
        if (path.StartsWith("res://"))
            return GD.Load<Font>(path);
        var font = new FontFile();
        font.LoadDynamicFont(path);
        return font;
    }

    /// <summary>
    /// Performs the actual audio loading. Override for custom behavior.
    /// </summary>
    protected virtual object LoadAudioCore(string path)
    {
        if (path.StartsWith("res://"))
            return GD.Load<AudioStream>(path);
        return AudioStreamOggVorbis.LoadFromFile(path);
    }

    /// <summary>
    /// Performs the actual video loading. Override for custom behavior.
    /// </summary>
    protected virtual object LoadVideoCore(string path)
    {
        if (path.StartsWith("res://"))
            return GD.Load<VideoStream>(path);
        throw new NotSupportedException(
            $"Loading video from file system path is not supported: {path}");
    }

    // ── Cache & reference tracking ──

    private object GetOrLoad(ResourceCategory category, string path,
        Node? consumer, Func<string, object> loader)
    {
        var key = (category, path);

        lock (_cacheLock)
        {
            if (!_cache.TryGetValue(key, out var cached))
            {
                cached = new CachedResource(loader(path));
                _cache[key] = cached;
            }

            if (consumer != null && cached.Consumers.Add(consumer))
            {
                var capturedKey = key;
                consumer.TreeExiting += () => OnConsumerExiting(capturedKey, consumer);
            }

            return cached.Resource;
        }
    }

    private void OnConsumerExiting((ResourceCategory, string) key, Node consumer)
    {
        lock (_cacheLock)
        {
            if (!_cache.TryGetValue(key, out var cached)) return;

            cached.Consumers.Remove(consumer);
            if (cached.Consumers.Count == 0)
            {
                _cache.Remove(key);
                if (cached.Resource is IDisposable disposable)
                    disposable.Dispose();
            }
        }
    }

    /// <summary>
    /// Returns the number of currently cached resources. Useful for diagnostics.
    /// </summary>
    public int CachedCount
    {
        get
        {
            lock (_cacheLock)
            {
                return _cache.Count;
            }
        }
    }

    /// <summary>
    /// Clears all cached resources and disposes any that implement IDisposable.
    /// </summary>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            foreach (var cached in _cache.Values)
            {
                if (cached.Resource is IDisposable disposable)
                    disposable.Dispose();
            }
            _cache.Clear();
        }
    }
}
