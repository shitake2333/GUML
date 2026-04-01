using System.Collections.Generic;
using System.IO;
using Godot;

namespace TestApplication.scripts.notebook;

/// <summary>
/// Static registry of all 12 showcase examples.
/// Each entry is a full Markdown document where fenced <c>```guml</c> blocks render as
/// syntax-highlighted code views and <c>```guml-live ControllerName Height</c> blocks
/// embed live GUML previews.
/// </summary>
public static class ExampleCatalog
{
    private static readonly string s_gumlDir = "res://gui/examples/";

    /// <summary>All example items in display order.</summary>
    public static IReadOnlyList<ShowcaseExampleItem> All { get; } = BuildAll();

    private static IReadOnlyList<ShowcaseExampleItem> BuildAll() =>
    [
        new ShowcaseExampleItem(
            Key: "01",
            Title: "Basic Components",
            Category: "Fundamentals",
            Description: "Labels, buttons and layout containers.",
            Markdown: $$"""
                # Basic Components
                GUML lets you describe a Godot scene tree with a concise, indented syntax.
                Every indented block becomes a child node; properties are set with **`:`**.

                ```guml
                {{_ReadGuml("01_basic_component.guml")}}
                ```

                ```guml-live BasicComponentController 280
                ```
                """),

        new ShowcaseExampleItem(
            Key: "02",
            Title: "Types & Literals",
            Category: "Fundamentals",
            Description: "Strings, integers, floats, booleans, vectors and colors.",
            Markdown: $$"""
                # Types & Literals
                GUML supports all Godot primitive types inline:
                - `"string"` — quoted string
                - `42` / `3.14` — integer / float
                - `true` / `false` — boolean
                - `vec2(x, y)` / `vec3(x, y, z)` — vectors
                - `color(r, g, b)` / `#RRGGBB` — colours

                ```guml
                {{_ReadGuml("02_types_and_literals.guml")}}
                ```

                ```guml-live TypesAndLiteralsController 320
                ```
                """),

        new ShowcaseExampleItem(
            Key: "03",
            Title: "Expressions",
            Category: "Fundamentals",
            Description: "Arithmetic, ternary, string interpolation and member access.",
            Markdown: $$"""
                # Expressions
                Property values can be arbitrary expressions: arithmetic (`+`, `-`, `*`, `/`),
                comparisons and the ternary operator `condition ? a : b`.

                ```guml
                {{_ReadGuml("03_expressions.guml")}}
                ```

                ```guml-live ExpressionsController 340
                ```
                """),

        new ShowcaseExampleItem(
            Key: "04",
            Title: "Data Binding",
            Category: "Binding",
            Description: "One-way `:=`, reverse `=:` and two-way `<=>` bindings to a Controller.",
            Markdown: $$"""
                # Data Binding
                GUML supports three binding operators:

                | Operator | Direction | Meaning |
                |----------|-----------|---------|
                | `:=` | Controller → Node | One-way read |
                | `=:` | Node → Controller | One-way write |
                | `<=>` | Both | Two-way sync |

                Bindings are re-evaluated whenever the controller raises `PropertyChanged`.

                ```guml
                {{_ReadGuml("04_data_binding.guml")}}
                ```

                ```guml-live DataBindingController 340
                ```
                """),

        new ShowcaseExampleItem(
            Key: "05",
            Title: "Events",
            Category: "Binding",
            Description: "Signal subscriptions with `#signal` syntax.",
            Markdown: $$"""
                # Events
                Godot signals are subscribed with the `#signal_name` shorthand.
                The right-hand side is a controller method or expression.

                ```guml
                {{_ReadGuml("05_events.guml")}}
                ```

                ```guml-live EventsController 320
                ```
                """),

        new ShowcaseExampleItem(
            Key: "06",
            Title: "Import Directive",
            Category: "Composition",
            Description: "Reuse GUML files with `import` and pass parameters.",
            Markdown: $$"""
                # Import Directive
                `import` embeds another `.guml` file at the current location.
                Parameters declared in the imported file can be passed by name.

                ```guml
                {{_ReadGuml("06_import_directive.guml")}}
                ```

                ```guml-live ImportDirectiveController 280
                ```
                """),

        new ShowcaseExampleItem(
            Key: "07",
            Title: "Parameters & Events",
            Category: "Composition",
            Description: "Declare component parameters and emit events upward.",
            Markdown: $$"""
                # Parameters & Events
                Components can declare typed parameters (`param`) and
                surface custom events (`event`) for parent consumers to handle.

                ```guml
                {{_ReadGuml("07_parameters_and_events.guml")}}
                ```

                ```guml-live ParametersAndEventsController 360
                ```
                """),

        new ShowcaseExampleItem(
            Key: "08",
            Title: "Each Blocks",
            Category: "Controls",
            Description: "Render lists with `each` and destructured loop variables.",
            Markdown: $$"""
                # Each Blocks
                `each` iterates over a controller collection and stamps a child node
                for every element. The loop variable gives access to the item and its index.

                ```guml
                {{_ReadGuml("08_each_blocks.guml")}}
                ```

                ```guml-live EachBlocksController 380
                ```
                """),

        new ShowcaseExampleItem(
            Key: "09",
            Title: "Aliases & Globals",
            Category: "Controls",
            Description: "Named node aliases with `@` and built-in globals `$root`, `$controller`.",
            Markdown: $$"""
                # Aliases & Globals
                Prefix a node type with `@alias:` to capture it by name for later use.
                GUML provides two built-in globals:
                - `$controller` — the bound controller instance
                - `$root` — the topmost GUML node

                ```guml
                {{_ReadGuml("09_aliases_and_globals.guml")}}
                ```

                ```guml-live AliasesAndGlobalsController 320
                ```
                """),

        new ShowcaseExampleItem(
            Key: "10",
            Title: "Template Strings",
            Category: "Controls",
            Description: "Embedded expressions inside string literals with `${ }`.",
            Markdown: $$"""
                # Template Strings
                Wrap an expression in `${ }` inside a quoted string to produce
                an interpolated value at runtime, similar to C# interpolated strings.

                ```guml
                {{_ReadGuml("10_template_strings.guml")}}
                ```

                ```guml-live TemplateStringsController 340
                ```
                """),

        new ShowcaseExampleItem(
            Key: "11",
            Title: "Each Projection",
            Category: "Advanced",
            Description: "Transform collections with projection templates.",
            Markdown: $$"""
                # Each Projection
                `each` with a `=>` projection maps each item to a different node tree,
                enabling heterogeneous list rendering from a single data source.

                ```guml
                {{_ReadGuml("11_each_projection.guml")}}
                ```

                ```guml-live EachProjectionController 280
                ```
                """),

        new ShowcaseExampleItem(
            Key: "12",
            Title: "Comprehensive Demo",
            Category: "Advanced",
            Description: "A full-featured example combining all GUML features.",
            Markdown: $$"""
                # Comprehensive Demo
                This example combines data binding, events, `each` blocks, aliases
                and template strings into a realistic mini-application.

                ```guml
                {{_ReadGuml("12_comprehensive.guml")}}
                ```

                ```guml-live ComprehensiveController 440
                ```
                """),

        new ShowcaseExampleItem(
            Key: "13",
            Title: "Internationalization",
            Category: "Advanced",
            Description: "Translate UI strings with tr(), ntr() and context disambiguation.",
            Markdown: $$"""
                # Internationalization (i18n)
                GUML provides two built-in translation functions that map to gettext conventions:

                | Function | Usage |
                |----------|-------|
                | `tr(msgid)` | Singular translation |
                | `tr(msgid, { context: "ctx" })` | Context-disambiguated translation |
                | `tr(msgid, { key: val })` | Translation with named argument substitution |
                | `ntr(singular, plural, count)` | Plural-aware translation |

                When `Guml.StringProvider` is not set the source-language string is returned as-is.
                Translations are stored in JSON files under `res://i18n/{locale}.json` (see `MockStringProvider`).

                ```guml
                {{_ReadGuml("13_i18n.guml")}}
                ```

                ```guml-live I18nController 520
                ```
                """),
    ];

    // Reads a .guml source file from the Godot resource path.
    private static string _ReadGuml(string filename)
    {
        string path = ProjectSettings.GlobalizePath(s_gumlDir + filename);
        return File.Exists(path)
            ? File.ReadAllText(path)
            : $"# {filename}\n# (source not found)";
    }
}
