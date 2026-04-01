using System;
using GUML;
using GUML.Shared;
using TestApplication.scripts.i18n;

namespace TestApplication.scripts.examples;

/// <summary>
/// 13 - Internationalization (i18n).
/// Demonstrates <c>tr()</c>, <c>ntr()</c>, context disambiguation and
/// locale switching at runtime using a JSON-backed <see cref="MockStringProvider"/>.
/// </summary>
[GumlController("res://gui/examples/13_i18n.guml")]
public partial class I18nController : GuiController
{
    // GUML: =: reverse binding from LineEdit
    public string UserName { get; set; } = "Alice";

    // GUML: $controller.item_count  (used by ntr)
    public int ItemCount { get; private set; } = 1;

    // GUML: $controller.current_locale  (for locale label + button highlight)
    public string CurrentLocale => Guml.StringProvider?.CurrentLocale ?? "en";

    // -----------------------------------------------------------------------
    // Locale switching
    // -----------------------------------------------------------------------

    // GUML: #pressed: $controller.switch_to_en
    public void SwitchToEn() => SetLocale("en");

    // GUML: #pressed: $controller.switch_to_zh
    public void SwitchToZh() => SetLocale("zh_CN");

    // GUML: #pressed: $controller.switch_to_ja
    public void SwitchToJa() => SetLocale("ja");

    private void SetLocale(string locale)
    {
        if (Guml.StringProvider is MockStringProvider sp)
            sp.CurrentLocale = locale;
        // Raise CurrentLocale so the locale label and button highlights refresh.
        // tr/ntr bindings are refreshed automatically via the "_locale" relay in GuiController.
        OnPropertyChanged(nameof(CurrentLocale));
    }

    // -----------------------------------------------------------------------
    // Item count
    // -----------------------------------------------------------------------

    // GUML: #pressed: $controller.inc_count
    public void IncCount()
    {
        ItemCount++;
        OnPropertyChanged(nameof(ItemCount));
    }

    // GUML: #pressed: $controller.dec_count
    public void DecCount()
    {
        ItemCount = Math.Max(0, ItemCount - 1);
        OnPropertyChanged(nameof(ItemCount));
    }
}
