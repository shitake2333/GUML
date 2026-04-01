using System.Collections.Generic;
using System.IO;
using Godot;

namespace TestApplication.scripts.notebook;

/// <summary>
/// Static registry of all GUML specification sections.
/// Each entry wraps a GUMLSpec Markdown document for display in the notebook viewer.
/// The spec files are resolved relative to the Godot project directory.
/// </summary>
public static class SpecCatalog
{
    private static readonly string s_specDir = Path.GetFullPath(
        Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "GUMLSpec"));

    /// <summary>All spec documents in section order.</summary>
    public static IReadOnlyList<ShowcaseExampleItem> All { get; } = BuildAll();

    private static IReadOnlyList<ShowcaseExampleItem> BuildAll() =>
    [
        new ShowcaseExampleItem(
            Key: "§0",
            Title: "Overview",
            Category: "Spec",
            Description: "GUML specification overview and index.",
            Markdown: _ReadSpec("README.md")),

        new ShowcaseExampleItem(
            Key: "§1",
            Title: "Scope",
            Category: "Spec",
            Description: "Scope and normative intent of the GUML specification.",
            Markdown: _ReadSpec("1_Scope.md")),

        new ShowcaseExampleItem(
            Key: "§2",
            Title: "Conformance",
            Category: "Spec",
            Description: "Conformance requirements for implementations.",
            Markdown: _ReadSpec("2_Conformance.md")),

        new ShowcaseExampleItem(
            Key: "§3",
            Title: "Lexical Structure",
            Category: "Spec",
            Description: "Tokens, whitespace, comments and string literals.",
            Markdown: _ReadSpec("3_Lexical_Structure.md")),

        new ShowcaseExampleItem(
            Key: "§4",
            Title: "Basic Concepts",
            Category: "Spec",
            Description: "Scene tree, root component, properties and binding.",
            Markdown: _ReadSpec("4_Basic_Concepts.md")),

        new ShowcaseExampleItem(
            Key: "§5",
            Title: "Types",
            Category: "Spec",
            Description: "Primitive types, enums, structs and resource types.",
            Markdown: _ReadSpec("5_Types.md")),

        new ShowcaseExampleItem(
            Key: "§6",
            Title: "Expressions",
            Category: "Spec",
            Description: "Arithmetic, ternary, member access and call expressions.",
            Markdown: _ReadSpec("6_Expressions.md")),

        new ShowcaseExampleItem(
            Key: "§7",
            Title: "Directives",
            Category: "Spec",
            Description: "import, alias and other top-level directives.",
            Markdown: _ReadSpec("7_Directives.md")),

        new ShowcaseExampleItem(
            Key: "§8",
            Title: "Components",
            Category: "Spec",
            Description: "Component declaration, parameters, events and each blocks.",
            Markdown: _ReadSpec("8_Components.md")),

        new ShowcaseExampleItem(
            Key: "§9",
            Title: "Controller Integration",
            Category: "Spec",
            Description: "Binding syntax, controller lifecycle and data flow.",
            Markdown: _ReadSpec("9_Controller_Integration.md")),

        new ShowcaseExampleItem(
            Key: "§10",
            Title: "Diagnostics",
            Category: "Spec",
            Description: "Error codes and diagnostic messages.",
            Markdown: _ReadSpec("10_Diagnostics.md")),

        new ShowcaseExampleItem(
            Key: "§11.1",
            Title: "API Interface",
            Category: "Spec",
            Description: "GUML analyzer API interface specification.",
            Markdown: _ReadSpec("11_1_Guml_Api_Interface_Specification.md")),

        new ShowcaseExampleItem(
            Key: "§11.2",
            Title: "Code Generation",
            Category: "Spec",
            Description: "Source generator output format specification.",
            Markdown: _ReadSpec("11_2_Code_Generation_Specification.md")),

        new ShowcaseExampleItem(
            Key: "§11.3",
            Title: "Extensions",
            Category: "Spec",
            Description: "Extension points and IDE integration specification.",
            Markdown: _ReadSpec("11_Extensions.md")),
    ];

    private static string _ReadSpec(string filename)
    {
        string path = Path.Combine(s_specDir, filename);
        return File.Exists(path)
            ? File.ReadAllText(path)
            : $"# {filename}\n\n*(source not found — expected at `{path}`)*";
    }
}
