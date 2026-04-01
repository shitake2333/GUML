# §10 Diagnostics

This chapter centralizes all named diagnostics defined by this specification.

## §10.1 Severity Levels

Diagnostic severities are classified as follows:

- **Error**: the document is ill-formed for conformance purposes. Implementations MUST fail generation for the current document.
- **Warn**: the document remains processable, but behavior may be unstable, degraded, or implementation-defined. Implementations SHOULD continue generation.

If a named diagnostic is emitted without an explicit severity override, the severity in this chapter SHALL be used.

## §10.2 API Diagnostics (GUML1xxx)

| Code | Severity | Description | Primary Chapter |
|------|----------|-------------|-----------------|
| `GUML1001` | Error | Type not found in API metadata. | §11.1 |
| `GUML1002` | Error | Property not found in API metadata. | §11.1 |
| `GUML1003` | Error | Mapping direction unsupported by endpoint capability. | §11.1 |
| `GUML1004` | Error | Two-way mapping requires property observability but is not available. | §11.1 |
| `GUML1005` | Error | Global reference is not registered for Source Generator analysis. | §11.1 |
| `GUML1006` | Error | Method or callable member not found on controller type. | §6.6 |
| `GUML1007` | Error | Method argument count does not match parameter count. | §6.6 |
| `GUML1008` | Warn | Argument type may not be compatible with the expected parameter type. | §6.6, §5.5 |
| `GUML1010` | Error | Static assignment type is incompatible with expected property type. | §8.2, §5.5 |
| `GUML1011` | Warn | Static assignment type may not be compatible with expected property type. | §8.2, §5.5 |
| `GUML1012` | Error | Enum member not found for the expected enum type. | §5.3 |
| `GUML1013` | Error | Dictionary literal assigned to non-dictionary property. | §5.6 |
| `GUML1014` | Error | Ternary expression branches have incompatible types for the target property. | §6.3, §5.5 |
| `GUML1015` | Warn | Ternary expression branch type may not be compatible with expected type. | §6.3, §5.5 |
| `GUML1016` | Error | Expected comma separator between adjacent value-assignment members. | §8.1.3 |

## §10.3 Code Generation Diagnostics (GUML2xxx)

| Code | Severity | Description | Primary Chapter |
|------|----------|-------------|-----------------|
| `GUML2001` | Error | Alias normalization collision cannot be resolved safely. | §11.2 |
| `GUML2002` | Error | Invalid generated identifier. | §11.2 |
| `GUML2003` | Warn | Naming stability violation risk across incremental edits. | §11.2 |
| `GUML2004` | Warn | Root node name cannot be stably derived from file name; normalization fallback used. | §11.2 |
| `GUML2005` | Error | Scene-tree node naming collision cannot be resolved. | §11.2 |
| `GUML2006` | Warn | Documentation-comment naming marker is invalid or empty; fallback naming used. | §11.2 |
| `GUML2007` | Error | Generated node name exceeds max length and cannot be resolved uniquely. | §11.2 |
| `GUML2008` | Error | Required parameter is not provided for imported component usage. | §8.6, §11.2 |
| `GUML2009` | Error | `+` operator is applied to non-numeric operands. | §6.2.2, §11.2 |
| `GUML2010` | Error | Invalid template projection assignment (`=>`) target or shape. | §8.8, §11.2 |
| `GUML2011` | Error | Projection context `$item` is used outside projection template scope. | §8.8, §11.2 |

## §10.4 Syntactic Diagnostics (GUML3xxx)

| Code | Severity | Description | Primary Chapter |
|------|----------|-------------|-----------------|
| `GUML3001` | Warn | Member access name is not `snake_case`. | §3.6.1.1 |
| `GUML3002` | Warn | Identifier (property, event, parameter, alias, event reference, template parameter, or projection name) is not `snake_case`. | §3.6.1.1 |

## §10.5 Conformance Rules for Diagnostics

- Implementations MAY emit additional implementation-specific diagnostics, but they SHOULD avoid reusing the `GUML` code space (specifically `GUML1xxx`, `GUML2xxx`, and `GUML3xxx` series) for unrelated meanings.
- If the same root cause is covered by multiple named diagnostics, implementations SHOULD emit the most specific code and MAY emit related secondary diagnostics.
- LSP, CLI, and build integrations SHOULD preserve both code and severity when presenting diagnostics.
