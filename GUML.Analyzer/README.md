# GUML.Analyzer

GUML language analyzer for Godot .NET — provides Roslyn-based project analysis and LSP features via JSON-RPC.

Packaged as a [.NET global tool](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools) under the command name `guml-analyzer`.

## Installation

```bash
# Install from NuGet
dotnet tool install GUML.Analyzer -g

# Install from local build artifacts
dotnet pack GUML.Analyzer/GUML.Analyzer.csproj -c Release -o ./artifacts
dotnet tool install GUML.Analyzer -g --add-source ./artifacts
```

## CLI Commands

### LSP Server (default)

When invoked without a subcommand, starts the language server communicating via JSON-RPC 2.0 over stdin/stdout.

```bash
guml-analyzer            # Start LSP server
guml-analyzer --restart  # Stop existing instances first, then start
```

### `check` — Static Analysis

Run static analysis on all `.guml` files in a Godot project.

```bash
guml-analyzer check [--path <dir>] [--format text|json] [--severity error|warning|info|hint]
```

| Option | Default | Description |
|--------|---------|-------------|
| `--path` | Current directory | Project root directory |
| `--format` | `text` | Output format: `text` or `json` |
| `--severity` | `info` | Minimum severity level filter |

**Exit codes:** `0` = no errors, `1` = errors found, `2` = internal failure.

### `format` — Code Formatting

Format all `.guml` files in a project.

```bash
guml-analyzer format [--path <dir>] [--dry-run] [--tab-size N] [--use-tabs]
```

| Option | Default | Description |
|--------|---------|-------------|
| `--path` | Current directory | Project root directory |
| `--dry-run` | `false` | Report files needing formatting without modifying them |
| `--tab-size` | `4` | Number of spaces per indentation level |
| `--use-tabs` | `false` | Use tab characters instead of spaces |

**Exit codes:** `0` = all formatted (or already formatted), `1` = dry-run found changes needed, `2` = error.

### `generate-api` — Godot API Metadata Generation

Generate Godot API metadata from XML documentation for enhanced LSP features.

```bash
guml-analyzer generate-api [--godot-version <ver>] [--godot-source <dir>] [--output <path>]
```

| Option | Default | Description |
|--------|---------|-------------|
| `--godot-version` | Auto-detected from `.csproj` | Godot version (e.g. `4.6.0`) |
| `--godot-source` | Downloads from GitHub | Path to local Godot source tree |
| `--output` | `~/.guml/api/godot_api_<version>.json` | Output file path |

### `stop` / `status` — Instance Management

```bash
guml-analyzer stop     # Stop all running instances
guml-analyzer status   # Show running instance info
```

### Transport Layer

- **Protocol:** JSON-RPC 2.0 with Content-Length header framing (LSP-compatible)
- **Input:** stdin (requests from IDE plugin)
- **Output:** stdout (responses/notifications to IDE plugin)
- **Logging:** stderr (Serilog)

### Process Control

Each running instance registers itself via:
- **PID file** at `$TEMP/guml-analyzer/<pid>.pid`
- **Named Pipe** `guml-analyzer-<pid>` for graceful shutdown commands

## LSP Capabilities

The server advertises these capabilities in the `initialize` response:

| Feature | Handler | Details |
|---------|---------|---------|
| Text Document Sync | — | Full sync (`openClose: true`, `change: Full`) |
| Completion | `CompletionHandler` | Trigger characters: `. $ # @ : =` |
| Hover | `HoverHandler` | Component types, properties, keywords, operators |
| Go to Definition | `DefinitionHandler` | Imports, types, properties, params, aliases, events |
| Document Formatting | `FormattingHandler` | Full document and range formatting |
| Semantic Tokens | `SemanticTokensHandler` | Full and range modes |
| Document Highlight | `DocumentHighlightHandler` | Read/write occurrence highlighting |
| Rename | `RenameHandler` | With `prepareRename` support |
| Diagnostics | `DiagnosticsPublisher` | Published via `textDocument/publishDiagnostics` |

### Completion Contexts

Completions are context-aware and provided for:
- **Component body:** properties, signals, child components, keywords
- **Property values:** enum values, resource constructors, boolean literals
- **Component names:** SDK types + imported components
- **Member access:** `$controller.member`, `$node.property`
- **Event references:** `@eventName`
- **Object literal body:** keys matching target type properties

### Definition Targets

Go-to-definition resolves:
- **Import paths** → `.guml` file locations
- **`$controller`** → C# controller class source
- **Component names** → source declaration or metadata-as-source stub
- **Properties** → declaring type (walks inheritance chain)
- **Params / each variables / aliases / events** → their declarations

### Rename Symbols

Rename supports these symbol kinds:
- Import aliases
- `param` declarations and references
- `event` declarations and references
- Controller member references
- `each` loop variables
- Named nodes (`@name`)

## IDE Plugin Integration

IDE plugins (e.g. the VS Code extension in `IDESupport/VSC/`) integrate as follows:

1. **Spawn** `guml-analyzer` as a child process (no subcommand = LSP mode)
2. **Communicate** via stdin/stdout JSON-RPC with Content-Length framing
3. **Send `initialize`** with `initializationOptions.workspaceRoot` (and optionally `projectPath`)
4. **Handle `projectCandidates`** notification if multiple `.csproj` files exist, respond with `selectProject`
5. **Handle `guml/missingGodotApi`** to prompt users to generate API data
6. **Receive `guml/apiCacheUpdated`** for incremental type/controller updates
7. **Use standard LSP** for editing features (completion, hover, diagnostics, etc.)


## Project Analysis

The `ProjectAnalyzer` uses Roslyn's `MSBuildWorkspace` to:

1. **Locate** the `.csproj` file in the workspace root
2. **Detect** the Godot SDK version from the project file
3. **Load** the Godot API catalog (external JSON or built-in fallback)
4. **Scan** all `Control`-derived types and their properties via compilation analysis
5. **Build** an `ApiDocument` containing type descriptors, properties, events, and controller mappings
6. **Watch** `.cs` files for changes and incrementally rebuild the cache (500ms debounce)
7. **Emit** `ApiCacheDiff` events describing what changed (added/updated/removed types and controllers)

### Godot API Catalog

API metadata is loaded from (in priority order):
1. **External JSON** at `~/.guml/api/godot_api_<version>.json` (generated by `generate-api`)
2. **Project-local** `.guml/api/` directory
3. **Built-in fallback** from `PropertySignalCatalog` (limited to property→signal mappings)

The external catalog provides rich data: property descriptions, signal parameters, theme overrides, enums, and methods — parsed from Godot's XML class documentation by `GodotDocParser`.

