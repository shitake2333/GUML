# Godot UI Markup Language

[![NuGet](https://img.shields.io/nuget/v/GUML)](https://www.nuget.org/packages/GUML) [![GitHub Release](https://img.shields.io/github/v/release/shitake2333/GUML)](https://github.com/shitake2333/GUML/releases/latest) [![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/shitake2333/GUML/blob/master/LICENSE)

GUML is a declarative UI markup language (`.guml`) for **Godot .NET**, providing a QML/XAML-like development experience. It allows developers to define UI components and layouts using a concise syntax, with support for data binding, event handling, and reusable components. GUML offers two modes: **Source Generator** (compile-time, Roslyn-based) and **Interpreter** (runtime), providing flexible development options.

## Features

- QML-like declarative syntax
- Data binding — automatically update UI when controller properties change (one-way `:=`, two-way `<=>`, reverse `=:`)
- Event wiring — connect Godot signals and C# events with one line
- List rendering — `each` blocks with incremental updates via `NotifyList<T>`
- Reusable components — compose and reuse `.guml` files with typed parameters
- Full Godot UI component support (all built-in Control types)
- Theme overrides — set theme styles directly in GUML
- Source Generator mode — compile-time code generation for maximum performance

## Install

```sh
dotnet add package GUML --version 0.3.0
```

For source generator support:
```sh
dotnet add package GUML.SourceGenerator --version 0.3.0
```

## Documentation

- [Documentation Hub](Doc/README.md) — guides, reference, and API docs
- [Quick Start](Doc/quick_start.md) — install GUML, write your first `.guml` file, run it in Godot
- [GUML Syntax](Doc/guml_syntax.md) — full syntax reference

## Language Specification

The formal GUML language specification is available in the [GUMLSpec](GUMLSpec/README.md) directory. It covers:

- Lexical structure & grammar ([BNF](GUMLSpec/guml.bnf))
- Types, expressions, and directives
- Component model and controller integration
- Code generation and API interface specifications
- Diagnostics

## IDE Support

Editor plugins have been moved to their own repositories:

| Editor | Repository |
|--------|-----------|
| VS Code | [GUML.VSC](https://github.com/shitake2333/GUML.VSC) |
| JetBrains Rider | [GUML.Rider](https://github.com/shitake2333/GUML.Rider) |

## Project Structure

| Directory | Description |
|-----------|-------------|
| `GUML` | Core runtime library — `GuiController`, interpreter, converters, bindings |
| `GUML.Shared` | Shared infrastructure — full-fidelity CST parser, API metadata models, diagnostics |
| `GUML.SourceGenerator` | Roslyn source generator for compile-time `.guml` → C# code generation |
| `GUML.Analyzer` | Language analyzer CLI tool — Roslyn-based project analysis and LSP features via JSON-RPC |
| `GUMLSpec` | Formal language specification |
| `Doc` | User documentation (guides, reference, quick start) |
| `TestApplication` | Sample Godot project demonstrating GUML usage |

## Example

`main.guml`:
```guml
Panel {
    size: vec2(640, 480),
    theme_overrides: { 
        panel: style_box_flat({
            bg_color: color(0.4, 0.4, 0.4, 0.4)
        })
    },
    Label {
        position: vec2(10, 10),
        size: vec2(200, 30),
        // $controller.SayHello binding to Label.text.If SayHello changes, text will also change.
        text:= "hello " + $controller.SayHello
    }
    
    Button {
        position: vec2(10, 50),
        size: vec2(200, 30),
        text: "Change world",
        #pressed: $controller.ChangeHelloBtnPressed
    }
}
```
`MainController.cs`:
```c#
public class MainController : GuiController
{
    public string SayHello {
	    get => _sayHello;
		set
		{
			_sayHello = value;
			OnPropertyChanged();
		}
    }
	
    private string _sayHello = "world!";

	public void ChangeHelloBtnPressed()
	{
		SayHello = "new world!";
	}
}
```
![example](./Doc/res/example.png)

After clicking the *change world* button, the text will change to `hello new world!`

## License

[MIT](LICENSE)
