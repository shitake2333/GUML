# §2 Conformance

A conforming implementation of GUML shall:

- Accept and correctly process all GUML documents that conform to the syntax and constraints specified herein.
- Reject GUML documents that violate the syntactic rules specified in §3.
- Produce diagnostics for all errors as defined throughout this specification, using the centralized diagnostic registry in §10.
- Produce diagnostics at compile-time for unresolved or unregistered global references (§9.3).

Diagnostic severities (`Error` / `Warn`) SHALL follow §10.1 and per-code definitions in §10.2 and §10.3.

A conforming GUML document shall:

- Contain exactly one root component declaration (§8.1.1).
- Place `parameter_declaration` and `event_declaration` only in the root component body (§8.1.1), not in child component bodies (§8.1.2).
- Use only the lexical elements defined in §3.
- Conform to all constraints specified in §7 and §8.

This specification defines conformance for a Source Generator-based implementation:

- **Source Generator mode**: The GUML document is processed at compile-time. A conforming Source Generator implementation shall produce C# source code that, when compiled, constructs the UI hierarchy and binds it to the associated controller (§9.1).


