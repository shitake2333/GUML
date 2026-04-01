namespace GUML.Analyzer;

/// <summary>
/// Inlined catalog that maps Godot UI property names to their change notification
/// signal names. Used by <see cref="ProjectAnalyzer"/> to determine whether a
/// property supports reactive binding directions (<c>=:</c> and <c>&lt;=&gt;</c>).
/// <para>
/// Each concrete type lists its own entries without inheritance chain lookup.
/// Property→signal mappings are not available in Godot's XML documentation,
/// so this hardcoded data is the authoritative source.
/// </para>
/// </summary>
internal static class PropertySignalCatalog
{
    /// <summary>
    /// Maps <c>TypeName → { property_name → signal_name }</c>.
    /// If a property appears here, the corresponding signal is emitted when the
    /// property value changes, making the property observable from the UI side.
    /// </summary>
    public static readonly Dictionary<string, Dictionary<string, string>> PropertySignals =
        new(StringComparer.Ordinal)
        {
            // ── BaseButton hierarchy ──────────────────────────────────────────
            ["BaseButton"] = new() { ["button_pressed"] = "toggled" },
            ["Button"] = new() { ["button_pressed"] = "toggled" },
            ["CheckButton"] = new() { ["button_pressed"] = "toggled" },
            ["CheckBox"] = new() { ["button_pressed"] = "toggled" },
            ["MenuButton"] = new() { ["button_pressed"] = "toggled" },
            ["OptionButton"] = new() { ["button_pressed"] = "toggled", ["selected"] = "item_selected" },
            ["LinkButton"] = new() { ["button_pressed"] = "toggled" },

            // ── Range hierarchy ───────────────────────────────────────────────
            ["Range"] = new() { ["value"] = "value_changed" },
            ["HSlider"] = new() { ["value"] = "value_changed" },
            ["VSlider"] = new() { ["value"] = "value_changed" },
            ["SpinBox"] = new() { ["value"] = "value_changed" },
            ["HScrollBar"] = new() { ["value"] = "value_changed" },
            ["VScrollBar"] = new() { ["value"] = "value_changed" },
            ["ProgressBar"] = new() { ["value"] = "value_changed" },

            // ── Text input ────────────────────────────────────────────────────
            ["LineEdit"] = new() { ["text"] = "text_changed" },
            ["TextEdit"] = new() { ["text"] = "text_changed" },
            ["CodeEdit"] = new() { ["text"] = "text_changed" },

            // ── Tab controls ──────────────────────────────────────────────────
            ["TabBar"] = new() { ["current_tab"] = "tab_changed" },
            ["TabContainer"] = new() { ["current_tab"] = "tab_changed" },

            // ── Color controls ────────────────────────────────────────────────
            ["ColorPicker"] = new() { ["color"] = "color_changed" },
            ["ColorPickerButton"] = new() { ["color"] = "color_changed" },
        };

    /// <summary>
    /// Returns the signal name that notifies when <paramref name="propertyName"/>
    /// changes on <paramref name="typeName"/>, or <c>null</c> if no mapping exists.
    /// </summary>
    public static string? GetSignalForProperty(string typeName, string propertyName)
    {
        if (PropertySignals.TryGetValue(typeName, out var props) &&
            props.TryGetValue(propertyName, out string? signal))
        {
            return signal;
        }

        return null;
    }

    /// <summary>
    /// Returns whether the given property on the given type has a known change signal.
    /// </summary>
    public static bool IsObservable(string typeName, string propertyName)
    {
        return PropertySignals.TryGetValue(typeName, out var props) &&
               props.ContainsKey(propertyName);
    }
}
