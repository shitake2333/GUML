using GUML.Analyzer.Handlers;
using GUML.Analyzer.Utils;
using GUML.Analyzer.Workspace;

namespace GUML.Analyzer;

/// <summary>
/// Runs standalone formatting on a project's .guml files, outputting results to the console.
/// Invoked via <c>guml-analyzer format [--path dir] [--dry-run] [--tab-size N] [--use-tabs]</c>.
/// </summary>
internal static class FormatRunner
{
    /// <summary>
    /// Entry point for the <c>format</c> command.
    /// Returns 0 if all files are already formatted (or formatting succeeded),
    /// 1 if <c>--dry-run</c> found files that need formatting, 2 on internal failure.
    /// </summary>
    public static async Task<int> RunAsync(string rootPath, bool dryRun, int tabSize, bool useTabs)
    {
        if (!Directory.Exists(rootPath))
        {
            await Console.Error.WriteLineAsync($"Error: Directory not found: {rootPath}");
            return 2;
        }

        var options = new FormattingOptions { InsertSpaces = !useTabs, TabSize = tabSize };

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
            Console.WriteLine("No .guml files found.");
            return 0;
        }

        int changedCount = 0;
        int errorCount = 0;

        foreach (string filePath in gumlFiles)
        {
            try
            {
                string text = await File.ReadAllTextAsync(filePath);
                string uri = PathUtils.FilePathToUri(filePath);
                var document = new GumlDocument(uri, text);
                var edits = FormattingHandler.Format(document, options);

                if (edits.Count == 0) continue;

                changedCount++;
                string relativePath = Path.GetRelativePath(rootPath, filePath);

                if (dryRun)
                {
                    Console.WriteLine($"  {relativePath}");
                }
                else
                {
                    // For full document format, there's a single edit replacing all text
                    string formatted = edits[0].NewText;
                    await File.WriteAllTextAsync(filePath, formatted);
                    Console.WriteLine($"  Formatted: {relativePath}");
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                string relativePath = Path.GetRelativePath(rootPath, filePath);
                await Console.Error.WriteLineAsync($"  Error formatting {relativePath}: {ex.Message}");
            }
        }

        Console.WriteLine();
        if (dryRun)
        {
            Console.WriteLine($"{changedCount} file(s) would be formatted out of {gumlFiles.Length} total.");
            return changedCount > 0 ? 1 : 0;
        }

        Console.WriteLine($"{changedCount} file(s) formatted, {errorCount} error(s), {gumlFiles.Length} total.");
        return errorCount > 0 ? 2 : 0;
    }
}
