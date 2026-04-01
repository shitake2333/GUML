#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using GUML;

namespace TestApplication.scripts.notebook;

/// <summary>
/// A Godot <see cref="Control"/> that renders parsed Markdown blocks using
/// CanvasItem draw calls (<c>DrawString</c>, <c>DrawRect</c>).
/// Height is determined by content and written to <see cref="Control.CustomMinimumSize"/>.
/// </summary>
public partial class MarkdownCellNode : Control
{
    // -----------------------------------------------------------------------
    // Fonts & metrics
    // -----------------------------------------------------------------------
    private static readonly float s_lineSpacing    = 4f;
    private static readonly float s_paragraphGap   = 12f;
    private static readonly float s_listIndent     = 20f;
    private static readonly float s_blockPaddingH  = 16f;
    private static readonly float s_blockPaddingV  = 12f;
    // Horizontal margin applied to embedded code / live-preview blocks (VSCode style).
    private static readonly float s_embedMarginH   = 16f;

    // Cached StyleBox for inline-code background (rounded corners, allocated once).
    private StyleBoxFlat? _inlineCodeStyle;

    // -----------------------------------------------------------------------
    // Parsed content
    // -----------------------------------------------------------------------
    private IReadOnlyList<IMarkdownBlock> _blocks = [];
    private string _rawMarkdown = "";

    // -----------------------------------------------------------------------
    // Cached layout
    // -----------------------------------------------------------------------
    private float _cachedWidth = -1;
    private float _cachedHeight;
    // Each entry stores the INDIVIDUAL WORD text, not the full run.
    private List<(Vector2 pos, string text, InlineStyle inlineStyle, FontStyle blockStyle, int fontSize)> _drawItems = [];
    private List<(float x, float y, float width)> _hRules = [];
    private readonly Dictionary<int, Control> _embeddedControls = [];
    private List<(Rect2 rect, Color color, bool filled)> _rectDraws = [];
    // Clickable link areas: (bounding rect, url).
    private List<(Rect2 rect, string url)> _linkRects = [];
    // Whether _Layout is already on the call stack (prevents re-entrance from MinimumSizeChanged).
    private bool _layoutInProgress;

    // -----------------------------------------------------------------------
    // API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Optional callback invoked when the user clicks a hyperlink inside this cell.
    /// The argument is the raw URL string from the Markdown source.
    /// </summary>
    public Action<string>? OnLinkClicked { get; set; }

    /// <summary>Sets the Markdown string to render.</summary>
    public void SetMarkdown(string markdown)
    {
        _rawMarkdown = markdown;

        // Free old embedded children.
        foreach (var ctrl in _embeddedControls.Values)
            ctrl.QueueFree();
        _embeddedControls.Clear();

        _blocks = MarkdownParser.Parse(_rawMarkdown);

        // Pre-create embedded child nodes for code and live-preview blocks.
        for (int i = 0; i < _blocks.Count; i++)
        {
            switch (_blocks[i])
            {
                case CodeBlock cb:
                {
                    var cv = new CodeViewCellNode();
                    AddChild(cv);
                    cv.SetCode(cb.Code, cb.Language);
                    // Re-layout when child collapses/expands (CustomMinimumSize changes).
                    cv.MinimumSizeChanged += _OnEmbeddedMinSizeChanged;
                    _embeddedControls[i] = cv;
                    break;
                }
                case GumlLiveBlock glb:
                {
                    var gv = new GumlViewCellNode();
                    AddChild(gv);
                    var type = _ResolveType(glb.ControllerTypeName);
                    if (type != null)
                        gv.LoadController(type, glb.Height);
                    else
                        gv.LoadDefault(glb.Height);
                    // Re-layout when child collapses/expands.
                    gv.MinimumSizeChanged += _OnEmbeddedMinSizeChanged;
                    _embeddedControls[i] = gv;
                    break;
                }
            }
        }

        _cachedWidth = -1; // invalidate layout
        _Layout();
    }

