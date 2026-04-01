#nullable enable
using Godot;
using System;
using GUML;

namespace TestApplication.scripts.notebook;

/// <summary>
/// A Godot <see cref="Control"/> that hosts a live GUML example inside a
/// rounded container. The controller is loaded via <see cref="Guml.ControllerRegistry"/>.
/// </summary>
public partial class GumlViewCellNode : Control
{
    // -----------------------------------------------------------------------
    // Layout constants
    // -----------------------------------------------------------------------
    private const float LabelH         = 20f;  // floating label area at the bottom
    private const float ContentPadding = 8f;
    private const float CornerRadius   = 6f;

    // -----------------------------------------------------------------------
    // State
    // -----------------------------------------------------------------------
    private float        _minH           = 300f;
    private GuiController? _controller;
    private Type?        _controllerType;
    private string       _title          = "";

    // -----------------------------------------------------------------------
    // Children / style
    // -----------------------------------------------------------------------
    // The GUML-generated root node, added directly as our child.
    private Control?      _gumlRoot;
    private StyleBoxFlat? _bgStyle;

    // -----------------------------------------------------------------------
    // API
    // -----------------------------------------------------------------------

    /// <summary>Loads the GUML example identified by <paramref name="controllerType"/>.</summary>
    public void LoadController(Type controllerType, float minHeight = 300f, string? title = null)
    {
        _controllerType = controllerType;
        _minH           = minHeight;
        _title          = title ?? controllerType.Name.Replace("Controller", "");
        ClipContents    = true;
        _ReloadController();
    }

    /// <summary>Disposes the current controller and reloads a fresh instance.</summary>
    public void Reset()
    {
        _DisposeController();
        _ReloadController();
    }

    /// <summary>
    /// Initialises the host region with <paramref name="minHeight"/> but without a
    /// controller (shown as an empty preview area when the type could not be resolved).
    /// </summary>
    public void LoadDefault(float minHeight = 300f)
    {
        _minH        = minHeight;
        ClipContents = true;
        _UpdateHostLayout();
    }

    // -----------------------------------------------------------------------
    // Godot overrides
    // -----------------------------------------------------------------------

    public override void _Draw()
    {
        // Single rounded background with border — label floats inside the top-left corner.
        _bgStyle ??= new StyleBoxFlat
        {
            BgColor              = new Color(0.14f, 0.14f, 0.16f),
            BorderColor          = new Color(0.28f, 0.28f, 0.32f),
            BorderWidthLeft      = 1,
            BorderWidthTop       = 1,
            BorderWidthRight     = 1,
            BorderWidthBottom    = 1,
            CornerRadiusTopLeft     = (int)CornerRadius,
            CornerRadiusTopRight    = (int)CornerRadius,
            CornerRadiusBottomLeft  = (int)CornerRadius,
            CornerRadiusBottomRight = (int)CornerRadius,
        };
        DrawStyleBox(_bgStyle, new Rect2(Vector2.Zero, Size));

        // "LIVE ControllerName" label floated inside the BOTTOM-left corner
        // (avoids overlapping the content area at the top).
        var font   = GetThemeFont("bold_font", "RichTextLabel");
        float textY = Size.Y - LabelH * 0.5f + font.GetAscent(10) * 0.5f - 2f;
        DrawString(font, new Vector2(ContentPadding + 2, textY),
                   "LIVE", HorizontalAlignment.Left, -1, 10, new Color(0.40f, 0.82f, 0.52f));
        if (!string.IsNullOrEmpty(_title))
        {
            float liveW = font.GetStringSize("LIVE ", HorizontalAlignment.Left, -1, 10).X;
            DrawString(font, new Vector2(ContentPadding + 2 + liveW, textY),
                       _title, HorizontalAlignment.Left, -1, 10, new Color(0.40f, 0.40f, 0.45f));
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            // Push size directly; plain Control parent never propagates NOTIFICATION_PARENT_RESIZED.
            if (_gumlRoot != null)
            {
                _gumlRoot.Position = new Vector2(ContentPadding, ContentPadding);
                _gumlRoot.Size = new Vector2(
                    Math.Max(0, Size.X - ContentPadding * 2),
                    Math.Max(0, Size.Y - ContentPadding - LabelH));
            }
            QueueRedraw();
        }
    }

    public override void _ExitTree()
    {
        _DisposeController();
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private void _UpdateHostLayout()
    {
        // Height: padding + content + label bar at the bottom.
        CustomMinimumSize = new Vector2(0, ContentPadding + _minH + LabelH);
        QueueRedraw();
    }

    private void _ReloadController()
    {
        if (_controllerType == null) return;

        if (!Guml.ControllerRegistry.TryGetValue(_controllerType, out var factory))
        {
            GD.PrintErr($"[GumlViewCellNode] No source-generated view found for '{_controllerType.Name}'. " +
                        "Ensure the controller has a [GumlController] attribute and the source generator ran.");
            return;
        }

        try
        {
            // factory(parent) adds the generated root node to `parent` and returns the controller.
            var result = factory(this);
            _controller = result;

            // Grab the last-added child as the GUML root (factory does exactly one AddChild).
            if (GetChildCount() > 0)
                _gumlRoot = GetChild(GetChildCount() - 1) as Control;

            _UpdateHostLayout();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GumlViewCellNode] Failed to load controller '{_controllerType.Name}': {ex.Message}");
        }
    }

    private void _DisposeController()
    {
        _controller?.Dispose();
        _controller = null;
        // Remove the old GUML root node from the tree.
        if (_gumlRoot != null)
        {
            _gumlRoot.QueueFree();
            _gumlRoot = null;
        }
    }
}
