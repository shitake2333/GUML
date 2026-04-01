using GUML.Analyzer.Utils;

namespace GUML.Analyzer.Workspace;

/// <summary>
/// Manages all open GUML documents and provides access to the semantic model.
/// Thread-safe for concurrent document operations.
/// </summary>
public sealed class GumlWorkspace
{
    private readonly Dictionary<string, GumlDocument> _documents = new();
    private readonly Dictionary<string, (int Version, SemanticModel Model)> _semanticModelCache = new();
    private readonly Lock _lock = new();
    private readonly ProjectAnalyzer _analyzer;

    /// <summary>
    /// The workspace root directory path, used for scanning .guml files on disk.
    /// </summary>
    public string WorkspaceRoot { get; set; } = "";

    public GumlWorkspace(ProjectAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }

    /// <summary>
    /// Opens or replaces a document with full source text.
    /// </summary>
    public GumlDocument OpenDocument(string uri, string text)
    {
        var doc = new GumlDocument(uri, text);
        lock (_lock)
        {
            _documents[uri] = doc;
        }

        return doc;
    }

    /// <summary>
    /// Updates an already-open document with new full text. Always performs a full re-parse.
    /// Falls back to opening a new document if the URI is not currently open.
    /// </summary>
    public GumlDocument UpdateDocument(string uri, string newText)
    {
        lock (_lock)
        {
            var doc = new GumlDocument(uri, newText,
                _documents.TryGetValue(uri, out var prev) ? prev.Version + 1 : 1);
            _documents[uri] = doc;
            return doc;
        }
    }

    /// <summary>
    /// Closes a document and releases resources.
    /// </summary>
    public void CloseDocument(string uri)
    {
        lock (_lock)
        {
            _documents.Remove(uri);
            _semanticModelCache.Remove(uri);
        }
    }

    /// <summary>
    /// Gets a document by URI, or null if not open.
    /// </summary>
    public GumlDocument? GetDocument(string uri)
    {
        lock (_lock)
        {
            return _documents.GetValueOrDefault(uri);
        }
    }

    /// <summary>
    /// Returns whether the given URI has an open document in the workspace.
    /// </summary>
    public bool IsDocumentOpen(string uri)
    {
        lock (_lock)
        {
            return _documents.ContainsKey(uri);
        }
    }

    /// <summary>
    /// Gets the semantic model for a document. Cached per document version.
    /// Diagnostics are pre-computed before the model is returned so that concurrent
    /// callers do not race on the lazy-initialization of internal model state.
    /// </summary>
    public SemanticModel? GetSemanticModel(string uri)
    {
        GumlDocument? doc;
        int docVersion;

        lock (_lock)
        {
            if (!_documents.TryGetValue(uri, out doc)) return null;
            docVersion = doc.Version;

            if (_semanticModelCache.TryGetValue(uri, out var cached) && cached.Version == docVersion)
                return cached.Model;
        }

        // Compute semantic model outside the lock to avoid blocking other requests
        var model = new SemanticModel(doc, _analyzer);
        model.GetDiagnostics();

        lock (_lock)
        {
            // Double-check: another thread may have computed it first
            if (_semanticModelCache.TryGetValue(uri, out var cached) && cached.Version == docVersion)
                return cached.Model;

            _semanticModelCache[uri] = (docVersion, model);
            return model;
        }
    }

    /// <summary>
    /// Returns all currently open documents.
    /// </summary>
    public IReadOnlyList<GumlDocument> GetAllDocuments()
    {
        lock (_lock)
        {
            return [.. _documents.Values];
        }
    }

    /// <summary>
    /// Atomically invalidates the semantic-model cache for every open document that imports
    /// <paramref name="changedUri"/> and returns the still-open documents that were affected.
    /// Using a single lock acquisition eliminates the TOCTOU window between invalidation
    /// and document retrieval.
    /// </summary>
    public List<(string Uri, GumlDocument Document)> InvalidateAndGetImporters(string changedUri)
    {
        string changedPath = PathUtils.UriToFilePath(changedUri);
        var affected = new List<(string Uri, GumlDocument Document)>();

        lock (_lock)
        {
            foreach (var (uri, doc) in _documents)
            {
                if (string.Equals(uri, changedUri, StringComparison.OrdinalIgnoreCase))
                    continue;

                string docPath = PathUtils.UriToFilePath(uri);
                string? docDir = Path.GetDirectoryName(docPath);
                if (docDir == null) continue;

                foreach (var import in doc.Root.Imports)
                {
                    string importPath = import.Path.Text.Trim('"');
                    if (string.IsNullOrEmpty(importPath)) continue;

                    try
                    {
                        string resolved = Path.GetFullPath(Path.Combine(docDir, importPath));
                        if (string.Equals(resolved, changedPath, StringComparison.OrdinalIgnoreCase))
                        {
                            _semanticModelCache.Remove(uri);
                            affected.Add((uri, doc));
                            break;
                        }
                    }
                    catch
                    {
                        // Path resolution failed, skip
                    }
                }
            }
        }

        return affected;
    }

    /// <summary>
    /// Returns parsed <see cref="GumlDocument"/> instances for all .guml files found on disk
    /// under <see cref="WorkspaceRoot"/>. For files already open in the workspace, the open
    /// version is returned; otherwise the file is read from disk and parsed on the fly.
    /// </summary>
    public IReadOnlyList<GumlDocument> GetAllGumlFilesFromDisk()
    {
        if (string.IsNullOrEmpty(WorkspaceRoot) || !Directory.Exists(WorkspaceRoot))
            return GetAllDocuments();

        string[] files;
        try
        {
            files = Directory.GetFiles(WorkspaceRoot, "*.guml", SearchOption.AllDirectories);
        }
        catch
        {
            return GetAllDocuments();
        }

        var result = new List<GumlDocument>();

        lock (_lock)
        {
            foreach (string filePath in files)
            {
                string uri = PathUtils.FilePathToUri(filePath);

                // Prefer open document (has latest unsaved edits)
                if (_documents.TryGetValue(uri, out var openDoc))
                {
                    result.Add(openDoc);
                    continue;
                }

                // Read and parse from disk
                try
                {
                    string text = File.ReadAllText(filePath);
                    result.Add(new GumlDocument(uri, text));
                }
                catch
                {
                    // Skip unreadable files
                }
            }
        }

        return result;
    }
}
