namespace GUML.Shared.Syntax;

/// <summary>
/// Represents a span of text in the source document.
/// </summary>
public readonly struct TextSpan : IEquatable<TextSpan>
{
    /// <summary>
    /// The start position (zero-based character offset) of the span.
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// The number of characters in the span.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// The exclusive end position of the span.
    /// </summary>
    public int End => Start + Length;

    public TextSpan(int start, int length)
    {
        Start = start;
        Length = length;
    }

    /// <summary>
    /// Create a span from start and end positions.
    /// </summary>
    public static TextSpan FromBounds(int start, int end) => new TextSpan(start, end - start);

    /// <summary>
    /// Whether this span contains the given position.
    /// </summary>
    public bool Contains(int position) => position >= Start && position < End;

    /// <summary>
    /// Whether this span overlaps with another span.
    /// </summary>
    public bool OverlapsWith(TextSpan other) => Start < other.End && other.Start < End;

    public bool Equals(TextSpan other) => Start == other.Start && Length == other.Length;
    public override bool Equals(object? obj) => obj is TextSpan other && Equals(other);
    public override int GetHashCode() => Start ^ (Length << 16);
    public static bool operator ==(TextSpan left, TextSpan right) => left.Equals(right);
    public static bool operator !=(TextSpan left, TextSpan right) => !left.Equals(right);
    public override string ToString() => $"[{Start}..{End})";
}
