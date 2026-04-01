using System.Text.Json.Serialization;

namespace GUML.Analyzer.Utils;

// ── Positions & Ranges ──

/// <summary>
/// A zero-based line/character position in a text document.
/// </summary>
public readonly struct LspPosition
{
    [JsonPropertyName("line")] public int Line { get; }

    [JsonPropertyName("character")] public int Character { get; }

    public LspPosition(int line, int character)
    {
        Line = line;
        Character = character;
    }
}

/// <summary>
/// A range in a text document expressed as start and end positions.
/// </summary>
public readonly struct LspRange
{
    [JsonPropertyName("start")] public LspPosition Start { get; }

    [JsonPropertyName("end")] public LspPosition End { get; }

    public LspRange(LspPosition start, LspPosition end)
    {
        Start = start;
        End = end;
    }
}

/// <summary>
/// Represents a location in a resource (URI + range).
/// </summary>
public sealed class LspLocation
{
    [JsonPropertyName("uri")] public string Uri { get; set; } = "";

    [JsonPropertyName("range")] public LspRange Range { get; set; }
}

// ── Text Edits ──

/// <summary>
/// A textual edit applicable to a text document.
/// </summary>
public sealed class TextEdit
{
    [JsonPropertyName("range")] public LspRange Range { get; set; }

    [JsonPropertyName("newText")] public string NewText { get; set; } = "";
}

// ── Completion ──

/// <summary>
/// Defines the kind of a completion entry.
/// </summary>
public enum CompletionItemKind
{
    Text = 1,
    Method = 2,
    Function = 3,
    Constructor = 4,
    Field = 5,
    Variable = 6,
    Class = 7,
    Interface = 8,
    Module = 9,
    Property = 10,
    Unit = 11,
    Value = 12,
    Enum = 13,
    Keyword = 14,
    Snippet = 15,
    Color = 16,
    File = 17,
    Reference = 18,
    Folder = 19,
    EnumMember = 20,
    Constant = 21,
    Struct = 22,
    Event = 23,
    Operator = 24,
    TypeParameter = 25,
}

/// <summary>
/// A completion item returned from a completion request.
/// </summary>
public sealed class CompletionItem
{
    [JsonPropertyName("label")] public string Label { get; set; } = "";

    [JsonPropertyName("kind")] public CompletionItemKind Kind { get; set; }

    [JsonPropertyName("detail")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; set; }

    [JsonPropertyName("documentation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Documentation { get; set; }

    [JsonPropertyName("insertText")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InsertText { get; set; }

    [JsonPropertyName("sortText")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SortText { get; set; }

    [JsonPropertyName("filterText")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FilterText { get; set; }

    /// <summary>
    /// The format of the insert text. 1 = PlainText, 2 = Snippet.
    /// </summary>
    [JsonPropertyName("insertTextFormat")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int InsertTextFormat { get; set; }
}

// ── Hover ──

/// <summary>
/// The result of a hover request.
/// </summary>
public sealed class HoverResult
{
    [JsonPropertyName("contents")] public MarkupContent Contents { get; set; } = new();

    [JsonPropertyName("range")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LspRange? Range { get; set; }
}

/// <summary>
/// A MarkupContent literal represents a string value with its kind (plaintext or markdown).
/// </summary>
public sealed class MarkupContent
{
    [JsonPropertyName("kind")] public string Kind { get; set; } = "markdown";

    [JsonPropertyName("value")] public string Value { get; set; } = "";
}

// ── Diagnostics ──

/// <summary>
/// LSP diagnostic severity levels.
/// </summary>
public enum DiagnosticSeverity
{
    Error = 1,
    Warning = 2,
    Information = 3,
    Hint = 4,
}

/// <summary>
/// An LSP diagnostic (error, warning, etc.) in a text document.
/// </summary>
public sealed class LspDiagnostic
{
    [JsonPropertyName("range")] public LspRange Range { get; set; }

    [JsonPropertyName("severity")] public DiagnosticSeverity Severity { get; set; }

    [JsonPropertyName("code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; set; }

    [JsonPropertyName("source")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Source { get; set; }

    [JsonPropertyName("message")] public string Message { get; set; } = "";
}

// ── Semantic Tokens ──

/// <summary>
/// Semantic token types used in GUML documents.
/// Order must match the legend sent during initialization.
/// </summary>
public static class SemanticTokenTypes
{
    public const int Keyword = 0;
    public const int Class = 1;
    public const int Property = 2;
    public const int Parameter = 3;
    public const int Variable = 4;
    public const int Event = 5;
    public const int EnumMember = 6;
    public const int String = 7;
    public const int Number = 8;
    public const int Comment = 9;

    public const int Operator = 10;

    public static readonly string[] Legend =
    [
        "keyword", "class", "property", "parameter", "variable",
        "event", "enumMember", "string", "number", "comment", "operator"
    ];
}

/// <summary>
/// Semantic token modifiers used in GUML documents.
/// </summary>
public static class SemanticTokenModifiers
{
    public const int Readonly = 0;
    public const int Declaration = 1;

    public static readonly string[] Legend = ["readonly", "declaration"];
}

/// <summary>
/// The result of a semantic tokens request (relative-encoded integer array).
/// </summary>
public sealed class SemanticTokensResult
{
    [JsonPropertyName("data")] public int[] Data { get; set; } = [];
}

// ── Document Highlight ──

/// <summary>
/// A document highlight kind.
/// </summary>
public enum DocumentHighlightKind
{
    Text = 1,
    Read = 2,
    Write = 3,
}

/// <summary>
/// A document highlight — a range inside a text document which deserves special attention.
/// </summary>
public sealed class DocumentHighlight
{
    [JsonPropertyName("range")] public LspRange Range { get; set; }

    [JsonPropertyName("kind")] public DocumentHighlightKind Kind { get; set; } = DocumentHighlightKind.Text;
}

// ── Rename ──

/// <summary>
/// The result of a prepare-rename request: the range that will be renamed and a placeholder text.
/// </summary>
public sealed class PrepareRenameResult
{
    [JsonPropertyName("range")] public LspRange Range { get; set; }

    [JsonPropertyName("placeholder")] public string Placeholder { get; set; } = "";
}

/// <summary>
/// A workspace edit represents changes to many resources managed in the workspace.
/// </summary>
public sealed class WorkspaceEdit
{
    /// <summary>
    /// Holds changes to existing resources. Keyed by document URI.
    /// </summary>
    [JsonPropertyName("changes")]
    public Dictionary<string, List<TextEdit>> Changes { get; set; } = new();
}

// ── Formatting Options ──

/// <summary>
/// Value-object describing the options to be used during formatting.
/// </summary>
public sealed class FormattingOptions
{
    [JsonPropertyName("tabSize")] public int TabSize { get; set; } = 4;

    [JsonPropertyName("insertSpaces")] public bool InsertSpaces { get; set; } = true;
}
