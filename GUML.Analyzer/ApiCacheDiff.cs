namespace GUML.Analyzer;

/// <summary>
/// Describes the changes made to the API cache during an incremental rebuild.
/// A <c>null</c> diff (passed via <see cref="ProjectAnalyzer.CacheUpdated"/>)
/// indicates a full rebuild (initialization or fallback).
/// </summary>
public sealed class ApiCacheDiff
{
    /// <summary>New or updated type names.</summary>
    public List<string> UpdatedTypes { get; set; } = [];

    /// <summary>Removed type names.</summary>
    public List<string> RemovedTypes { get; set; } = [];

    /// <summary>New or updated controller GUML paths (normalized).</summary>
    public List<string> UpdatedControllers { get; set; } = [];

    /// <summary>Removed controller GUML paths.</summary>
    public List<string> RemovedControllers { get; set; } = [];

    /// <summary>Whether the diff is empty (no changes detected).</summary>
    public bool IsEmpty => UpdatedTypes.Count == 0 && RemovedTypes.Count == 0
                           && UpdatedControllers.Count == 0 && RemovedControllers.Count == 0;
}
