namespace GUML.Shared.Syntax;

/// <summary>
/// Severity level of a diagnostic message.
/// </summary>
public enum DiagnosticSeverity
{
    Error,
    Warning,
    Information,
    Hint
}

/// <summary>
/// A diagnostic message produced during lexing, parsing, or semantic analysis.
/// </summary>
public sealed class Diagnostic
{
    /// <summary>
    /// The diagnostic identifier (e.g. "GUML0001").
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The human-readable diagnostic message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// The severity of the diagnostic.
    /// </summary>
    public DiagnosticSeverity Severity { get; }

    /// <summary>
    /// The text span in the source where the diagnostic occurred.
    /// </summary>
    public TextSpan Span { get; }

    public Diagnostic(string id, string message, DiagnosticSeverity severity, TextSpan span)
    {
        Id = id;
        Message = message;
        Severity = severity;
        Span = span;
    }

    public override string ToString() => $"{Id}: {Message} {Span}";
}
