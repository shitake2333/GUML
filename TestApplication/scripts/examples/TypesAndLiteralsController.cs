using Godot;
using GUML;
using GUML.Shared;

namespace TestApplication.scripts.examples;

/// <summary>
/// 02 - Types &amp; Literals.
/// Demonstrates type literals alongside a live RGB color mixer.
/// </summary>
[GumlController("res://gui/examples/02_types_and_literals.guml")]
public partial class TypesAndLiteralsController : GuiController
{
    // GUML: $controller.r_val / g_val / b_val
    public float RVal { get; private set; } = 0.9f;
    public float GVal { get; private set; } = 0.3f;
    public float BVal { get; private set; } = 0.3f;

    // GUML: $controller.mixed_color
    public Color MixedColor { get; private set; } = new(0.9f, 0.3f, 0.3f);

    // GUML: #value_changed: $controller.on_r_changed
    public void OnRChanged(double value)
    {
        RVal = (float)value;
        _RefreshColor();
    }

    // GUML: #value_changed: $controller.on_g_changed
    public void OnGChanged(double value)
    {
        GVal = (float)value;
        _RefreshColor();
    }

    // GUML: #value_changed: $controller.on_b_changed
    public void OnBChanged(double value)
    {
        BVal = (float)value;
        _RefreshColor();
    }

    private void _RefreshColor()
    {
        MixedColor = new Color(RVal, GVal, BVal);
        OnPropertyChanged(nameof(MixedColor));
        OnPropertyChanged(nameof(RVal));
        OnPropertyChanged(nameof(GVal));
        OnPropertyChanged(nameof(BVal));
    }
}
