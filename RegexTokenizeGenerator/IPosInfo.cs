namespace RegexTokenizeGenerator;

/// <summary>
/// Defines the position information of a text element, including start/end indices and line/column numbers.
/// </summary>
public interface IPosInfo
{
    /// <summary>
    /// The starting index (0-based) in the source string.
    /// </summary>
    public int Start { get; set; }

    /// <summary>
    /// The ending index (0-based) in the source string.
    /// </summary>
    public int End { get; set; }

    /// <summary>
    /// The line number (1-based) where the element starts.
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// The column number (1-based) where the element starts.
    /// </summary>
    public int Column { get; set; }
}
