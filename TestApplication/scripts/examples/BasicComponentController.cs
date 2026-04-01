using Godot;
using GUML;
using GUML.Shared;

namespace TestApplication.scripts.examples;

/// <summary>
/// 01 - Basic Component Declaration.
/// Demonstrates static layout alongside a live click counter and color cycling.
/// </summary>
[GumlController("res://gui/examples/01_basic_component.guml")]
public partial class BasicComponentController : GuiController
{
    private static readonly Color[] s_paletteColors =
    [
        new(0.9f, 0.3f, 0.3f), new(0.3f, 0.9f, 0.3f),
        new(0.3f, 0.5f, 1.0f), new(1f, 0.8f, 0.2f),
        new(0.9f, 0.3f, 0.9f), new(0.2f, 0.9f, 0.9f),
    ];

    private static readonly string[] s_paletteNames = ["Red", "Green", "Blue", "Yellow", "Magenta", "Cyan"];
    private int _colorIndex;

    // GUML: $controller.click_count
    public int ClickCount { get; private set; }

    // GUML: $controller.current_color
    public Color CurrentColor { get; private set; } = s_paletteColors[0];

    // GUML: $controller.color_name
    public string ColorName { get; private set; } = s_paletteNames[0];

    // GUML: #pressed: $controller.on_click
    public void OnClick()
    {
        ClickCount++;
        OnPropertyChanged(nameof(ClickCount));
    }

    // GUML: #pressed: $controller.reset_count
    public void ResetCount()
    {
        ClickCount = 0;
        OnPropertyChanged(nameof(ClickCount));
    }

    // GUML: #pressed: $controller.cycle_color
    public void CycleColor()
    {
        _colorIndex = (_colorIndex + 1) % s_paletteColors.Length;
        CurrentColor = s_paletteColors[_colorIndex];
        ColorName = s_paletteNames[_colorIndex];
        OnPropertyChanged(nameof(CurrentColor));
        OnPropertyChanged(nameof(ColorName));
    }
}
