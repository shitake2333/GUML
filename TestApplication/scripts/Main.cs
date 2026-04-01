using Godot;
using GUML;
using TestApplication.scripts.examples;
using TestApplication.scripts.i18n;
namespace TestApplication.scripts;
public partial class Main : Node
{
    [Export]
    public Node Root;

    private GuiController _controller;
    public override void _Ready()
    {
        Guml.ResourceProvider = new DefaultResourceProvider();
        Guml.StringProvider = new MockStringProvider();
        _controller = Guml.Load<MainController>(Root);
        // Anchor the GUML root control to fill the parent window node.
        if (_controller?.GumlRootNode is { } gumlRoot)
            gumlRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    }

    public override void _Process(double delta)
    {
        _controller?.Update();

    }

    public override void _ExitTree()
    {
        _controller?.Dispose();
    }
}
