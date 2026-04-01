# §11.1 GUML API Interface Specification

This extension specification defines the C#-side API description contract required by GUML tooling.

The contract is used by:

- Source Generator code generation.
- Compile-time diagnostics and semantic validation.
- IDE/LSP semantic completion and hover information.

This specification is intentionally language-adjacent and does not define GUML syntax itself.

This specification defines the contract first, and implementation models second. Existing code models are informative references, not the normative source of truth.

## §11.1.0 Normative Terms

The key words `MUST`, `MUST NOT`, `SHOULD`, `SHOULD NOT`, and `MAY` in this document are to be interpreted as requirement levels.

- `MUST` / `MUST NOT`: mandatory for conformance.
- `SHOULD` / `SHOULD NOT`: strongly recommended unless there is a justified reason.
- `MAY`: optional behavior.

## §11.1.1 Scope

A conforming API description shall provide structured metadata for:

- Component types (class hierarchy and docs).
- Component properties (type, docs, enum metadata).
- Component events (name, docs, parameters).
- Mapping constraints required by property mapping operators (`:`, `:=`, `=:`, `<=>`).
- Controller-to-GUML binding metadata required by Source Generator workflows.

Out of scope:

- Runtime interpreter protocols.
- Transport protocols (file, RPC, pipe, HTTP).
- UI presentation concerns.

## §11.1.2 Canonical Object Model (C#)

The API contract shall be representable in both forms:

- C# object graph (for in-process tooling).
- JSON document (for cross-process tooling).

An implementation may reuse existing classes, but the following logical entities are normative.

Minimum required entities:

- `ApiDocument`
- `TypeDescriptor`
- `PropertyDescriptor`
- `EnumValueDescriptor`
- `EventDescriptor`
- `ParameterDescriptor`
- `ControllerDescriptor`
- `MappingConstraintDescriptor`

### Required fields

`ApiDocument`:

- `SchemaVersion: string`
- `GeneratedAt: DateTime`
- `SdkVersion: string?`
- `Types: Dictionary<string, TypeDescriptor>`
- `Controllers: Dictionary<string, ControllerDescriptor>`

`TypeDescriptor`:

- `Name: string`
- `QualifiedName: string`
- `Kind: enum { Class, Struct }`
- `BaseType: string?`
- `Description: string`
- `Properties: Dictionary<string, PropertyDescriptor>`
- `Events: Dictionary<string, EventDescriptor>`

`PropertyDescriptor`:

- `Name: string`
- `Type: string` (canonical CLR type name)
- `Description: string`
- `IsReadable: bool`
- `IsWritable: bool`
- `EnumValues: List<EnumValueDescriptor>?`
- `Mapping: MappingConstraintDescriptor`

`EnumValueDescriptor`:

- `Name: string`
- `Value: string`
- `Description: string?`

`EventDescriptor`:

- `Name: string`
- `Description: string?`
- `Parameters: List<ParameterDescriptor>`

`ParameterDescriptor`:

- `Name: string`
- `Type: string`

`ControllerDescriptor`:

- `FullName: string`
- `SimpleName: string`
- `GumlPath: string`
- `Properties: List<ParameterDescriptor>`
- `Methods: List<string>`

`MappingConstraintDescriptor`:

- `CanStaticMap: bool`
- `CanBindDataToProperty: bool`
- `CanBindPropertyToData: bool`
- `CanBindTwoWay: bool`
- `IsObservableProperty: bool`
- `ObservabilitySource: enum { None, InotifyPropertyChanged, Signal, Custom }`

## §11.1.3 Mapping Constraint Metadata

To support mapping operators in §8.3, API descriptions shall include directional capability metadata for properties.

At minimum, each property shall expose the following derived flags (direct field or equivalent computed capability):

- `CanStaticMap` (supports `:`)
- `CanBindDataToProperty` (supports `:=`)
- `CanBindPropertyToData` (supports `=:`)
- `CanBindTwoWay` (supports `<=>`)
- `IsObservableProperty` (whether property-side change notifications are observable)

### Semantics

- `CanStaticMap` MUST be `true` for settable properties.
- `CanBindDataToProperty` MUST be `true` when the target property can receive updates.
- `CanBindPropertyToData` MUST be `false` when property-side observable change notifications are unavailable.
- `CanBindTwoWay` MUST be `true` if and only if both directions are supported.
- `IsObservableProperty` MUST reflect actual runtime/compile-time observable capability exposed to GUML tooling.

