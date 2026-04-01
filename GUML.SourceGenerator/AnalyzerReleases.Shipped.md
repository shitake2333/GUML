; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 0.1.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
GUML001 | GUML.SourceGenerator | Error | GUML parse error
GUML002 | GUML.SourceGenerator | Error | Unknown GUML component
GUML003 | GUML.SourceGenerator | Warning | Unresolvable GUML binding
GUML004 | GUML.SourceGenerator | Info | GUML view generated
GUML005 | GUML.SourceGenerator | Error | GUML file not found
GUML006 | GUML.SourceGenerator | Error | Duplicate GUML path
GUML007 | GUML.SourceGenerator | Warning | Controller not partial
