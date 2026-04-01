using GUML;
using GUML.Shared;
using System;

namespace TestApplication.scripts.examples;

/// <summary>
/// 10 - Template Strings.
/// Interactive controls let you see template-string bindings update live.
/// </summary>
[GumlController("res://gui/examples/10_template_strings.guml")]
public partial class TemplateStringsController : GuiController
{
    // GUML: $controller.user_name  (=: reverse binding from LineEdit)
    public string UserName { get; set; } = "Alice";

    // GUML: $controller.score / max_score
    public int Score { get; private set; } = 85;
    public int MaxScore { get; } = 100;

    // GUML: $controller.price / quantity
    public float Price { get; } = 9.99f;
    public int Quantity { get; } = 3;

    // GUML: $controller.is_online
    public bool IsOnline { get; } = true;

    // GUML: $controller.created_at
    public DateTime CreatedAt { get; } = new DateTime(2025, 3, 29);

    // GUML: $controller.format_date(date)
    public string FormatDate(DateTime date) => date.ToString("yyyy-MM-dd");

    // GUML: $controller.first_name / last_name / user_id
    public string FirstName { get; } = "John";
    public string LastName { get; } = "Doe";
    public int UserId { get; } = 42;

    // GUML: $controller.player_name / health / max_health / level
    public string PlayerName { get; } = "Hero";
    public int Health { get; private set; } = 80;
    public int MaxHealth { get; } = 100;
    public int Level { get; private set; } = 12;

    // GUML: #pressed: $controller.inc_score
    public void IncScore()
    {
        Score = Math.Min(MaxScore, Score + 5);
        OnPropertyChanged(nameof(Score));
    }

    // GUML: #pressed: $controller.dec_score
    public void DecScore()
    {
        Score = Math.Max(0, Score - 5);
        OnPropertyChanged(nameof(Score));
    }
}