    // -----------------------------------------------------------------------
    // Godot overrides
    // -----------------------------------------------------------------------

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            if (Math.Abs(Size.X - _cachedWidth) > 0.5f)
                _Layout();
        }
    }

    // Called when an embedded child's CustomMinimumSize changes (collapse/expand).
    private void _OnEmbeddedMinSizeChanged() => _Layout();

    public override void _Draw()
    {
        // Background matches Godot dark UI panel (~#282828)
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.16f, 0.16f, 0.16f));

        foreach (var (rect, color, filled) in _rectDraws)
            DrawRect(rect, color, filled);

        foreach (var (pos, text, inlineStyle, blockStyle, fontSize) in _drawItems)
        {
            var font  = GetFont(inlineStyle, blockStyle);
            Color color;
            if ((inlineStyle & InlineStyle.InlineCode) != 0)
                color = new Color(1.0f, 0.78f, 0.45f);   // warm amber for inline code
            else if ((inlineStyle & InlineStyle.Link) != 0)
                color = new Color(0.35f, 0.65f, 1.0f);   // blue for hyperlinks
            else
                color = new Color(0.86f, 0.86f, 0.86f);  // Godot default text color

            if ((inlineStyle & InlineStyle.InlineCode) != 0)
            {
                float ascent  = font.GetAscent(fontSize);
                float descent = font.GetDescent(fontSize);
                float textW   = font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize).X;
                // Rounded inline-code background (VSCode amber tint with 3px corners).
                _inlineCodeStyle ??= new StyleBoxFlat
                {
                    BgColor = new Color(0.26f, 0.24f, 0.18f),
                    CornerRadiusTopLeft     = 3,
                    CornerRadiusTopRight    = 3,
                    CornerRadiusBottomLeft  = 3,
                    CornerRadiusBottomRight = 3,
                };
                DrawStyleBox(_inlineCodeStyle,
                    new Rect2(pos.X - 3, pos.Y - ascent - 2, textW + 6, ascent + descent + 2));
            }

            DrawString(font, pos, text, HorizontalAlignment.Left, -1, fontSize, color);

            // Underline for links
            if ((inlineStyle & InlineStyle.Link) != 0)
            {
                float descent = font.GetDescent(fontSize);
                float textW   = font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize).X;
                DrawLine(new Vector2(pos.X, pos.Y + descent - 1),
                         new Vector2(pos.X + textW, pos.Y + descent - 1),
                         new Color(0.35f, 0.65f, 1.0f, 0.7f));
            }
        }

        foreach (var (x, y, w) in _hRules)
            DrawRect(new Rect2(x, y, w, 1), new Color(0.35f, 0.35f, 0.35f));
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb
            && OnLinkClicked != null)
        {
            var localPos = mb.Position;
            foreach (var (rect, url) in _linkRects)
            {
                if (rect.HasPoint(localPos))
                {
                    OnLinkClicked.Invoke(url);
                    AcceptEvent();
                    return;
                }
            }
        }
    }

    // -----------------------------------------------------------------------
    // Layout
    // -----------------------------------------------------------------------

    private void _Layout()
    {
        if (_layoutInProgress) return;
        _layoutInProgress = true;
        try
        {
            _LayoutImpl();
        }
        finally
        {
            _layoutInProgress = false;
        }
    }

    private void _LayoutImpl()
    {
        _cachedWidth = Size.X;
        _drawItems  = [];
        _hRules     = [];
        _rectDraws  = [];
        _linkRects  = [];

        if (_blocks.Count == 0)
        {
            CustomMinimumSize = new Vector2(0, s_blockPaddingV * 2);
            QueueRedraw();
            return;
        }

        float availW = Math.Max(Size.X - s_blockPaddingH * 2, 60f);
        float x0     = s_blockPaddingH;
        float y      = s_blockPaddingV;

        for (int idx = 0; idx < _blocks.Count; idx++)
        {
            var block = _blocks[idx];
            switch (block)
            {
                case HeadingBlock h:
                {
                    int size  = h.Level switch { 1 => 24, 2 => 20, _ => 17 };
                    y = LayoutInlines(h.Inlines, x0, y, availW, FontStyle.Bold, size) + s_paragraphGap;
                    break;
                }
                case ParagraphBlock p:
                {
                    y = LayoutInlines(p.Inlines, x0, y, availW, FontStyle.Normal, 14) + s_paragraphGap;
                    break;
                }
                case ListItemBlock li:
                {
                    float itemIndent = li.Depth * s_listIndent;
                    var bulletFont = GetFont(InlineStyle.Normal, FontStyle.Normal);
                    float bh = bulletFont.GetAscent(14);
                    string bullet = li.Depth switch { 0 => "•", 1 => "◦", _ => "▪" };
                    _drawItems.Add((new Vector2(x0 + itemIndent, y + bh), bullet, InlineStyle.Normal, FontStyle.Normal, 14));
                    y = LayoutInlines(li.Inlines, x0 + itemIndent + s_listIndent, y, availW - itemIndent - s_listIndent, FontStyle.Normal, 14) + s_lineSpacing;
                    break;
                }
                case HRuleBlock:
                {
                    // Store for drawing in _Draw() — DrawRect cannot be called here.
                    _hRules.Add((x0, y + 6, availW));
                    y += 14 + s_paragraphGap;
                    break;
                }
                case CodeBlock when _embeddedControls.TryGetValue(idx, out var cv):
                {
                    y += s_lineSpacing;
                    float embedX = s_embedMarginH;
                    float embedW = Math.Max(Size.X - s_embedMarginH * 2, 60f);
                    cv.Position = new Vector2(embedX, y);
                    float ch = Math.Max(cv.CustomMinimumSize.Y, 60f);
                    cv.Size = new Vector2(embedW, ch);
                    y += ch + s_lineSpacing;
                    break;
                }
                case GumlLiveBlock when _embeddedControls.TryGetValue(idx, out var gv):
                {
                    y += s_lineSpacing;
                    float embedX = s_embedMarginH;
                    float embedW = Math.Max(Size.X - s_embedMarginH * 2, 60f);
                    gv.Position = new Vector2(embedX, y);
                    float gh = Math.Max(gv.CustomMinimumSize.Y, 60f);
                    gv.Size = new Vector2(embedW, gh);
                    y += gh + s_lineSpacing;
                    break;
                }
                case TableBlock tb:
                    y = _LayoutTable(tb, x0, y, availW) + s_paragraphGap;
                    break;
            }
        }

        _cachedHeight = y + s_blockPaddingV;
        CustomMinimumSize = new Vector2(0, _cachedHeight);
        QueueRedraw();
    }

    // Lay out inline runs with word-wrap, returns new Y after last line.
    // Each WORD is stored as a separate draw item with its own position.
    private float LayoutInlines(IReadOnlyList<InlineRun> inlines, float x0, float y,
                                  float maxW, FontStyle blockStyle, int fontSize)
    {
        float x     = x0;
        float lineH = 0;

        foreach (var run in inlines)
        {
            var font  = GetFont(run.Style, blockStyle);
            string[] words = run.Text.Split(' ');

            for (int wi = 0; wi < words.Length; wi++)
            {
                // Preserve trailing space for X-advance, but draw without it to avoid ragged right edge.
                string wordWithSpace = words[wi] + (wi < words.Length - 1 ? " " : "");
                var wSize = font.GetStringSize(wordWithSpace, HorizontalAlignment.Left, -1, fontSize);

                if (wSize.X <= 0) continue; // skip empty tokens

                if (x > x0 && x + wSize.X > x0 + maxW)
                {
                    y    += lineH + s_lineSpacing;
                    x     = x0;
                    lineH = 0;
                }

                // Y position = top of line + ascent → gives correct DrawString baseline.
                float ascent = font.GetAscent(fontSize);
                _drawItems.Add((new Vector2(x, y + ascent), wordWithSpace, run.Style, blockStyle, fontSize));

                // Record clickable rect for links.
                if ((run.Style & InlineStyle.Link) != 0 && !string.IsNullOrEmpty(run.Url))
                {
                    float lineHeight = font.GetHeight(fontSize);
                    _linkRects.Add((new Rect2(x, y, wSize.X, lineHeight), run.Url));
                }

                x    += wSize.X;
                lineH = Math.Max(lineH, font.GetHeight(fontSize));
            }
        }

        return y + lineH;
    }

    // -----------------------------------------------------------------------
    // Font helpers
    // -----------------------------------------------------------------------

    private Font GetFont(InlineStyle inlineStyle, FontStyle blockStyle)
    {
        bool bold   = (inlineStyle & InlineStyle.Bold)       != 0 || blockStyle == FontStyle.Bold;
        bool italic = (inlineStyle & InlineStyle.Italic)     != 0;
        bool code   = (inlineStyle & InlineStyle.InlineCode) != 0;

        if (code)   return GetThemeFont("normal_font", "RichTextLabel");
        if (bold)   return GetThemeFont("bold_font",   "RichTextLabel");
        if (italic) return GetThemeFont("italics_font","RichTextLabel");
        return          GetThemeFont("normal_font", "RichTextLabel");
    }

    private enum FontStyle { Normal, Bold }

    // -----------------------------------------------------------------------
    // Table layout
    // -----------------------------------------------------------------------

    private float _LayoutTable(TableBlock table, float x0, float y, float availW)
    {
        const float rowH   = 26f;
        const float cellPh = 6f;

        if (table.Headers.Count == 0) return y;

        int   cols      = table.Headers.Count;
        float colW      = availW / cols;
        var   headerFont = GetFont(InlineStyle.Bold, FontStyle.Bold);
        var   bodyFont   = GetFont(InlineStyle.Normal, FontStyle.Normal);

        // Header row background
        _rectDraws.Add((new Rect2(x0, y, availW, rowH), new Color(0.20f, 0.20f, 0.22f), true));

        float hAsc = headerFont.GetAscent(13);
        float hH   = headerFont.GetHeight(13);
        for (int c = 0; c < cols; c++)
        {
            _drawItems.Add((
                new Vector2(x0 + c * colW + cellPh, y + hAsc + (rowH - hH) * 0.5f),
                table.Headers[c], InlineStyle.Bold, FontStyle.Bold, 13));
        }
        y += rowH;

        float bAsc = bodyFont.GetAscent(13);
        float bH   = bodyFont.GetHeight(13);
        for (int r = 0; r < table.Rows.Count; r++)
        {
            if (r % 2 == 0)
                _rectDraws.Add((new Rect2(x0, y, availW, rowH), new Color(0.18f, 0.18f, 0.20f), true));

            var row = table.Rows[r];
            for (int c = 0; c < Math.Min(cols, row.Count); c++)
            {
                _drawItems.Add((
                    new Vector2(x0 + c * colW + cellPh, y + bAsc + (rowH - bH) * 0.5f),
                    row[c], InlineStyle.Normal, FontStyle.Normal, 13));
            }
            y += rowH;
        }

        // Outer border
        float tableH = rowH * (1 + table.Rows.Count);
        _rectDraws.Add((new Rect2(x0, y - tableH, availW, tableH), new Color(0.30f, 0.30f, 0.32f), false));

        return y;
    }

    // -----------------------------------------------------------------------
    // Type resolver for GumlLiveBlock controller names
    // -----------------------------------------------------------------------

    /// <summary>
    /// Resolves a controller type name to a <see cref="Type"/> by searching
    /// <see cref="Guml.ControllerRegistry"/> keys (populated by [ModuleInitializer]).
    /// This is more reliable than Assembly.GetType() which depends on assembly-load
    /// order and full namespace qualification at call time.
    /// </summary>
    private static Type? _ResolveType(string typeName)
    {
        // Primary: look up the exact Type from the registry keyed by [ModuleInitializer].
        foreach (var type in Guml.ControllerRegistry.Keys)
        {
            if (type.Name == typeName)
                return type;
        }

        // Fallback: reflection-based search (slower, kept for custom/non-generated controllers).
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("TestApplication.scripts.examples." + typeName)
                 ?? asm.GetType("TestApplication.scripts.notebook." + typeName)
                 ?? asm.GetType(typeName);
            if (t != null) return t;
        }

        GD.PrintErr($"[MarkdownCellNode] Could not resolve controller type '{typeName}'. " +
                    $"Registry has {Guml.ControllerRegistry.Count} entries: " +
                    string.Join(", ", Guml.ControllerRegistry.Keys.Select(k => k.Name)));
        return null;
    }
}
