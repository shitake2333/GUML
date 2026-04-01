using GUML.Shared.Syntax;

namespace GUML.Analyzer.Utils;

/// <summary>
/// Converts between LSP line/column positions and absolute character offsets
/// within a source text string.
/// </summary>
public sealed class PositionMapper
{
    private readonly int[] _lineStarts;

    /// <summary>The total number of lines in the source text.</summary>
    public int LineCount => _lineStarts.Length;

    /// <summary>
    /// Creates a mapper for the given source text.
    /// </summary>
    public PositionMapper(string text)
    {
        _lineStarts = ComputeLineStarts(text);
    }

    /// <summary>
    /// Converts a 0-based (line, character) position to an absolute character offset.
    /// </summary>
    public int GetOffset(int line, int character)
    {
        if (line < 0 || line >= _lineStarts.Length)
            return _lineStarts.Length > 0 ? _lineStarts[^1] : 0;

        return _lineStarts[line] + character;
    }

    /// <summary>
    /// Converts a 0-based (line, character) position to an absolute character offset.
    /// </summary>
    public int GetOffset(LspPosition position) => GetOffset(position.Line, position.Character);

    /// <summary>
    /// Converts an absolute character offset to a 0-based (line, character) position.
    /// </summary>
    public LspPosition GetPosition(int offset)
    {
        int line = GetLineFromOffset(offset);
        int character = offset - _lineStarts[line];
        return new LspPosition(line, character);
    }

    /// <summary>
    /// Converts a <see cref="TextSpan"/> to an <see cref="LspRange"/>.
    /// </summary>
    public LspRange GetRange(TextSpan span)
    {
        return new LspRange(GetPosition(span.Start), GetPosition(span.End));
    }

    /// <summary>
    /// Converts an <see cref="LspRange"/> to a <see cref="TextSpan"/>.
    /// </summary>
    public TextSpan GetSpan(LspRange range)
    {
        int start = GetOffset(range.Start);
        int end = GetOffset(range.End);
        return TextSpan.FromBounds(start, end);
    }

    private int GetLineFromOffset(int offset)
    {
        int lo = 0, hi = _lineStarts.Length - 1;
        while (lo <= hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (_lineStarts[mid] <= offset)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        return Math.Max(0, lo - 1);
    }

    private static int[] ComputeLineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                starts.Add(i + 1);
            }
            else if (text[i] == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                    i++; // skip \r\n as one line break
                starts.Add(i + 1);
            }
        }

        return starts.ToArray();
    }
}
