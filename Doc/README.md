# GUML Documentation

**GUML** (Godot UI Markup Language) is a declarative markup language for building user interfaces in Godot .NET applications. It provides a concise, QML/XAML-like syntax for declaring UI hierarchies, data bindings, and event handlers — with full Godot node support.

---

## Getting Started

New to GUML? Start here.

| | |
|---|---|
| [Quick Start](quick_start.md) | Install GUML, write your first `.guml` file, run it in Godot. |
| [Guide: Getting Started](guide/getting_started.md) | Step-by-step setup guide with project structure explanation. |

---

## Guides

These guides explain GUML's key concepts with worked examples. Read them in order or jump to the topic you need.

| Guide | Description |
|-------|-------------|
| [The Controller](guide/controller.md) | How `.guml` files pair with C# controller classes. Lifecycle, `GuiController` base class, `[GumlController]` attribute. |
| [Components & Properties](guide/components_and_properties.md) | Declaring components, nesting, assigning properties, all value types, `@alias` naming. |
| [Data Binding](guide/data_binding.md) | One-way (`:=`), two-way (`<=>`), reverse (`=:`), `INotifyPropertyChanged`, expression bindings and template strings. |
| [Events](guide/events.md) | Subscribing to Godot signals and C# events (`#pressed`), declaring custom events (`event`). |
| [List Rendering](guide/list_rendering.md) | `each` blocks, `ObservableCollection<T>`, incremental updates, scope chains, projection form. |
| [Reusable Components](guide/reusable_components.md) | `import` directive, `param` declarations, passing data into components, `$root` reference. |
| [Source Generator](guide/source_generator.md) | Roslyn source generator setup, generated output, named node properties, diagnostics. |
| [Theming & Styling](guide/theming.md) | `theme_override_*` properties, StyleBox types, `DefaultTheme`, styling patterns. |

---

## Reference

Complete technical reference for the GUML language.

| Reference | Description |
|-----------|-------------|
| [Types](reference/types.md) | All value types and their C#/Godot equivalents. Struct constructors, resource loaders. |
| [Expressions](reference/expressions.md) | Operators, precedence, template strings, member access, call expressions. |
| [API Reference](reference/api.md) | `Guml` static class, `GuiController`, `ObservableCollection<T>`, `IResourceProvider`. |
| [Syntax Reference](guml_syntax.md) | Full GUML syntax with grammar and examples for every construct. |

---

## About GUML

| | |
|---|---|
| **Mode** | Source Generator (Roslyn, compile-time) — strongly-typed, zero-reflection,  best performance. |
| **Target** | Godot .NET (GodotSharp 4.x) |
| **Controller pattern** | Every `.guml` file pairs with a C# `GuiController` subclass. |
| **Binding direction** | Primarily controller → UI (`:=`). Reverse and two-way also supported with compatible components. |

### Feature Overview

- **Declarative UI** — describe your interface with a clean, readable syntax.
- **All Godot nodes** — `Label`, `Button`, `VBoxContainer`, `Panel`, `LineEdit`, and every built-in Godot Control type is supported out of the box.
- **Data binding** — automatically update UI when controller properties change.
- **Event wiring** — connect Godot signals and C# events with one line.
- **List rendering** — `each` blocks with incremental updates via `NotifyList<T>`.
- **Reusable components** — compose and reuse GUML files with typed parameters.
- **Theme overrides** — set theme styles directly in GUML, no editor required.
- **Source Generator** — compile-time code generation for maximum performance.
