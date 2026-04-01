#nullable enable
using Godot;
using System;

namespace TestApplication.scripts.notebook;

/// <summary>
/// A self-contained Godot UI component that renders notebook content.
/// Internally manages a <see cref="ScrollContainer"/> with a <see cref="VBoxContainer"/>
/// and creates the appropriate cell nodes (Markdown, CodeView, GumlView).
/// </summary>
public partial class NotebookViewer : ScrollContainer
{
    private const float CellGap = 4f;
    private VBoxContainer _cellContainer = null!;

    /// <summary>
    /// Optional callback invoked when the user clicks a hyperlink in the rendered content.
    /// The argument is the raw URL from the Markdown source.
    /// </summary>
    public Action<string>? OnLinkClicked { get; set; }

    // -----------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------

    public override void _Ready()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        _cellContainer = new VBoxContainer();
        _cellContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _cellContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
        _cellContainer.AddThemeConstantOverride("separation", 0);
        AddChild(_cellContainer);
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Renders a single <see cref="ShowcaseExampleItem"/> as a Markdown cell.
    /// </summary>
    public void Load(ShowcaseExampleItem item)
    {
        var node = new MarkdownCellNode();
        node.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        node.SizeFlagsVertical = SizeFlags.ExpandFill;
        node.OnLinkClicked = OnLinkClicked;
        _cellContainer.AddChild(node); // enter tree FIRST so theme fonts are available
        node.SetMarkdown(item.Markdown);
    }

    /// <summary>
    /// Renders all cells in a <see cref="NotebookDocument"/>.
    /// </summary>
    public void Load(NotebookDocument doc)
    {
        foreach (var cell in doc.Cells)
        {
            var node = _CreateNode(cell);
            if (node == null) continue;

            node.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _cellContainer.AddChild(node);

            // gap spacer
            var spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(0, CellGap);
            _cellContainer.AddChild(spacer);
        }
    }

    /// <summary>
    /// Disposes controller instances and removes all cell children.
    /// </summary>
    public void Clear()
    {
        foreach (var child in _cellContainer.GetChildren())
        {
            if (child is GumlViewCellNode gvn)
                gvn.QueueFree(); // _ExitTree() disposes controller
            else
                child.QueueFree();
        }
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private static Control? _CreateNode(INotebookCell cell) => cell switch
    {
        MarkdownCell md =>
            _Make<MarkdownCellNode>(n => n.SetMarkdown(md.Markdown)),

        CodeViewCell code =>
            _Make<CodeViewCellNode>(n => n.SetCode(code.Code, code.LanguageId, code.Title)),

        GumlViewCell gv =>
            _Make<GumlViewCellNode>(n => n.LoadController(gv.ControllerType, gv.MinHeight, gv.Title)),

        _ => null
    };

    private static T _Make<T>(Action<T> init) where T : Control, new()
    {
        var node = new T();
        init(node);
        return node;
    }
}
