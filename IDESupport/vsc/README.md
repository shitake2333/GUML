# GUML (Godot UI Markup Language) for VS Code

Provide syntax highlighting support for **GUML** (Godot UI Markup Language) files in Visual Studio Code.

GUML is a declarative UI markup language for Godot Engine (C#/.NET), designed to provide a development experience similar to QML or XAML.

## Features

- **Syntax Highlighting**: Full colorization for .guml files, including:
  - Components (e.g., Panel, Button, Label)
  - Properties and Assignments (e.g., text: "Hello", size: vec2(100, 100))
  - Keywords (import, each, true, false, null)
  - Strings and Escape Sequences
  - Comments (// Line comment, /* Block comment */)
  - Built-in Types and Support Functions (vec2, color, resource)
  - Variables and Bindings (, @alias)

## Usage

Simply install the extension and open any file with the .guml extension.

### Example Code

\\\guml
import "setting"

Panel {
    // This is a comment
    Label {
        position: vec2(10, 10),
        size: vec2(200, 30),
        text:= "hello " + .SayHello
    }
    
    each .Names { |index, name|
        Control {
            custom_minimum_size: vec2(220, 100),
            Label {
                text: name
            }
        }
    }         
}
\\\

## Requirements

No special requirements. This extension provides syntax highlighting out of the box.

## Extension Settings

This extension currently does not contribute any settings.

## Known Issues

- This is an early preview version. Semantic analysis and IntelliSense are not yet implemented.

## Release Notes

### 0.0.1

- Initial release with basic syntax highlighting support.
