namespace GUML.Shared.Converter;

/// <summary>
/// Provides static helpers for converting between GUML key-name conventions
/// (snake_case / kebab-case) and C# identifier conventions (PascalCase / camelCase).
/// </summary>
public static class KeyConverter
{
    /// <summary>
    /// Converts a PascalCase or camelCase string to snake_case.
    /// </summary>
    public static string FromCamelCase(string str)
    {
        str = char.ToLower(str[0]) + str.Substring(1);

        str = new Regex("(?<char>[A-Z])").Replace(str, match => '_' + match.Groups["char"].Value.ToLowerInvariant());
        return str;
    }

    public static string ToPascalCase(string str)
    {
        string text =
            new Regex("([_\\-])(?<char>[a-z])").Replace(str, match => match.Groups["char"].Value.ToUpperInvariant());
        return char.ToUpperInvariant(text[0]) + text.Substring(1);
    }

    public static string ToCamelCase(string str)
    {
        if (string.IsNullOrEmpty(str)) return str;
        // Convert PascalCase to camelCase
        return char.ToLowerInvariant(str[0]) + str.Substring(1);
    }
}
