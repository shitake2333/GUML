# §1 Scope

This specification describes the form and establishes the interpretation of programs written in the GUML (Godot UI Markup Language) language.

GUML is a declarative UI markup language designed for Godot .NET applications. It provides a syntax for:

- Declaring hierarchical UI component trees.
- Assigning property values to components.
- Establishing data bindings from C# controller objects to UI properties.
- Subscribing to events (Godot signals and C# delegates, unified as the "event" concept).
- Defining reusable component parameters and custom events.
- Expression evaluation including conditionals, function calls, and template strings.
- Content projection via template parameter assignment and projected `each`.
- Typed arrays and typed dictionaries for structured data.

GUML programs are processed at compile-time by a C# Source Generator, producing generated C# code that constructs Godot scene trees bound to C# controller classes.

This specification does not define:

- The mechanism by which GUML programs are transformed into Godot scene trees (this is implementation-defined).
- The set of available Godot component types (this depends on the Godot SDK version).
- The C# APIs used by generated code.

