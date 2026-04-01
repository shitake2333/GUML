using GUML;
using GUML.Shared;

namespace TestApplication.scripts.examples;

/// <summary>
/// 03 - Expressions.
/// Provides interactive controls so expression results can be observed updating live.
/// </summary>
[GumlController("res://gui/examples/03_expressions.guml")]
public partial class ExpressionsController : GuiController
{
    // GUML: $controller.score  (stepper buttons)
    public int Score { get; private set; } = 85;

    // GUML: $controller.health  (slider)
    public float Health { get; private set; } = 75f;

    // GUML: $controller.level  (stepper buttons)
    public int Level { get; private set; } = 8;

    // GUML: $controller.is_vip  (toggle button)
    public bool IsVip { get; private set; }

    // GUML: $controller.data.name  (member chain access demo)
    public PlayerData Data { get; set; } = new();

    // GUML: $controller.first_name / last_name  (method call demo)
    public string FirstName { get; set; } = "John";
    public string LastName { get; set; } = "Doe";

    // GUML: $controller.format_name(first, last)
    public string FormatName(string first, string last) => $"{first} {last}";

    // GUML: #pressed: $controller.inc_score
    public void IncScore()
    {
        Score = System.Math.Min(100, Score + 10);
        OnPropertyChanged(nameof(Score));
    }

    // GUML: #pressed: $controller.dec_score
    public void DecScore()
    {
        Score = System.Math.Max(0, Score - 10);
        OnPropertyChanged(nameof(Score));
    }

    // GUML: #value_changed: $controller.on_health_changed
    public void OnHealthChanged(double value)
    {
        Health = (float)value;
        OnPropertyChanged(nameof(Health));
    }

    // GUML: #pressed: $controller.inc_level
    public void IncLevel()
    {
        Level = System.Math.Min(20, Level + 1);
        OnPropertyChanged(nameof(Level));
    }

    // GUML: #pressed: $controller.dec_level
    public void DecLevel()
    {
        Level = System.Math.Max(1, Level - 1);
        OnPropertyChanged(nameof(Level));
    }

    // GUML: #pressed: $controller.toggle_vip
    public void ToggleVip()
    {
        IsVip = !IsVip;
        OnPropertyChanged(nameof(IsVip));
    }
}

public class PlayerData
{
    public string Name { get; set; } = "Player1";
}
