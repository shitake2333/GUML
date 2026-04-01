#nullable enable
using Godot;
using System;
using System.Collections.Generic;

namespace TestApplication.scripts.notebook;

/// <summary>
/// A Godot <see cref="Control"/> that displays syntax-highlighted source code
/// with a line-number gutter. The background uses rounded corners; a small
/// language label and copy button are overlaid at the top-right corner.
/// </summary>
public partial class CodeViewCellNode : Control
{
    // -----------------------------------------------------------------------
    // Layout constants
    // -----------------------------------------------------------------------
    private const float PaddingH     = 12f;
    private const float PaddingV     = 12f;
    private const float LineHeight   = 20f;
    private const float CornerRadius = 6f;

    // -----------------------------------------------------------------------
    // State
    // -----------------------------------------------------------------------
    private IReadOnlyList<LineTokens> _lines  = [];
    private string _rawCode  = "";
    private string _language = "";

    // -----------------------------------------------------------------------
    // Children
    // -----------------------------------------------------------------------
    private CodeBodyCanvas?  _body;
    private ScrollContainer? _scroll;
    private Button?          _copyBtn;

    // -----------------------------------------------------------------------
    // Style
    // -----------------------------------------------------------------------
    private StyleBoxFlat? _bgStyle;

    // -----------------------------------------------------------------------
    // API
    // -----------------------------------------------------------------------

    /// <summary>Loads code content and triggers a re-layout.</summary>
    public void SetCode(string code, string languageId, string? title = null)
    {
        _rawCode  = code;
        _language = languageId;
        _lines    = TextMateCodeHighlighter.Instance.Tokenize(_rawCode, _language);
        _RefreshBody();
    }

    // -----------------------------------------------------------------------
    // Godot overrides
    // -----------------------------------------------------------------------

    public override void _Ready()
    {
        _BuildTree();
        // If SetCode() was called before _Ready(), replay it now.
        if (_lines.Count > 0)
            _RefreshBody();
    }

    public override void _Draw()
    {
        // Rounded background + 1px border — mirrors the GumlViewCellNode container style.
        _bgStyle ??= new StyleBoxFlat
        {
            BgColor                 = TextMateCodeHighlighter.DefaultBackground,
            BorderColor             = new Color(0.28f, 0.28f, 0.32f),
            BorderWidthLeft         = 1,
            BorderWidthTop          = 1,
            BorderWidthRight        = 1,
            BorderWidthBottom       = 1,
            CornerRadiusTopLeft     = (int)CornerRadius,
            CornerRadiusTopRight    = (int)CornerRadius,
            CornerRadiusBottomLeft  = (int)CornerRadius,
            CornerRadiusBottomRight = (int)CornerRadius,
        };
        DrawStyleBox(_bgStyle, new Rect2(Vector2.Zero, Size));

        // Language label pinned to the top-LEFT corner (right side has the Copy button).
        if (!string.IsNullOrEmpty(_language))
        {
            Font font  = GetThemeFont("bold_font", "RichTextLabel");
            string label = _language.ToUpperInvariant();
            DrawString(font,
                new Vector2(PaddingH, PaddingV + font.GetAscent(11)),
                label, HorizontalAlignment.Left, -1, 11,
                new Color(0.36f, 0.36f, 0.40f));
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            _UpdateScrollSize();
            _RepositionCopyBtn();
            QueueRedraw();
        }
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private void _BuildTree()
    {
        if (_body != null) return; // guard against double init

        // Clip children so they don't bleed outside the rounded corners.
        ClipContents = true;

        _scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode   = ScrollContainer.ScrollMode.Disabled,
        };
        // Transparent scroll panel so parent's rounded StyleBoxFlat bg shows through.
        _scroll.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        AddChild(_scroll);

        _body = new CodeBodyCanvas();
        _scroll.AddChild(_body);

        // Small copy button overlaid at the top-right corner.
        _copyBtn = new Button();
        _copyBtn.Text = "Copy";
        _copyBtn.AddThemeFontSizeOverride("font_size", 10);
        _copyBtn.CustomMinimumSize = new Vector2(44, 19);
        _copyBtn.TooltipText = "Copy code to clipboard";
        AddChild(_copyBtn);
        _copyBtn.Pressed += () => DisplayServer.ClipboardSet(_rawCode);
        _RepositionCopyBtn();
    }

