namespace GUML.Analyzer.Utils;

/// <summary>
/// Shared utilities for converting between file system paths and LSP file:// URIs.
/// </summary>
internal static class PathUtils
{
    /// <summary>
    /// Converts a <c>file://</c> URI to a local file system path.
    /// Handles percent-decoding and Windows drive-letter prefix stripping.
    /// </summary>
    public static string UriToFilePath(string uri)
    {
        if (uri.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
        {
            string path = Uri.UnescapeDataString(uri[8..]);
            // On Windows, strip the leading '/' before the drive letter (e.g. /C:/...)
            if (OperatingSystem.IsWindows() && path.Length > 0 && path[0] == '/')
                path = path[1..];
            return path.Replace('/', Path.DirectorySeparatorChar);
        }

        return uri;
    }

    /// <summary>
    /// Converts an absolute file system path to a <c>file://</c> URI.
    /// Properly escapes special characters while preserving path separators and drive letters.
    /// </summary>
    public static string FilePathToUri(string filePath)
    {
        string normalized = filePath.Replace('\\', '/');
        if (!normalized.StartsWith('/'))
            normalized = "/" + normalized;
        return "file://" + Uri.EscapeDataString(normalized)
            .Replace("%2F", "/")
            .Replace("%3A", ":");
    }
}
