using System.Text;
using System.Text.RegularExpressions;

namespace RegexTokenizeGenerator;

/// <summary>
/// Represents a token produced by the tokenizer.
/// </summary>
public struct Token : IPosInfo
{
    /// <summary>
    /// The name/type of the token (e.g., "identifier", "number", "operator").
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// The actual text value of the token.
    /// </summary>
    public string Value { get; init; }

    /// <inheritdoc/>
    public int Start { get; set; }

    /// <inheritdoc/>
    public int End { get; set; }

    /// <inheritdoc/>
    public int Line { get; set; }

    /// <inheritdoc/>
    public int Column { get; set; }

    public override string ToString() => $"<Token {Name}: {Regex.Escape(Value)} start: {Start}, end: {End}, line: {Line}, column: {Column}>";
}

/// <summary>
/// Exception thrown during tokenization errors, providing detailed diagnostic information.
/// </summary>
public class TokenizeException : Exception
{
    /// <summary>
    /// The line number (1-based) where the error occurred.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// The column number (1-based) where the error occurred.
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// The entire source code string where the error happened.
    /// </summary>
    public string CodeString { get; }

    /// <summary>
    /// The index in the source code where the error starts.
    /// </summary>
    public int StartIndex { get; }

    /// <summary>
    /// The length of the erroneous text segment.
    /// </summary>
    public int Length { get; }

    public TokenizeException(string message, string codeString, int startIndex, int length, int line, int column)
        : base($"{message} at {line}:{column}.")
    {
        CodeString = codeString;
        StartIndex = startIndex;
        Length = length;
        Line = line;
        Column = column;
    }

    /// <summary>
    /// Generates a diagnostic string showing the error location in the source code with a pointer.
    /// </summary>
    /// <returns>A formatted error message with visual context.</returns>
    public string PrintDiagnostic()
    {
        var lines = CodeString.Split('\n');
        // Line is 1-based
        if (Line < 1 || Line > lines.Length) return Message;

        var lineContent = lines[Line - 1].TrimEnd(); // Remove \r if any

        // Calculate pointer position
        var pointerLine = new StringBuilder();

        // Count leading tabs to align properly
        for (int i = 1; i < Column; i++)
        {
            if (i - 1 < lineContent.Length && lineContent[i - 1] == '\t')
                pointerLine.Append('\t');
            else
                pointerLine.Append(' ');
        }

        int markerLength = Math.Max(1, Length);
        for(int i = 0; i < markerLength; i++)
        {
             pointerLine.Append('^');
        }

        var sb = new StringBuilder();
        var lineStr = Line.ToString();
        var padding = new string(' ', lineStr.Length);

        sb.AppendLine($"error: {Message}");
        sb.AppendLine($"  --> line:{Line}:{Column}");
        sb.AppendLine($" {padding} |");
        sb.AppendLine($" {lineStr} | {lineContent}");
        sb.AppendLine($" {padding} | {pointerLine} {Message}");

        return sb.ToString();
    }
}

/// <summary>
/// Proivdes functionality for tokenizing source code strings.
/// </summary>
public interface ITokenize
{
    /// <summary>
    /// The current index position in the source string.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// The source code string being tokenized.
    /// </summary>
    public string CodeString { get; }

    /// <summary>
    /// Advances the tokenizer to the next character and returns it.
    /// </summary>
    /// <returns>The next character in the sequence, or null if end of stream.</returns>
    public char? Next();

    /// <summary>
    /// Moves the tokenizer back one position and returns the character at that new position.
    /// </summary>
    /// <returns>The character after moving back, or null if already at start.</returns>
    public char? Back();

    /// <summary>
    /// Calculates the line and column number for a given absolute index.
    /// </summary>
    /// <param name="index">The 0-based index in the code string.</param>
    /// <returns>A tuple containing (Line, Column) where both are 1-based.</returns>
    public (int Line, int Column) GetPosition(int index);
}