### Operator compatibility matrix

| Operator | Required capability |
|----------|---------------------|
| `:` | `CanStaticMap = true` |
| `:=` | `CanBindDataToProperty = true` |
| `=:` | `CanBindPropertyToData = true` |
| `<=>` | `CanBindTwoWay = true` |

### Diagnostic requirement

When a GUML mapping operator requires unsupported direction capabilities (for example using `=:` on a non-observable property), a conforming implementation MUST produce a diagnostic.

Named diagnostic codes and severities for this chapter are defined in §10.2.

## §11.1.4 JSON Serialization Contract

A conforming implementation shall be able to emit a JSON document equivalent to the canonical C# model.

Minimum JSON top-level shape:

```json
{
  "SchemaVersion": "string",
  "GeneratedAt": "ISO-8601 datetime",
  "Types": {
    "Namespace.ClassName": {
      "Name": "ClassName",
      "QualifiedName": "Namespace.ClassName",
      "Kind": "Class",
      "BaseType": "BaseClass or null",
      "Description": "...",
      "Properties": {
        "property_name": {
          "Name": "property_name",
          "Type": "System.String",
          "Description": "...",
          "EnumValues": [
            {
              "Name": "Center",
              "Value": "4",
              "Description": "..."
            }
          ],
          "Mapping": {
            "CanStaticMap": true,
            "CanBindDataToProperty": true,
            "CanBindPropertyToData": false,
            "CanBindTwoWay": false,
            "IsObservableProperty": false,
            "ObservabilitySource": "None"
          }
        }
      },
      "Events": {
        "signal_name": {
          "Name": "signal_name",
          "Description": "...",
          "Parameters": [
            { "Name": "arg", "Type": "System.Boolean" }
          ]
        }
      }
    }
  },
  "Controllers": {
    "Namespace.MainController": {
      "FullName": "Namespace.MainController",
      "SimpleName": "MainController",
      "GumlPath": "res://gui/main.guml",
      "Properties": [
        { "Name": "UserName", "Type": "System.String" }
      ],
      "Methods": ["OnClick"]
    }
  }
}
```

JSON field names MUST follow C# property naming conventions (PascalCase) and MUST match canonical object model field names.

Type names should be canonicalized to CLR full names where possible.

JSON and C# object representations MUST be semantically equivalent.

## §11.1.5 Producer and Consumer Responsibilities

Producer responsibilities (API extractor/analyzer):

- Resolve class/property/event metadata from C# sources and referenced assemblies.
- Resolve controller metadata from `[GumlController]` usage.
- Resolve global reference registration metadata used by Source Generator analysis.
- Populate mapping capability metadata.
- Emit stable, deterministic JSON/C# object data.

Consumer responsibilities (generator/diagnostics/LSP):

- Validate mapping operators against property capability metadata.
- Use enum and type metadata for static checks and code generation.
- Use event parameter metadata for handler compatibility diagnostics.
- Use controller metadata to validate `$controller` member access and handler binding.
- Validate non-predefined global references as registered before generation; otherwise emit diagnostic.

## §11.1.6 Versioning and Compatibility

- The schema version shall be carried in `SchemaVersion`.
- Additive fields are backward-compatible.
- Removing or renaming required fields is a breaking change.
- Consumers should ignore unknown fields.

Because GUML is consumed via Source Generator and emitted as C# code at compile-time, runtime `.guml` loading is out of scope for this contract.

Breaking changes shall require a major schema version increment.

## §11.1.7 Compliance Profile

This section defines minimum conformance by field category.

### §11.1.7.1 Field categories

| Category | Meaning |
|----------|---------|
| Required | MUST exist in conforming output. |
| Optional | MAY exist; if present, must follow contract shape. |
| Derived | MAY be stored or computed; effective value MUST be equivalent. |

### §11.1.7.2 ApiDocument

| Field | Category | Requirement |
|-------|----------|-------------|
| `SchemaVersion` | Required | MUST be non-empty. |
| `GeneratedAt` | Required | MUST be present as a valid datetime. |
| `SdkVersion` | Optional | MAY be null/omitted if unknown. |
| `Types` | Required | MUST be present (empty dictionary allowed). |
| `Controllers` | Required | MUST be present (empty dictionary allowed). |

