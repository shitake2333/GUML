namespace GUML.Shared.Api;

/// <summary>
/// A 0-based line/column source position for goto-definition support.
/// </summary>
public readonly record struct SourcePosition(int Line, int Column);
