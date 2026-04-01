#nullable enable
using GUML;
using GUML.Shared;
using Godot;
using TestApplication.scripts.notebook;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace TestApplication.scripts.examples;

/// <summary>
/// Root controller for the GUML Showcase application.
/// Manages the left-side navigation list (examples and spec sections) and loads
/// the selected content into the right-side notebook panel via <see cref="NotebookViewer"/>.
/// </summary>
[GumlController("res://gui/main.guml")]
public partial class MainController : GuiController
{
    // -----------------------------------------------------------------------
    // Bound properties
    // -----------------------------------------------------------------------

    private string _activeSection = "examples";

    /// <summary>Currently active navigation section: <c>"examples"</c> or <c>"spec"</c>.</summary>
    public string ActiveSection
    {
        get => _activeSection;
        private set { _activeSection = value; OnPropertyChanged(); }
    }

    /// <summary>Items currently shown in the navigation list.</summary>
    public ObservableCollection<ShowcaseExampleItem> NavItems
    {
        get;
        private set
        { field = value; OnPropertyChanged(); }
    } = [.. ExampleCatalog.All];

    /// <summary>Key of the currently selected navigation item.</summary>
    public string SelectedKey
    {
        get;
        private set { field = value; OnPropertyChanged(); }
    } = "";

    // -----------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------

    /// <summary>Selects the first example on startup.</summary>
    public override void Created()
    {
        // Warm up TextMate so first highlight is instant
        TextMateCodeHighlighter.Instance.Warmup();

        Viewer.OnLinkClicked = _HandleLinkClicked;

        var split = (SplitContainer)GumlRootNode;
        split.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        split.SplitOffsets = [260];
        if (NavItems.Count > 0)
            SelectItem(NavItems[0].Key);
    }

    // -----------------------------------------------------------------------
    // Section switching (callable from GUML)
    // -----------------------------------------------------------------------

    /// <summary>Switches the navigation list to the Examples section.</summary>
    public void ShowExamples()
    {
        if (_activeSection == "examples") return;
        ActiveSection = "examples";
        NavItems = [.. ExampleCatalog.All];
        _LoadFirst();
    }

    /// <summary>Switches the navigation list to the Spec section.</summary>
    public void ShowSpec()
    {
        if (_activeSection == "spec") return;
        ActiveSection = "spec";
        NavItems = [.. SpecCatalog.All];
        _LoadFirst();
    }

    // Called internally when navigating via a Markdown link inside a spec doc.
    private void _ShowSpecAndSelect(string key)
    {
        if (_activeSection != "spec")
        {
            ActiveSection = "spec";
            NavItems = [.. SpecCatalog.All];
        }
        SelectItem(key);
    }

    // -----------------------------------------------------------------------
    // Navigation (callable from GUML)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Loads the content for the navigation item identified by <paramref name="key"/>.
    /// Called by button signals in main.guml.
    /// </summary>
    public void SelectItem(string key)
    {
        if (SelectedKey == key) return;
        SelectedKey = key;

        Viewer.Clear();

        var item = NavItems.FirstOrDefault(e => e.Key == key);
        if (item == null) return;

        Viewer.Load(item);
    }

    // -----------------------------------------------------------------------
    // Link navigation
    // -----------------------------------------------------------------------

    private void _HandleLinkClicked(string url)
    {
        if (string.IsNullOrEmpty(url)) return;

        // External URL → open in browser.
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            OS.ShellOpen(url);
            return;
        }

        // Relative spec file link (e.g. "./3_Lexical_Structure.md" or "3_Lexical_Structure.md").
        string fileName = System.IO.Path.GetFileName(url);
        if (fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            if (fileName.Equals("README.md", StringComparison.OrdinalIgnoreCase))
            {
                _ShowSpecAndSelect("\u00a70");
                return;
            }
            Match m = Regex.Match(fileName, @"^(\d+)_");
            if (m.Success && int.TryParse(m.Groups[1].Value, out int num))
            {
                _ShowSpecAndSelect($"\u00a7{num}");
            }
        }
        // Anchor links (#…) are same-page fragments — ignore.
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private void _LoadFirst()
    {
        SelectedKey = "";
        Viewer.Clear();
        if (NavItems.Count > 0) SelectItem(NavItems[0].Key);
    }
}
