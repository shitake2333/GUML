using GUML;
using GUML.Shared;

namespace TestApplication.scripts.examples;

/// <summary>
/// 04 - Data Binding.
/// Demonstrates static (:), one-way (:=) and reverse (=:) bindings with live interactive controls.
/// </summary>
[GumlController("res://gui/examples/04_data_binding.guml")]
public partial class DataBindingController : GuiController
{
    // GUML: $controller.user_name  (=: reverse binding from LineEdit)
    public string UserName { get; set; } = "Alice";

    // GUML: $controller.score
    public int Score { get; private set; } = 85;

    // GUML: $controller.max_score
    public int MaxScore { get; } = 100;

    // GUML: $controller.health  (slider + := binding)
    public float Health { get; private set; } = 75f;

    // GUML: $controller.first_name / last_name
    public string FirstName { get; set; } = "John";
    public string LastName { get; set; } = "Doe";

    // GUML: $controller.format_display(first, last)
    public string FormatDisplay(string first, string last) => $"{last}, {first}";

    // GUML: #pressed: $controller.inc_score
    public void IncScore()
    {
        Score = System.Math.Min(MaxScore, Score + 5);
        OnPropertyChanged(nameof(Score));
    }

    // GUML: #pressed: $controller.dec_score
    public void DecScore()
    {
        Score = System.Math.Max(0, Score - 5);
        OnPropertyChanged(nameof(Score));
    }

    // GUML: #value_changed: $controller.on_health_changed
    public void OnHealthChanged(double value)
    {
        Health = (float)value;
        OnPropertyChanged(nameof(Health));
    }
}
