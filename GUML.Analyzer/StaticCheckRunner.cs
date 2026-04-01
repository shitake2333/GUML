using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Build.Locator;
using Serilog;
using GUML.Analyzer.Utils;
using GUML.Analyzer.Workspace;
using DiagnosticSeverity = GUML.Shared.Syntax.DiagnosticSeverity;

namespace GUML.Analyzer;

/// <summary>
/// Runs standalone static analysis on a Godot project's .guml files,
/// outputting diagnostics to the console without requiring an editor connection.
/// </summary>
internal static class StaticCheckRunner
{
    /// <summary>
    /// Entry point for the <c>check</c> command.
    /// Returns 0 if no errors found, 1 if errors exist, 2 on internal failure.
    /// </summary>
    public static async Task<int> RunAsync(string rootPath, string format, string severityFilter)
    {
        var minSeverity = severityFilter.ToLowerInvariant() switch
        {
            "error" => DiagnosticSeverity.Error,
            "warning" => DiagnosticSeverity.Warning,
            "hint" => DiagnosticSeverity.Hint,
            _ => DiagnosticSeverity.Information
        };

        // Initialize logging to stderr
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose)
            .MinimumLevel.Information()
            .CreateLogger();

        if (!Directory.Exists(rootPath))
        {
            await Console.Error.WriteLineAsync($"Error: Directory not found: {rootPath}");
            return 2;
        }

        Log.Logger.Information("Running static analysis on {Path}", rootPath);

        // Register MSBuild
        if (!MSBuildLocator.IsRegistered)
            MSBuildLocator.RegisterDefaults();

        try
        {
            // Initialize project analyzer
            using var analyzer = new ProjectAnalyzer();
            await analyzer.InitializeAsync(rootPath);

            if (!analyzer.IsReady)
            {
                await Console.Error.WriteLineAsync("Error: Failed to initialize project analyzer. No .csproj found or project load failed.");
                return 2;
            }

            // Scan all .guml files
            string[] gumlFiles;
            try
            {
                gumlFiles = Directory.GetFiles(rootPath, "*.guml", SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error: Failed to scan for .guml files: {ex.Message}");
                return 2;
            }

            if (gumlFiles.Length == 0)
            {
                await Console.Error.WriteLineAsync("No .guml files found.");
                return 0;
            }

            Log.Logger.Information("Found {Count} .guml file(s)", gumlFiles.Length);

            // Analyze each file
            var results = new List<FileCheckResult>();
            int totalErrors = 0, totalWarnings = 0, totalInfos = 0;

            foreach (string filePath in gumlFiles)
            {
                try
                {
                    string text = await File.ReadAllTextAsync(filePath);
                    string uri = PathUtils.FilePathToUri(filePath);
                    var document = new GumlDocument(uri, text);
                    var semanticModel = new SemanticModel(document, analyzer);
                    var diagnostics = semanticModel.GetDiagnostics();
                    var mapper = new PositionMapper(text);

                    string relativePath = Path.GetRelativePath(rootPath, filePath);
                    var fileDiags = new List<CheckDiagnostic>();

                    foreach (var diag in diagnostics)
                    {
                        if (diag.Severity > minSeverity) continue;

                        var range = mapper.GetRange(diag.Span);
                        fileDiags.Add(new CheckDiagnostic
                        {
                            Line = range.Start.Line + 1,       // 1-based for display
                            Column = range.Start.Character + 1,
                            EndLine = range.End.Line + 1,
                            EndColumn = range.End.Character + 1,
                            Severity = diag.Severity switch
                            {
                                DiagnosticSeverity.Error => "error",
                                DiagnosticSeverity.Warning => "warning",
                                DiagnosticSeverity.Information => "info",
                                _ => "hint"
                            },
                            Code = diag.Id,
                            Message = diag.Message
                        });

                        switch (diag.Severity)
                        {
                            case DiagnosticSeverity.Error: totalErrors++; break;
                            case DiagnosticSeverity.Warning: totalWarnings++; break;
                            default: totalInfos++; break;
                        }
                    }

                    if (fileDiags.Count > 0)
                    {
                        results.Add(new FileCheckResult
                        {
                            FilePath = relativePath,
                            AbsolutePath = filePath,
                            Diagnostics = fileDiags
                        });
                    }
                }
                catch (Exception ex)
                {
                    Log.Logger.Warning(ex, "Failed to analyze {File}", filePath);
                }
            }

            // Output results
            if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                OutputJson(results, totalErrors, totalWarnings, totalInfos, gumlFiles.Length);
            }
            else
            {
                OutputText(results, totalErrors, totalWarnings, totalInfos, gumlFiles.Length);
            }

            return totalErrors > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Static analysis failed");
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            return 2;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    private static void OutputText(
        List<FileCheckResult> results, int errors, int warnings, int infos, int totalFiles)
    {
        foreach (var file in results)
        {
            foreach (var diag in file.Diagnostics)
            {
                Console.WriteLine($"{file.AbsolutePath}({diag.Line},{diag.Column}): {diag.Severity} {diag.Code}: {diag.Message}");
            }
        }

        if (results.Count > 0)
            Console.WriteLine();

        Console.WriteLine(new string('\u2500', 40));
        Console.WriteLine($"{errors} error(s), {warnings} warning(s), {infos} info(s) in {totalFiles} file(s)");
    }

    private static void OutputJson(
        List<FileCheckResult> results, int errors, int warnings, int infos, int totalFiles)
    {
        var output = new
        {
            files = results.Select(f => new
            {
                path = f.FilePath,
                diagnostics = f.Diagnostics.Select(d => new
                {
                    line = d.Line,
                    column = d.Column,
                    endLine = d.EndLine,
                    endColumn = d.EndColumn,
                    severity = d.Severity,
                    code = d.Code,
                    message = d.Message
                })
            }),
            summary = new
            {
                errors,
                warnings,
                infos,
                totalFiles,
                filesWithDiagnostics = results.Count
            }
        };

        string json = JsonSerializer.Serialize(output, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        Console.WriteLine(json);
    }

    private sealed class FileCheckResult
    {
        public required string FilePath { get; init; }
        public required string AbsolutePath { get; init; }
        public required List<CheckDiagnostic> Diagnostics { get; init; }
    }

    private sealed class CheckDiagnostic
    {
        public int Line { get; init; }
        public int Column { get; init; }
        public int EndLine { get; init; }
        public int EndColumn { get; init; }
        public required string Severity { get; init; }
        public required string Code { get; init; }
        public required string Message { get; init; }
    }
}
