using GUML.Analyzer.Handlers;
using GUML.Analyzer.Utils;
using GUML.Analyzer.Workspace;
using Serilog;

namespace GUML.Analyzer;

/// <summary>
/// The main GUML analyzer server. Processes JSON-RPC requests for both the legacy
/// API-server protocol and LSP-style language features (completion, hover, definition,
/// formatting, diagnostics, semantic tokens, document highlight).
/// </summary>
public sealed class AnalyzerServer : IDisposable
{
    private readonly JsonRpcTransport _transport;
    private readonly ProcessControl _processControl;
    private readonly ProjectAnalyzer _analyzer = new();
    private readonly CancellationTokenSource _shutdownCts = new();
    private GumlWorkspace? _workspace;
    private volatile bool _initialized;

    // .guml file watcher
    private FileSystemWatcher? _gumlWatcher;
    private readonly Lock _gumlDebounceLock = new();
    private readonly HashSet<string> _pendingGumlChanges = new();
    private CancellationTokenSource? _gumlDebounceCts;
    private const int GumlDebounceDelayMs = 500;

    /// <summary>
    /// Creates a new analyzer server instance with the given transport.
    /// </summary>
    public AnalyzerServer(JsonRpcTransport transport, ProcessControl processControl)
    {
        _transport = transport;
        _processControl = processControl;

        _analyzer.CacheUpdated += OnCacheUpdated;
        _analyzer.MissingGodotApi += OnMissingGodotApi;
        _processControl.StopRequested += () =>
        {
            Log.Logger.Information("Stop requested via Named Pipe, shutting down...");
            _shutdownCts.Cancel();
        };
    }

    /// <summary>
    /// Runs the server main loop: reads JSON-RPC requests and dispatches them.
    /// </summary>
    public async Task RunAsync()
    {
        Log.Logger.Information("Analyzer server started, waiting for requests...");

        var ct = _shutdownCts.Token;
        while (!ct.IsCancellationRequested)
        {
            var request = await _transport.ReadRequestAsync(ct);
            if (request == null)
            {
                Log.Logger.Information("Input stream closed, shutting down");
                break;
            }

            try
            {
                await DispatchAsync(request);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error dispatching request: {Method}", request.Method);
                if (request.Id != null)
                    _transport.SendError(request.Id, -32603, $"Internal error: {ex.Message}");
            }
        }

        Log.Logger.Information("Analyzer server main loop exited");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _shutdownCts.Cancel();
        _gumlWatcher?.Dispose();
        _gumlDebounceCts?.Dispose();
        _analyzer.Dispose();
        _processControl.Dispose();
        _transport.Dispose();
        _shutdownCts.Dispose();
    }

    // ── Request dispatching ──

    private async Task DispatchAsync(JsonRpcRequest request)
    {
        switch (request.Method)
        {
            // Legacy API-server methods (backward-compatible)
            case "initialize":
                await HandleInitializeAsync(request);
                break;
            case "getApiCache":
                HandleGetApiCache(request);
                break;
            case "getClassInfo":
                HandleGetClassInfo(request);
                break;
            case "getControllerInfo":
                HandleGetControllerInfo(request);
                break;
            case "selectProject":
                HandleSelectProject(request);
                break;
            case "guml/rebuildCache":
                HandleRebuildCache(request);
                break;
            case "guml/generateGodotApi":
                await HandleGenerateGodotApiAsync(request);
                break;
            case "shutdown":
                HandleShutdown(request);
                break;
            case "initialized":
            case "exit":
            case "$/cancelRequest":
                break;

            // LSP document synchronization
            case "textDocument/didOpen":
                HandleDidOpen(request);
                break;
            case "textDocument/didChange":
                HandleDidChange(request);
                break;
            case "textDocument/didClose":
                HandleDidClose(request);
                break;

            // LSP language features
            case "textDocument/completion":
                HandleCompletion(request);
                break;
            case "textDocument/hover":
                HandleHover(request);
                break;
            case "textDocument/definition":
                HandleDefinition(request);
                break;
            case "textDocument/formatting":
                HandleFormatting(request);
                break;
            case "textDocument/rangeFormatting":
                HandleRangeFormatting(request);
                break;
            case "textDocument/semanticTokens/full":
                HandleSemanticTokensFull(request);
                break;
            case "textDocument/semanticTokens/range":
                HandleSemanticTokensRange(request);
                break;
            case "textDocument/documentHighlight":
                HandleDocumentHighlight(request);
                break;
            case "textDocument/prepareRename":
                HandlePrepareRename(request);
                break;
            case "textDocument/rename":
                HandleRename(request);
                break;

            default:
                Log.Logger.Warning("Unknown method: {Method}", request.Method);
                if (request.Id != null)
                    _transport.SendError(request.Id, -32601, $"Method not found: {request.Method}");
                break;
        }
    }