    private void _RepositionCopyBtn()
    {
        if (_copyBtn == null) return;
        var sz   = _copyBtn.CustomMinimumSize;
        float btnX = Size.X - PaddingH - sz.X;
        float btnY = Math.Max(2f, (PaddingV - sz.Y) * 0.5f);
        _copyBtn.Position = new Vector2(btnX, btnY);
        _copyBtn.Size     = sz;
    }

    private void _RefreshBody()
    {
        if (_body == null) _BuildTree();
        if (_body == null) return;

        _body.SetLines(_lines);

        float bodyH = _lines.Count * LineHeight + PaddingV * 2;
        // Add top+bottom inset so the scroll doesn't compress over rounded corners.
        CustomMinimumSize = new Vector2(0, bodyH + CornerRadius * 2);
        if (_scroll != null) _scroll.Visible = true;
        _UpdateScrollSize();
        QueueRedraw();
    }

    private void _UpdateScrollSize()
    {
        if (_scroll == null) return;
        // Inset by CornerRadius so the gutter never covers the rounded corners
        // drawn by the parent StyleBoxFlat (which is purely visual, not a clip mask).
        _scroll.Position = new Vector2(CornerRadius, CornerRadius);
        _scroll.Size     = new Vector2(
            Math.Max(0, Size.X - CornerRadius * 2),
            Math.Max(0, Size.Y - CornerRadius * 2));
    }

    // -----------------------------------------------------------------------
    // Inner control: draws token spans with line-number gutter
    // -----------------------------------------------------------------------

    /// <summary>
    /// Child control that renders syntax-highlighted lines of code with a
    /// line-number gutter on the left.
    /// </summary>
    public partial class CodeBodyCanvas : Control
    {
        private const float GutterW  = 52f;
        private static readonly Color s_gutterBg  = new(0.07f, 0.07f, 0.09f);
        private static readonly Color s_gutterFg  = new(0.40f, 0.40f, 0.46f);
        private static readonly Color s_gutterSep = new(0.20f, 0.20f, 0.24f);

        private IReadOnlyList<LineTokens> _lines = [];

        /// <summary>Updates the tokenized lines and triggers a redraw.</summary>
        public void SetLines(IReadOnlyList<LineTokens> lines)
        {
            _lines = lines;
            float codeW = _MeasureMaxWidth();
            float h     = _lines.Count * LineHeight + PaddingV * 2;
            CustomMinimumSize = new Vector2(GutterW + PaddingH + codeW + PaddingH, h);
            QueueRedraw();
        }

        public override void _Draw()
        {
            // Do NOT draw a full background here — the parent CodeViewCellNode
            // draws the rounded StyleBoxFlat which shows through.

            // Gutter background + right border.
            DrawRect(new Rect2(0, 0, GutterW, Size.Y), s_gutterBg);
            DrawRect(new Rect2(GutterW, 0, 1, Size.Y), s_gutterSep);

            var font = GetThemeFont("source_code_font", "RichTextLabel")
                    ?? GetThemeFont("normal_font",       "RichTextLabel");

            float y = PaddingV + LineHeight * 0.75f; // baseline offset

            for (int i = 0; i < _lines.Count; i++)
            {
                // Line number (right-aligned inside gutter)
                string numText = (i + 1).ToString();
                float numW    = font.GetStringSize(numText, HorizontalAlignment.Left, -1, 12).X;
                DrawString(font, new Vector2(GutterW - numW - 8, y),
                           numText, HorizontalAlignment.Left, -1, 12, s_gutterFg);

                // Code tokens
                float x = GutterW + PaddingH;
                foreach (var span in _lines[i].Spans)
                {
                    DrawString(font, new Vector2(x, y), span.Text,
                               HorizontalAlignment.Left, -1, 13, span.Foreground);
                    x += font.GetStringSize(span.Text, HorizontalAlignment.Left, -1, 13).X;
                }

                y += LineHeight;
            }
        }

        private float _MeasureMaxWidth()
        {
            var font  = GetThemeFont("normal_font", "RichTextLabel");
            float max = 0;
            foreach (var line in _lines)
            {
                float w = 0;
                foreach (var span in line.Spans)
                    w += font.GetStringSize(span.Text, HorizontalAlignment.Left, -1, 13).X;
                if (w > max) max = w;
            }
            return max;
        }
    }
}
