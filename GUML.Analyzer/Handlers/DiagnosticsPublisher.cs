using GUML.Analyzer.Utils;
using GUML.Analyzer.Workspace;
using SharedDiagSeverity = GUML.Shared.Syntax.DiagnosticSeverity;
using LspDiagSeverity = GUML.Analyzer.Utils.DiagnosticSeverity;

namespace GUML.Analyzer.Handlers;

/// <summary>
/// Converts GUML.Shared diagnostics to LSP diagnostic notifications and publishes them.
/// </summary>
public static class DiagnosticsPublisher
{
    /// <summary>
    /// Collects syntax + semantic diagnostics for a document and returns LSP diagnostics.
    /// </summary>
    public static List<LspDiagnostic> CollectDiagnostics(GumlDocument document, SemanticModel semanticModel)
    {
        var mapper = new PositionMapper(document.Text);
        var allDiags = semanticModel.GetDiagnostics();
        var result = new List<LspDiagnostic>(allDiags.Count);

        foreach (var diag in allDiags)
        {
            result.Add(new LspDiagnostic
            {
                Range = mapper.GetRange(diag.Span),
                Severity = MapSeverity(diag.Severity),
                Code = diag.Id,
                Source = "guml",
                Message = diag.Message
            });
        }

        return result;
    }

    private static LspDiagSeverity MapSeverity(SharedDiagSeverity severity)
    {
        return severity switch
        {
            SharedDiagSeverity.Error => LspDiagSeverity.Error,
            SharedDiagSeverity.Warning => LspDiagSeverity.Warning,
            SharedDiagSeverity.Information => LspDiagSeverity.Information,
            SharedDiagSeverity.Hint => LspDiagSeverity.Hint,
            _ => LspDiagSeverity.Information
        };
    }
}