/// <summary>
/// A customizable lexer/tokenizer generator based on regex-like patterns.
/// </summary>
/// <param name="specs">A list of token specifications. Each tuple contains:
/// 1. A name generator function: (matchedString, tokenizer) => tokenName
/// 2. A pattern matching function: (tokenizer) => matchedString</param>
public class TokenizeGenerator(List<(Func<string, ITokenize, string>, Func<ITokenize, string>)> specs)
    : ITokenize
{
    private readonly List<int> _lineStart = [0];

    private int _cacheIndex;
    private int _column;
    private int _line;

    public int Index { get; private set; }

    public string CodeString { get; private set; } = "";

    public char? Next()
    {
        if (Index >= CodeString.Length) return null;
        var ch = CodeString[Index];
        if (ch == '\n')
        {
            if (!_lineStart.Contains(Index + 1)) _lineStart.Add(Index + 1);
        }
        Index += 1;
        return ch;
    }

    public char? Back()
    {
        if (Index <= 0) return null;
        Index -= 1;
        var ch = CodeString[Index];
        return ch;
    }

    /// <summary>
    /// Calculates the line and column number for a given absolute index using binary search on line start positions.
    /// </summary>
    /// <param name="index">The 0-based index in the code string.</param>
    /// <returns>A tuple containing (Line, Column) where both are 1-based.</returns>
    public (int Line, int Column) GetPosition(int index)
    {
        // _lineStart is sorted. Use BinarySearch for O(log N) lookup.
        int binarySearchIndex = _lineStart.BinarySearch(index);

        int lineIndex;
        if (binarySearchIndex >= 0)
        {
            // Exact match: 'index' is the start of a line.
            lineIndex = binarySearchIndex;
        }
        else
        {
            // Not found. BinarySearch returns bitwise complement of the index of the first element larger than 'index'.
            // So ~binarySearchIndex is the index where 'index' should be inserted to keep order.
            // The line start applicable to 'index' is the element just before that insertion point.
            lineIndex = (~binarySearchIndex) - 1;
        }

        // Safety check: if index < 0 (before first line start, impossible given _lineStart=[0]), use 0.
        if (lineIndex < 0) lineIndex = 0;

        // Line is 1-based index into _lineStart + 1
        // Column is 1-based offset from that line's start
        return (lineIndex + 1, index - _lineStart[lineIndex] + 1);
    }

    /// <summary>
    /// Tokenizes the provided code string based on the configured patterns.
    /// </summary>
    /// <param name="code">The source code string to tokenize.</param>
    /// <returns>A list of recognized tokens.</returns>
    /// <exception cref="TokenizeException">Thrown when an unexpected character or sequence is encountered.</exception>
    public List<Token> Tokenize(string code)
    {
        var result = new List<Token>();
        if (specs.Count == 0)
        {
            return result;
        }

        CodeString = code;
        _line = 1;
        _column = 0;
        Index = 0;
        _cacheIndex = 0;

        // Reset line starts for new tokenization
        _lineStart.Clear();
        _lineStart.Add(0);

        while (Index < CodeString.Length)
        {
            var patternString = "";
            var index = 0;
            var matched = false;

            while (index < specs.Count)
            {
                var (nameFunc, patternFunc) = specs[index];
                var start = Index;

                // Try to match pattern
                patternString = patternFunc(this);

                if (patternString != "")
                {
                    matched = true;
                    _cacheIndex = Index; // Save valid index

                    var name = nameFunc(patternString, this);
                    if (name == "")
                    {
                        // Explicit skip (e.g. whitespace)
                        // Don't add token, just continue outer loop effectively (but we need to break inner loop)
                        // Wait, if name is empty, it means "ignore this token but consume it".
                        // So we matched, consumed, and we are done with this token.
                        break;
                    }

                    SetLineFormIndex(start);
                    result.Add(new Token
                    {
                        Name = name,
                        Value = patternString,
                        Start = start,
                        End = Index,
                        Line = _line,
                        Column = _column
                    });
                    break; // Found a match, break inner loop to get next token
                }
                else
                {
                    // Match failed, backtrack
                    Index = start; // Revert index for next pattern attempt
                    index += 1;
                }
            }

            if (matched)
            {
                // Continue to next token
                continue;
            }

            if (Index >= CodeString.Length) break;

            // If No match found and still content left
             var errorString = code.Substring(Index, 1);
             SetLineFormIndex(Index);
             throw new TokenizeException($"Unexpected token '{errorString}'", CodeString, Index, 1, _line, _column);
        }

        // Add EOF token
        result.Add(new Token
        {
            Name = "eof",
            Value = "eof",
            Start = CodeString.Length,
            End = CodeString.Length,
            Line = _lineStart.Count, // Approximation
            Column = 0
        });
        return result;
    }

    // Helper to calculate Line/Column from absolute index
    private void SetLineFormIndex(int start)
    {
        (_line, _column) = GetPosition(start);
    }

    /// <summary>
    /// Creates a pattern that matches an exact string (e.g. keywords, operators).
    /// </summary>
    /// <param name="pattern">The exact text to match.</param>
    /// <returns>A matching function or empty string on mismatch.</returns>
    public static Func<ITokenize, string> ValuePattern(string pattern) => tokenize =>
    {
        // Try to read 'pattern' length chars
        var startIdx = tokenize.Index;
        foreach (var ch in pattern)
        {
            var nextCh = tokenize.Next();
            if (nextCh != ch)
            {
                // Mismatch
                return "";
            }
        }
        return pattern;
    };

    private class TrieNode
    {
        public bool IsEndOfWord;
        public string? Word;
        public Dictionary<char, TrieNode> Children = [];
    }

    /// <summary>
    /// Creates a pattern that matches one of several provided strings.
    /// Uses a Trie structure for efficient longest-prefix matching.
    /// </summary>
    /// <param name="patterns">The array of possible string values.</param>
    /// <returns>A matching function that returns the matched string.</returns>
    public static Func<ITokenize, string> ValuesPattern(string[] patterns)
    {
        var root = new TrieNode();

        foreach (var pattern in patterns)
        {
            var current = root;
            foreach (var ch in pattern)
            {
                if (!current.Children.TryGetValue(ch, out var next))
                {
                    next = new TrieNode();
                    current.Children[ch] = next;
                }
                current = next;
            }
            current.IsEndOfWord = true;
            current.Word = pattern;
        }

        return tokenize =>
        {
            var current = root;

            // The length of the longest match found so far
            var lastMatchLength = 0;
            // The value of the longest match found so far
            string? lastMatchWord = null;

            // Current depth of traversal (consumed characters)
            var currentLength = 0;

            while (true)
            {
                var ch = tokenize.Next();
                if (ch == null) break;

                // We consumed a char, increase length
                currentLength++;

                if (current.Children.TryGetValue(ch.Value, out var next))
                {
                    current = next;
                    if (current.IsEndOfWord)
                    {
                        lastMatchLength = currentLength;
                        lastMatchWord = current.Word;
                    }
                }
                else
                {
                    // Mismatch, stop search
                    break;
                }
            }

            // If we found a match (lastMatchWord != null), we should be at index + lastMatchLength.
            // Currently we are at index + currentLength.
            // So we need to rewind (currentLength - lastMatchLength).

            // If we didn't find any match, lastMatchLength is 0.
            // We rewind currentLength steps to restore state to start.

            var rewindSteps = currentLength - lastMatchLength;
            for (var i = 0; i < rewindSteps; i++)
            {
                tokenize.Back();
            }

            return lastMatchWord ?? "";
        };
    }

    /// <summary>
    /// Matches any single character from the allowed set (e.g. whitespace).
    /// </summary>
    public static Func<ITokenize, string> CharsPattern(char[] chars) =>
        tokenize =>
        {
            var ch = tokenize.Next();
            if (ch != null && chars.Contains(ch.Value))
            {
                return ch.Value.ToString();
            }
            return "";
        };

    /// <summary>
    /// Matches a specific single character.
    /// </summary>
    public static Func<ITokenize, string> CharPattern(char patternChar) =>
        tokenize =>
        {
            var ch = tokenize.Next();
            return patternChar == ch ? patternChar.ToString() : "";
        };

    /// <summary>
    /// Matches a line comment starting with a specific prefix. Everything until newline is consumed.
    /// </summary>
    public static Func<ITokenize, string> CommentPattern(string commentStart) =>
        tokenize =>
        {
            if (commentStart.Length == 0)
            {
                throw new ArgumentException("Comment string should not be empty.");
            }

            var startIdx = tokenize.Index;

            // Check start sequence
            foreach (var ch in commentStart)
            {
                if (tokenize.Next() != ch)
                {
                   return "";
                }
            }

            var commentStr = new StringBuilder(commentStart);
            var currentChar = tokenize.Next();

            while (currentChar != null && currentChar.Value != '\n')
            {
                commentStr.Append(currentChar);
                currentChar = tokenize.Next();
            }

            // Note: newline is consumed or we hit EOF.
            // If we want to include newline in comment or stop before it?
            // Usually comment goes to end of line.

            return commentStr.ToString();
        };

    /// <summary>
    /// Matches a standard string literal enclosed in single or double quotes, supporting common C-style escapes.
    /// Throws TokenizeException for unterminated strings or inline newlines.
    /// </summary>
    public static Func<ITokenize, string> StringPattern() =>
        tokenize =>
        {
            var result = new StringBuilder();
            var quoteChar = tokenize.Next();

            if (quoteChar != '\'' && quoteChar != '"')
            {
                return "";
            }

            // Add opening quote? Original code didn't enable standard string capturing unless we return the whole thing.
            // Usually we want the whole string literal including quotes.
            result.Append(quoteChar);

            bool isEscape = false;
            var currentChar = tokenize.Next();
            while (currentChar != null)
            {
                if (isEscape)
                {
                    // Escape sequences
                    result.Append('\\'); // Keep escape char in raw token?
                    // Or interpret it?
                    // Original code: result.Append('\\'); then result.Append(ch);
                    // It seems the output 'Value' is the raw string literal.
                    result.Append(currentChar);
                    isEscape = false;
                }
                else if (currentChar == '\\')
                {
                    isEscape = true;
                    // Don't append yet, wait for next char
                }
                else if (currentChar == quoteChar)
                {
                    result.Append(currentChar);
                    return result.ToString();
                }
                else if (currentChar == '\n')
                {
                    // Newline in string literal usually not allowed unless multiline string
                    var (line, col) = tokenize.GetPosition(tokenize.Index - 1);
                    throw new TokenizeException("String literal must be on a single line.", tokenize.CodeString, tokenize.Index - 1, 1, line, col);
                }
                else
                {
                    result.Append(currentChar);
                }

                currentChar = tokenize.Next();
            }

            // End of file reached without closing quote
            var (eofLine, eofCol) = tokenize.GetPosition(tokenize.Index);
            throw new TokenizeException("Unterminated string literal.", tokenize.CodeString, tokenize.Index, 1, eofLine, eofCol);
        };

    /// <summary>
    /// Matches decimal numbers (integer or float).
    /// </summary>
    /// <param name="hasDecimal">If true, allows a decimal point (e.g. 1.23).</param>
    public static Func<ITokenize, string> NumberPattern(bool hasDecimal = false) =>
        tokenize =>
        {
            var result = new StringBuilder();
            var hasDot = false;

            while (true)
            {
                var ch = tokenize.Next();
                if (ch == null) break;

                if (char.IsDigit(ch.Value))
                {
                    result.Append(ch);
                }
                else if (hasDecimal && ch == '.')
                {
                    if (hasDot) // Already had a dot
                    {
                        tokenize.Back(); // Put back the second dot
                        break;
                    }
                    hasDot = true;
                    result.Append(ch);
                }
                else
                {
                    // Not a digit or valid dot
                    tokenize.Back();
                    break;
                }
            }

            return result.Length > 0 ? result.ToString() : "";
        };
}