    // ══════════════════════════════════════════════════════════
    //  Legacy API-server handlers (backward-compatible)
    // ══════════════════════════════════════════════════════════

    private async Task HandleInitializeAsync(JsonRpcRequest request)
    {
        string workspaceRoot = "";
        string? projectPath = null;

        if (request.Params.HasValue)
        {
            var p = request.Params.Value;

            // Read from initializationOptions (standard LSP client pattern)
            if (p.TryGetProperty("initializationOptions", out var initOpts))
            {
                if (initOpts.TryGetProperty("workspaceRoot", out var wr))
                    workspaceRoot = wr.GetString() ?? "";
                if (initOpts.TryGetProperty("projectPath", out var pp))
                    projectPath = pp.GetString();
            }

            // Fallback: top-level params (legacy / direct JSON-RPC callers)
            if (string.IsNullOrEmpty(workspaceRoot) && p.TryGetProperty("workspaceRoot", out var wr2))
                workspaceRoot = wr2.GetString() ?? "";
            if (projectPath == null && p.TryGetProperty("projectPath", out var pp2))
                projectPath = pp2.GetString();

            // Fallback: standard LSP rootUri / rootPath
            if (string.IsNullOrEmpty(workspaceRoot) && p.TryGetProperty("rootUri", out var ru))
            {
                string? rootUri = ru.GetString();
                if (rootUri != null && Uri.TryCreate(rootUri, UriKind.Absolute, out var uri))
                    workspaceRoot = uri.LocalPath;
            }

            if (string.IsNullOrEmpty(workspaceRoot) && p.TryGetProperty("rootPath", out var rp))
                workspaceRoot = rp.GetString() ?? "";
        }

        Log.Logger.Information("Initialize: workspaceRoot={Root}, projectPath={Project}",
            workspaceRoot, projectPath ?? "(auto-detect)");

        // Suppress notifications until init response is sent (LSP spec requirement)
        _initialized = false;

        Func<List<string>, Task<string?>>? projectSelector;

        if (projectPath == null)
        {
            var tcs = new TaskCompletionSource<string?>();
            _pendingProjectSelection = tcs;

            projectSelector = async candidates =>
            {
                var scoredCandidates = candidates.Select(c => new { path = c, score = 0 }).ToArray();
                _transport.SendNotification("projectCandidates", new { candidates = scoredCandidates });

                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                try
                {
                    return await tcs.Task.WaitAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            };
        }
        else
        {
            projectSelector = _ => Task.FromResult<string?>(projectPath);
        }

        await _analyzer.InitializeAsync(workspaceRoot, projectSelector);

        // Create workspace after analyzer is ready
        _workspace = new GumlWorkspace(_analyzer) { WorkspaceRoot = workspaceRoot };

        // Start watching .guml files for external changes
        if (!string.IsNullOrEmpty(workspaceRoot) && Directory.Exists(workspaceRoot))
            StartGumlFileWatcher(workspaceRoot);

        if (request.Id != null)
        {
            // IMPORTANT: Send InitializeResult BEFORE any notifications
            // (LSP spec: server must not send notifications before the init response)
            // Return standard LSP InitializeResult so vscode-languageclient
            // correctly registers capabilities for completion, hover, etc.
            _transport.SendResponse(request.Id,
                new
                {
                    capabilities = new
                    {
                        textDocumentSync = new
                        {
                            openClose = true,
                            // TextDocumentSyncKind: Full = 1
                            change = 1
                        },
                        completionProvider = new { triggerCharacters = new[] { ".", "$", "#", "@", ":", "=" } },
                        hoverProvider = true,
                        definitionProvider = true,
                        documentFormattingProvider = true,
                        documentRangeFormattingProvider = true,
                        semanticTokensProvider = new
                        {
                            full = true,
                            range = true,
                            legend = new
                            {
                                tokenTypes = SemanticTokenTypes.Legend,
                                tokenModifiers = SemanticTokenModifiers.Legend
                            }
                        },
                        documentHighlightProvider = true,
                        renameProvider = new { prepareProvider = true }
                    }
                });
        }

        // Now that init response is sent, notifications are allowed
        _initialized = true;

        if (_analyzer.IsReady)
        {
            PushApiCacheUpdated(null); // full cache on init
            SendStatus("ready");
        }
    }

    private TaskCompletionSource<string?>? _pendingProjectSelection;

    private void HandleGetApiCache(JsonRpcRequest request)
    {
        if (request.Id != null)
            _transport.SendResponse(request.Id, _analyzer.ApiDocument);
    }

    private void HandleGetClassInfo(JsonRpcRequest request)
    {
        string? className = null;
        if (request.Params.HasValue &&
            request.Params.Value.TryGetProperty("className", out var cn))
            className = cn.GetString();

        if (request.Id != null)
        {
            if (className != null)
            {
                var info = _analyzer.GetTypeInfo(className);
                _transport.SendResponse(request.Id, info);
            }
            else
            {
                _transport.SendError(request.Id, -32602, "Missing required parameter: className");
            }
        }
    }

    private void HandleGetControllerInfo(JsonRpcRequest request)
    {
        string? gumlPath = null;
        if (request.Params.HasValue &&
            request.Params.Value.TryGetProperty("gumlPath", out var gp))
            gumlPath = gp.GetString();

        if (request.Id != null)
        {
            if (gumlPath != null)
            {
                var info = _analyzer.GetControllerForGuml(gumlPath);
                _transport.SendResponse(request.Id, info);
            }
            else
            {
                _transport.SendError(request.Id, -32602, "Missing required parameter: gumlPath");
            }
        }
    }

    private void HandleSelectProject(JsonRpcRequest request)
    {
        string? projectPath = null;
        if (request.Params.HasValue &&
            request.Params.Value.TryGetProperty("projectPath", out var pp))
            projectPath = pp.GetString();

        _pendingProjectSelection?.TrySetResult(projectPath);

        if (request.Id != null)
            _transport.SendResponse(request.Id, new { status = "ok" });
    }

    private void HandleShutdown(JsonRpcRequest request)
    {
        Log.Logger.Information("Shutdown requested via JSON-RPC");
        if (request.Id != null)
            _transport.SendResponse(request.Id, null);
        _shutdownCts.Cancel();
    }

    private void HandleRebuildCache(JsonRpcRequest request)
    {
        Log.Logger.Information("Manual cache rebuild requested");

        // Immediately acknowledge the request
        if (request.Id != null)
            _transport.SendResponse(request.Id, new { status = "accepted" });

        _ = Task.Run(async () =>
        {
            try
            {
                SendStatus("analyzing", "Rebuilding API cache...");
                await _analyzer.ForceRebuildAsync();
                SendStatus("ready");
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Manual cache rebuild failed");
                SendStatus("ready");
            }
        });
    }

    // ══════════════════════════════════════════════════════════
    //  Godot API generation
    // ══════════════════════════════════════════════════════════

    private void OnMissingGodotApi(string godotVersion, string expectedPath)
    {
        if (!_initialized) return;
        _transport.SendNotification("guml/missingGodotApi", new
        {
            godotVersion,
            expectedPath,
        });
    }

    private async Task HandleGenerateGodotApiAsync(JsonRpcRequest request)
    {
        string? godotVersion = null;
        string? localPath = null;

        if (request.Params.HasValue)
        {
            var p = request.Params.Value;
            if (p.TryGetProperty("godotVersion", out var gv))
                godotVersion = gv.GetString();
            // "source" parameter reserved for future use (e.g. remote download).
            if (p.TryGetProperty("localPath", out var lp))
                localPath = lp.GetString();
        }

        godotVersion ??= _analyzer.DetectedGodotVersion;

        if (godotVersion == null)
        {
            if (request.Id != null)
                _transport.SendError(request.Id, -32602,
                    "Cannot determine Godot version. Provide godotVersion parameter or ensure a Godot .csproj is loaded.");
            return;
        }

        Log.Logger.Information("Generating Godot API for version {Version}", godotVersion);

        void OnProgress(string stage, string message)
        {
            _transport.SendNotification("guml/generateGodotApiProgress", new
            {
                stage,
                message,
            });
        }

        try
        {
            string? resultPath;

            if (!string.IsNullOrEmpty(localPath))
            {
                resultPath = GodotApiCatalog.GenerateFromLocal(
                    localPath, godotVersion, outputPath: null, OnProgress);
            }
            else
            {
                resultPath = await GodotApiCatalog.GenerateFromGitHubAsync(
                    godotVersion, outputPath: null, OnProgress);
            }

            if (resultPath != null)
            {
                _analyzer.ReloadGodotCatalog(resultPath);

                if (request.Id != null)
                    _transport.SendResponse(request.Id, new
                    {
                        success = true,
                        outputPath = resultPath,
                        classCount = _analyzer.GodotCatalog?.Classes.Count ?? 0,
                        message = $"Successfully generated API data for Godot {godotVersion}",
                    });
            }
            else
            {
                if (request.Id != null)
                    _transport.SendResponse(request.Id, new
                    {
                        success = false,
                        message = "API generation failed. Check server logs for details.",
                    });
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Failed to generate Godot API");
            if (request.Id != null)
                _transport.SendError(request.Id, -32603, $"API generation failed: {ex.Message}");
        }
    }

    // ══════════════════════════════════════════════════════════
    //  LSP document synchronization
    // ══════════════════════════════════════════════════════════

    private void HandleDidOpen(JsonRpcRequest request)
    {
        if (_workspace == null || !request.Params.HasValue) return;

        var p = request.Params.Value;
        if (!p.TryGetProperty("textDocument", out var td)) return;
        string uri = td.GetProperty("uri").GetString() ?? "";
        string text = td.GetProperty("text").GetString() ?? "";

        Log.Logger.Debug("DidOpen: {Uri} ({Length} chars)", uri, text.Length);
        var doc = _workspace.OpenDocument(uri, text);
        PublishDiagnostics(doc);
        RepublishImporters(uri);
    }

    private void HandleDidChange(JsonRpcRequest request)
    {
        if (_workspace == null || !request.Params.HasValue) return;

        var p = request.Params.Value;
        if (!p.TryGetProperty("textDocument", out var td)) return;
        string uri = td.GetProperty("uri").GetString() ?? "";

        if (!p.TryGetProperty("contentChanges", out var changes)) return;

        // The server declares TextDocumentSyncKind.Full so clients always send
        // the complete document text in the single content change entry.
        string? fullText = null;
        foreach (var change in changes.EnumerateArray())
        {
            fullText = change.GetProperty("text").GetString() ?? "";
        }

        if (fullText != null)
        {
            var doc = _workspace.UpdateDocument(uri, fullText);
            PublishDiagnostics(doc);
            RepublishImporters(uri);
        }
    }

    /// <summary>
    /// Invalidates semantic models and re-publishes diagnostics for all open documents
    /// that import the file identified by <paramref name="changedUri"/>.
    /// </summary>
    private void RepublishImporters(string changedUri)
    {
        if (_workspace == null) return;

        var affected = _workspace.InvalidateAndGetImporters(changedUri);
        foreach (var (uri, affectedDoc) in affected)
        {
            Log.Logger.Debug("Re-publishing diagnostics for importer: {Uri}", uri);
            PublishDiagnostics(affectedDoc);
        }
    }

    private void HandleDidClose(JsonRpcRequest request)
    {
        if (_workspace == null || !request.Params.HasValue) return;

        var p = request.Params.Value;
        if (!p.TryGetProperty("textDocument", out var td)) return;
        string uri = td.GetProperty("uri").GetString() ?? "";

        _workspace.CloseDocument(uri);

        // Clear diagnostics for the closed document
        _transport.SendNotification("textDocument/publishDiagnostics",
            new { uri, diagnostics = Array.Empty<LspDiagnostic>() });
    }

    // ══════════════════════════════════════════════════════════
    //  LSP language feature handlers
    // ══════════════════════════════════════════════════════════

    private void HandleCompletion(JsonRpcRequest request)
    {
        if (request.Id == null) return;

        Log.Logger.Information("Completion raw params: {Params}",
            request.Params.HasValue ? request.Params.Value.GetRawText() : "(null)");

        var (doc, position) = ExtractDocumentPosition(request);
        if (doc == null)
        {
            Log.Logger.Warning("Completion: document not found. Open docs: {Docs}",
                string.Join(", ", (_workspace?.GetAllDocuments() ?? []).Select(d => d.Uri)));
            _transport.SendResponse(request.Id, Array.Empty<CompletionItem>());
            return;
        }

        var model = _workspace!.GetSemanticModel(doc.Uri);
        var items = CompletionHandler.GetCompletions(doc, model, position, _analyzer);
        Log.Logger.Information("Completion at {Uri} ({Line}:{Char}): {Count} items",
            doc.Uri, position.Line, position.Character, items.Count);
        _transport.SendResponse(request.Id, items);
    }

    private void HandleHover(JsonRpcRequest request)
    {
        if (request.Id == null) return;

        var (doc, position) = ExtractDocumentPosition(request);
        if (doc == null)
        {
            Log.Logger.Debug("Hover: document not found");
            _transport.SendResponse(request.Id, null);
            return;
        }

        var model = _workspace!.GetSemanticModel(doc.Uri);
        var hover = HoverHandler.GetHover(doc, model, position, _analyzer);
        Log.Logger.Debug("Hover at {Uri} ({Line}:{Char}): {HasResult}",
            doc.Uri, position.Line, position.Character, hover != null);
        _transport.SendResponse(request.Id, hover);
    }

    private void HandleDefinition(JsonRpcRequest request)
    {
        if (request.Id == null) return;

        var (doc, position) = ExtractDocumentPosition(request);
        if (doc == null)
        {
            Log.Logger.Debug("Definition: document not found");
            _transport.SendResponse(request.Id, Array.Empty<LspLocation>());
            return;
        }

        var model = _workspace!.GetSemanticModel(doc.Uri);
        var locations = DefinitionHandler.GetDefinitions(doc, model, position, _analyzer);
        Log.Logger.Debug("Definition at {Uri} ({Line}:{Char}): {Count} locations",
            doc.Uri, position.Line, position.Character, locations.Count);
        _transport.SendResponse(request.Id, locations);
    }

    private void HandleFormatting(JsonRpcRequest request)
    {
        if (request.Id == null) return;

        var doc = ExtractDocument(request);
        if (doc == null)
        {
            _transport.SendResponse(request.Id, Array.Empty<TextEdit>());
            return;
        }

        var options = ExtractFormattingOptions(request);
        var edits = FormattingHandler.Format(doc, options);
        _transport.SendResponse(request.Id, edits);
    }

    private void HandleRangeFormatting(JsonRpcRequest request)
    {
        if (request.Id == null) return;

        var doc = ExtractDocument(request);
        if (doc == null)
        {
            _transport.SendResponse(request.Id, Array.Empty<TextEdit>());
            return;
        }

        var options = ExtractFormattingOptions(request);
        LspRange? range = ExtractRange(request);
        var edits = FormattingHandler.Format(doc, options, range);
        _transport.SendResponse(request.Id, edits);
    }

    private void HandleSemanticTokensFull(JsonRpcRequest request)
    {
        if (request.Id == null) return;

        var doc = ExtractDocument(request);
        if (doc == null)
        {
            _transport.SendResponse(request.Id, new SemanticTokensResult());
            return;
        }

        var result = SemanticTokensHandler.GetTokens(doc);
        _transport.SendResponse(request.Id, result);
    }

    private void HandleSemanticTokensRange(JsonRpcRequest request)
    {
        if (request.Id == null) return;

        var doc = ExtractDocument(request);
        if (doc == null)
        {
            _transport.SendResponse(request.Id, new SemanticTokensResult());
            return;
        }

        LspRange? range = ExtractRange(request);
        var result = SemanticTokensHandler.GetTokens(doc, range);
        _transport.SendResponse(request.Id, result);
    }

    private void HandleDocumentHighlight(JsonRpcRequest request)
    {
        if (request.Id == null) return;

        var (doc, position) = ExtractDocumentPosition(request);
        if (doc == null)
        {
            _transport.SendResponse(request.Id, Array.Empty<DocumentHighlight>());
            return;
        }

        var highlights = DocumentHighlightHandler.GetHighlights(doc, position);
        _transport.SendResponse(request.Id, highlights);
    }

    private void HandlePrepareRename(JsonRpcRequest request)
    {
        if (request.Id == null) return;

        var (doc, position) = ExtractDocumentPosition(request);
        if (doc == null)
        {
            _transport.SendResponse(request.Id, null);
            return;
        }

        var model = _workspace!.GetSemanticModel(doc.Uri);
        var result = RenameHandler.PrepareRename(doc, model, position);
        Log.Logger.Debug("PrepareRename at {Uri} ({Line}:{Char}): {HasResult}",
            doc.Uri, position.Line, position.Character, result != null);
        _transport.SendResponse(request.Id, result);
    }

    private void HandleRename(JsonRpcRequest request)
    {
        if (request.Id == null) return;

        var (doc, position) = ExtractDocumentPosition(request);
        if (doc == null)
        {
            _transport.SendResponse(request.Id, null);
            return;
        }

        string newName = "";
        if (request.Params.HasValue && request.Params.Value.TryGetProperty("newName", out var nn))
            newName = nn.GetString() ?? "";

        var model = _workspace!.GetSemanticModel(doc.Uri);
        var edits = RenameHandler.GetRenameEdits(doc, model, position, newName, _workspace);
        Log.Logger.Debug("Rename at {Uri} ({Line}:{Char}) -> '{NewName}': {HasEdits}",
            doc.Uri, position.Line, position.Character, newName, edits != null);
        _transport.SendResponse(request.Id, edits);
    }

    // ══════════════════════════════════════════════════════════
    //  .guml file watching
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Starts watching .guml files under the workspace root for external changes.
    /// When a .guml file is modified outside the editor (e.g. git checkout, external editor),
    /// dependent documents are re-analyzed and diagnostics are republished.
    /// </summary>
    private void StartGumlFileWatcher(string rootPath)
    {
        try
        {
            _gumlWatcher = new FileSystemWatcher(rootPath, "*.guml")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime
            };

            _gumlWatcher.Changed += OnGumlFileChanged;
            _gumlWatcher.Created += OnGumlFileChanged;
            _gumlWatcher.Deleted += OnGumlFileChanged;
            _gumlWatcher.Renamed += (_, e) => OnGumlFileChanged(null, e);

            _gumlWatcher.EnableRaisingEvents = true;
            Log.Logger.Information("Started watching .guml files in {Root}", rootPath);
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Failed to start .guml file watcher");
        }
    }

    private void OnGumlFileChanged(object? sender, FileSystemEventArgs e)
    {
        // Ignore obj/bin directories
        if (e.FullPath.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) ||
            e.FullPath.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            return;

        lock (_gumlDebounceLock)
        {
            _pendingGumlChanges.Add(e.FullPath);
            _gumlDebounceCts?.Cancel();
            _gumlDebounceCts?.Dispose();
            _gumlDebounceCts = new CancellationTokenSource();
            var token = _gumlDebounceCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(GumlDebounceDelayMs, token);
                    if (token.IsCancellationRequested) return;

                    HashSet<string> changedFiles;
                    lock (_gumlDebounceLock)
                    {
                        changedFiles = [.. _pendingGumlChanges];
                        _pendingGumlChanges.Clear();
                    }

                    if (_workspace == null) return;

                    foreach (string filePath in changedFiles)
                    {
                        string uri = PathUtils.FilePathToUri(filePath);

                        // Skip files already open in the editor — didChange handles those
                        if (_workspace.IsDocumentOpen(uri)) continue;

                        Log.Logger.Debug("External .guml change detected: {Path}", filePath);
                        RepublishImporters(uri);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Debounce cancelled by a newer change, expected
                }
                catch (Exception ex)
                {
                    Log.Logger.Error(ex, "Error handling .guml file change");
                }
            }, token);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  Notification helpers
    // ══════════════════════════════════════════════════════════

    private void OnCacheUpdated(ApiCacheDiff? diff)
    {
        Log.Logger.Information("API cache updated (incremental={IsIncremental}), pushing notification",
            diff != null);
        if (!_initialized) return; // suppress during initialization
        SendStatus("analyzing", "Rebuilding API cache...");
        PushApiCacheUpdated(diff);
        SendStatus("ready");

        // Re-publish diagnostics for affected documents
        if (_workspace != null)
        {
            if (diff == null)
            {
                // Full rebuild: re-publish all open documents
                foreach (var doc in _workspace.GetAllDocuments())
                    PublishDiagnostics(doc);
            }
            else
            {
                // Incremental: only re-publish documents affected by the diff
                var affectedUris = GetAffectedDocumentUris(diff);
                foreach (var doc in _workspace.GetAllDocuments())
                {
                    if (affectedUris.Contains(doc.Uri))
                        PublishDiagnostics(doc);
                }
            }
        }
    }

    /// <summary>
    /// Determines which open GUML document URIs are affected by the given diff.
    /// </summary>
    private HashSet<string> GetAffectedDocumentUris(ApiCacheDiff diff)
    {
        var affected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Controllers directly map to guml files
        foreach (string ctrlPath in diff.UpdatedControllers.Concat(diff.RemovedControllers))
        {
            // Convert normalized path to file URI
            string fileUri = PathUtils.FilePathToUri(ctrlPath);
            affected.Add(fileUri);
        }

        // For updated/removed types, find all controllers that reference them
        if (diff.UpdatedTypes.Count > 0 || diff.RemovedTypes.Count > 0)
        {
            var changedTypeNames = new HashSet<string>(
                diff.UpdatedTypes.Concat(diff.RemovedTypes));

            foreach (var (ctrlPath, ctrl) in _analyzer.GetAllControllers())
            {
                bool references = ctrl.Properties.Any(p =>
                    changedTypeNames.Contains(ProjectAnalyzer.ExtractCoreTypeName(p.Type)));

                if (references)
                {
                    string fileUri = PathUtils.FilePathToUri(ctrlPath);
                    affected.Add(fileUri);
                }
            }

            // Type changes (Control-derived) may affect any guml using that component
            // For simplicity, if any Control type changed, re-publish all open docs
            if (_workspace != null)
            {
                var controlTypeNames = changedTypeNames.Where(name =>
                    _analyzer.ApiDocument.Types.TryGetValue(name, out var td)
                    && td.BaseType != null).ToList();

                if (controlTypeNames.Count > 0)
                {
                    foreach (var doc in _workspace.GetAllDocuments())
                        affected.Add(doc.Uri);
                }
            }
        }

        return affected;
    }

    private void PushApiCacheUpdated(ApiCacheDiff? diff)
    {
        if (diff == null)
        {
            // Full rebuild — send signal without details
            _transport.SendNotification("apiCacheUpdated", new
            {
                fullRebuild = true
            });
        }
        else
        {
            _transport.SendNotification("apiCacheUpdated", new
            {
                fullRebuild = false,
                updatedTypes = diff.UpdatedTypes,
                removedTypes = diff.RemovedTypes,
                updatedControllers = diff.UpdatedControllers,
                removedControllers = diff.RemovedControllers
            });
        }
    }

    private void SendStatus(string status, string? message = null)
    {
        _transport.SendNotification("serverStatus", new { status, message });
    }

    private void PublishDiagnostics(GumlDocument doc)
    {
        if (_workspace == null) return;
        var model = _workspace.GetSemanticModel(doc.Uri);
        if (model == null) return;
        var diagnostics = DiagnosticsPublisher.CollectDiagnostics(doc, model);
        _transport.SendNotification("textDocument/publishDiagnostics", new { uri = doc.Uri, diagnostics });
    }

    // ══════════════════════════════════════════════════════════
    //  Parameter extraction helpers
    // ══════════════════════════════════════════════════════════

    private GumlDocument? ExtractDocument(JsonRpcRequest request)
    {
        if (_workspace == null || !request.Params.HasValue) return null;
        var p = request.Params.Value;
        if (!p.TryGetProperty("textDocument", out var td)) return null;
        string uri = td.GetProperty("uri").GetString() ?? "";
        return _workspace.GetDocument(uri);
    }

    private (GumlDocument? doc, LspPosition position) ExtractDocumentPosition(JsonRpcRequest request)
    {
        var doc = ExtractDocument(request);
        if (doc == null || !request.Params.HasValue)
            return (null, default);

        var p = request.Params.Value;
        if (!p.TryGetProperty("position", out var pos))
            return (doc, default);

        int line = pos.GetProperty("line").GetInt32();
        int character = pos.GetProperty("character").GetInt32();
        return (doc, new LspPosition(line, character));
    }

    private static FormattingOptions ExtractFormattingOptions(JsonRpcRequest request)
    {
        var options = new FormattingOptions();
        if (request.Params.HasValue &&
            request.Params.Value.TryGetProperty("options", out var opts))
        {
            if (opts.TryGetProperty("tabSize", out var ts))
                options.TabSize = ts.GetInt32();
            if (opts.TryGetProperty("insertSpaces", out var ins))
                options.InsertSpaces = ins.GetBoolean();
        }

        return options;
    }

    private static LspRange? ExtractRange(JsonRpcRequest request)
    {
        if (!request.Params.HasValue) return null;
        if (!request.Params.Value.TryGetProperty("range", out var range)) return null;

        var start = range.GetProperty("start");
        var end = range.GetProperty("end");
        return new LspRange(
            new LspPosition(start.GetProperty("line").GetInt32(), start.GetProperty("character").GetInt32()),
            new LspPosition(end.GetProperty("line").GetInt32(), end.GetProperty("character").GetInt32()));
    }
}
