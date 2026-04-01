namespace GUML.Analyzer.Utils;

/// <summary>
/// Represents the resolved source location of a C# type declaration.
/// </summary>
/// <param name="FilePath">Absolute path to the source file.</param>
/// <param name="Line">Zero-based line number of the type declaration.</param>
/// <param name="Column">Zero-based column number of the type declaration.</param>
/// <param name="IsMetadataSource">
/// <c>true</c> if the file was generated from metadata (assembly without source);
/// <c>false</c> if it points to genuine source code.
/// </param>
/// <param name="MemberLines">
/// For metadata-as-source files, maps C# member names (PascalCase) to their zero-based line numbers.
/// <c>null</c> for genuine source files.
/// </param>
public record TypeSourceLocation(
    string FilePath,
    int Line,
    int Column,
    bool IsMetadataSource,
    IReadOnlyDictionary<string, int>? MemberLines = null);