### §11.1.7.3 TypeDescriptor

| Field | Category | Requirement |
|-------|----------|-------------|
| `Name` | Required | MUST be non-empty. |
| `QualifiedName` | Required | MUST uniquely identify the type within the document. |
| `Kind` | Required | MUST be one of `Class` or `Struct`. |
| `BaseType` | Optional | MAY be null. |
| `Description` | Optional | MAY be empty string. |
| `Properties` | Required | MUST be present (empty dictionary allowed). |
| `Events` | Required | MUST be present (empty dictionary allowed). |

### §11.1.7.4 PropertyDescriptor

| Field | Category | Requirement |
|-------|----------|-------------|
| `Name` | Required | MUST be non-empty. |
| `Type` | Required | MUST be a deterministic type name. |
| `Description` | Optional | MAY be empty string. |
| `IsReadable` | Required | MUST reflect actual readable capability. |
| `IsWritable` | Required | MUST reflect actual writable capability. |
| `EnumValues` | Optional | MAY be null; SHOULD be present for enum properties. |
| `Mapping` | Required | MUST be present and internally consistent. |

### §11.1.7.5 MappingConstraintDescriptor

| Field | Category | Requirement |
|-------|----------|-------------|
| `CanStaticMap` | Derived | MUST be equivalent to static mapping support. |
| `CanBindDataToProperty` | Derived | MUST be equivalent to `:=` support. |
| `CanBindPropertyToData` | Derived | MUST be equivalent to `=:` support. |
| `CanBindTwoWay` | Derived | MUST be equivalent to `<=>` support. |
| `IsObservableProperty` | Derived | MUST match property observability. |
| `ObservabilitySource` | Optional | SHOULD be provided when `IsObservableProperty = true`. |

### §11.1.7.6 ControllerDescriptor

| Field | Category | Requirement |
|-------|----------|-------------|
| `FullName` | Required | MUST be non-empty. |
| `SimpleName` | Required | MUST be non-empty. |
| `GumlPath` | Required | MUST be non-empty and stable for the workspace context. |
| `Properties` | Required | MUST be present (empty list allowed). |
| `Methods` | Required | MUST be present (empty list allowed). |

### §11.1.7.7 EventDescriptor

| Field | Category | Requirement |
|-------|----------|-------------|
| `Name` | Required | MUST be non-empty. |
| `Description` | Optional | MAY be null or empty string. |
| `Parameters` | Required | MUST be present (empty list allowed). |

### §11.1.7.8 ParameterDescriptor

| Field | Category | Requirement |
|-------|----------|-------------|
| `Name` | Required | MUST be non-empty. |
| `Type` | Required | MUST be a deterministic type name. |

### §11.1.7.9 EnumValueDescriptor

| Field | Category | Requirement |
|-------|----------|-------------|
| `Name` | Required | MUST be non-empty. |
| `Value` | Required | MUST be non-empty and deterministic. |
| `Description` | Optional | MAY be null or empty string. |

## §11.1.8 Conformance Test Cases

This section defines minimal executable conformance scenarios for producers and consumers.

### §11.1.8.1 GUML1001 Type Not Found

- Input: GUML references a component type not present in `Types`.
- Expected: `GUML1001` diagnostic at compile-time.

### §11.1.8.2 GUML1002 Property Not Found

- Input: GUML references a property not present in the target component type's `Properties`.
- Expected: `GUML1002` diagnostic at compile-time.

### §11.1.8.3 GUML1003 Mapping Direction Unsupported

- Input: Property uses `=:` but `CanBindPropertyToData = false`.
- Expected: `GUML1003` diagnostic at compile-time.

### §11.1.8.4 GUML1004 Two-Way Requires Observability

- Input: Property uses `<=>` but `CanBindTwoWay = false` or `IsObservableProperty = false`.
- Expected: `GUML1004` diagnostic at compile-time.

### §11.1.8.5 GUML1005 Unknown Global Reference

- Input: Expression references an unknown `$xxx` global reference (only `$controller` and `$root` are supported).
- Expected: `GUML1005` diagnostic at compile-time.
