#nullable enable
using Godot;
using System.Collections.Generic;

namespace TestApplication.scripts.notebook;

// ---------------------------------------------------------------------------
// Notebook data model types – define the content of a showcase page.
// ---------------------------------------------------------------------------

/// <summary>Marker interface for all notebook cell types.</summary>
public interface INotebookCell;

/// <summary>A cell that renders Markdown-formatted explanatory text.</summary>
/// <param name="Markdown">Markdown source text.</param>
public record MarkdownCell(string Markdown) : INotebookCell;

/// <summary>A cell that renders syntax-highlighted source code.</summary>
/// <param name="Code">Source code text.</param>
/// <param name="LanguageId">Language identifier: <c>"guml"</c> or <c>"csharp"</c>.</param>
/// <param name="Title">Optional display title (e.g. file name).</param>
public record CodeViewCell(string Code, string LanguageId, string? Title = null) : INotebookCell;

/// <summary>
/// A cell that embeds a live GUML component in an isolated SubViewport sandbox.
/// </summary>
/// <param name="ControllerType">The controller type to load via <c>Guml.Load</c>.</param>
/// <param name="MinHeight">Minimum height of the sandbox area in pixels.</param>
/// <param name="Title">Optional cell title label. Defaults to "Live Preview".</param>
public record GumlViewCell(System.Type ControllerType, float MinHeight = 300f, string? Title = null) : INotebookCell;

/// <summary>An ordered collection of cells that defines one showcase example page.</summary>
/// <param name="Cells">Ordered cell list.</param>
public record NotebookDocument(IReadOnlyList<INotebookCell> Cells);

// ---------------------------------------------------------------------------
// Showcase example item
// ---------------------------------------------------------------------------

/// <summary>
/// Metadata and unified Markdown content for one showcase example.
/// The <see cref="Markdown"/> string is a full Markdown document that may
/// contain fenced <c>```guml</c> code blocks (syntax-highlighted) and
/// fenced <c>```guml-live ControllerType Height</c> blocks (live preview).
/// </summary>
public record ShowcaseExampleItem(
    string Key,
    string Title,
    string Category,
    string Description,
    string Markdown);

// ---------------------------------------------------------------------------
// Rendering IR – consumed by _Draw() overrides, produced by parsers/tokenizers.
// ---------------------------------------------------------------------------

/// <summary>A single text run with a foreground color, produced by the code highlighter.</summary>
/// <param name="Text">Text content of this span.</param>
/// <param name="Foreground">Foreground color to paint this span with.</param>
public record StyledSpan(string Text, Color Foreground);

/// <summary>All colored spans that make up one line of highlighted code.</summary>
public record LineTokens(IReadOnlyList<StyledSpan> Spans);

/// <summary>Inline text style flags for Markdown rendering.</summary>
[System.Flags]
public enum InlineStyle
{
    Normal     = 0,
    Bold       = 1 << 0,
    Italic     = 1 << 1,
    InlineCode = 1 << 2,
    Link       = 1 << 3,
}

/// <summary>A run of inline text with associated style flags.</summary>
/// <param name="Text">Text content.</param>
/// <param name="Style">Style flags applied to this run.</param>
/// <param name="Url">Optional hyperlink URL; non-null when <see cref="InlineStyle.Link"/> is set.</param>
public record InlineRun(string Text, InlineStyle Style, string? Url = null);

/// <summary>Marker interface for a parsed Markdown block.</summary>
public interface IMarkdownBlock;

/// <summary>Heading block (H1–H3).</summary>
/// <param name="Level">Heading level: 1, 2, or 3.</param>
/// <param name="Inlines">Inline runs comprising the heading text.</param>
public record HeadingBlock(int Level, IReadOnlyList<InlineRun> Inlines) : IMarkdownBlock;

/// <summary>Paragraph block.</summary>
/// <param name="Inlines">Inline runs comprising the paragraph.</param>
public record ParagraphBlock(IReadOnlyList<InlineRun> Inlines) : IMarkdownBlock;

/// <summary>An item within a list.</summary>
/// <param name="Inlines">Inline text of the list item.</param>
/// <param name="Depth">Nesting depth (0 = top level).</param>
public record ListItemBlock(IReadOnlyList<InlineRun> Inlines, int Depth = 0) : IMarkdownBlock;

/// <summary>A horizontal rule / thematic break.</summary>
public record HRuleBlock : IMarkdownBlock;

/// <summary>A fenced code block (``` lang ... ```).</summary>
/// <param name="Language">Language identifier string, e.g. "guml", "csharp".</param>
/// <param name="Code">Raw source code text.</param>
public record CodeBlock(string Language, string Code) : IMarkdownBlock;

/// <summary>
/// A live GUML preview directive embedded in Markdown via a fenced block
/// with language tag <c>guml-live ControllerTypeName Height</c>.
/// </summary>
/// <param name="ControllerTypeName">Simple class name of the GuiController to load.</param>
/// <param name="Height">Minimum height of the live preview area in pixels.</param>
public record GumlLiveBlock(string ControllerTypeName, float Height) : IMarkdownBlock;

/// <summary>A pipe-table block.</summary>
/// <param name="Headers">Plain-text header cell strings.</param>
/// <param name="Rows">Data rows; each row is a list of plain-text cell strings.</param>
public record TableBlock(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows) : IMarkdownBlock;


