# GUML Language Specification

**Version 0.1 — Draft**

This specification describes the GUML (Godot UI Markup Language) language.

GUML is a declarative markup language for building user interfaces in Godot .NET applications. It provides a concise syntax for declaring UI component hierarchies, setting properties, establishing data bindings, and connecting events to C# controllers.

## Table of Contents

- §1 [Scope](./1_Scope.md)
- §2 [Conformance](./2_Conformance.md)
- §3 [Lexical Structure](./3_Lexical_Structure.md)
  - §3.1 Documents
  - §3.2 Grammars
  - §3.3 Lexical Analysis
  - §3.4 Comments
  - §3.5 White Space
  - §3.6 Tokens
    - §3.6.1 Identifiers
    - §3.6.2 Keywords
    - §3.6.3 Literals
    - §3.6.4 Operators and Punctuators
- §4 [Basic Concepts](./4_Basic_Concepts.md)
  - §4.1 Document Structure
  - §4.2 Execution Model
  - §4.3 Scopes
  - §4.4 Component Lifecycle
- §5 [Types](./5_Types.md)
  - §5.1 Value Types
    - §5.1.1 Primitive Types
    - §5.1.2 Enum Types
    - §5.1.3 Object Literal Type
    - §5.1.4 Array Types
    - §5.1.5 Typed Dictionary Type
  - §5.2 Struct Mapping Types
  - §5.3 Resource Types
  - §5.4 The Null Type
  - §5.5 Type Conversion Rules
- §6 [Expressions](./6_Expressions.md)
  - §6.1 Expression Classifications
  - §6.2 Operators
    - §6.2.1 Prefix (Unary) Operators
    - §6.2.2 Infix (Binary) Operators
    - §6.2.3 Operator Precedence and Parenthesization
    - §6.2.4 Conditional (Ternary) Expression
    - §6.2.5 Assignment Operators
  - §6.3 Primary Expressions
    - §6.3.1 Value Separators
  - §6.4 Object Creation Expressions
  - §6.5 Member Access
  - §6.6 Call Expressions
- §7 [Directives](./7_Directives.md)
  - §7.1 Import Directive
- §8 [Components](./8_Components.md)
  - §8.1 Component Declarations
    - §8.1.1 Root Component Declaration
    - §8.1.2 Child Component Declaration
  - §8.2 Property Assignments
  - §8.3 Property Mapping and Binding
    - §8.3.1 Mapping Modes
    - §8.3.2 Capability Constraints
  - §8.4 Event Subscriptions
  - §8.5 Child Components
  - §8.6 Parameter Declarations
    - §8.6.1 Required vs Optional Parameters
  - §8.7 Event Declarations
  - §8.8 Each Blocks
    - §8.8.1 Nesting Rules
    - §8.8.2 Incremental Updates
- §9 [Controller Integration](./9_Controller_Integration.md)
  - §9.1 GumlController Attribute
  - §9.2 Aliases
  - §9.3 Global References
    - §9.3.1 Predefined Global References
    - §9.3.2 Custom Global References
- §10 [Diagnostics](./10_Diagnostics.md)
  - §10.1 Severity Levels
  - §10.2 API Diagnostics (GUML1xxx)
  - §10.3 Code Generation Diagnostics (GUML2xxx)
  - §10.4 Syntactic Diagnostics (GUML3xxx)
  - §10.5 Conformance Rules for Diagnostics
- §11 [Extensions](./11_Extensions.md)
  - §11.1 [GUML API Interface Specification](./11_1_Guml_Api_Interface_Specification.md)
  - §11.2 [Code Generation Specification](./11_2_Code_Generation_Specification.md)
