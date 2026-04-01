namespace GUML.Shared.Syntax;

/// <summary>
/// Describes a text change applied to a source document for incremental parsing.
/// </summary>
public readonly struct TextChange
{
    /// <summary>
    /// The start position of the change in the original text.
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// The length of the text removed from the original text.
    /// </summary>
    public int OldLength { get; }

    /// <summary>
    /// The new text inserted at the start position.
    /// </summary>
    public string NewText { get; }

    public TextChange(int start, int oldLength, string newText)
    {
        Start = start;
        OldLength = oldLength;
        NewText = newText;
    }
}
