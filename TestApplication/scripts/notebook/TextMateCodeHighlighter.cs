#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using Godot;

namespace TestApplication.scripts.notebook;

/// <summary>
/// Singleton service that tokenizes source code into <see cref="LineTokens"/> lists
/// using TextMateSharp grammars. Results are cached to avoid repeated tokenization.
/// </summary>
/// <remarks>
/// Supported language IDs: <c>"csharp"</c>, <c>"guml"</c>.
/// For GUML, the grammar is loaded from the VS Code extension syntax file at
/// <c>IDESupport/VSC/syntaxes/guml.tmlanguage.json</c> (copied to
/// <c>res://notebook_assets/guml.tmlanguage.json</c> at build time).
/// </remarks>
public sealed class TextMateCodeHighlighter
{
    // -----------------------------------------------------------------------
    // Singleton
    // -----------------------------------------------------------------------
    private static TextMateCodeHighlighter? s_instance;
    public static TextMateCodeHighlighter Instance => s_instance ??= new TextMateCodeHighlighter();

    // -----------------------------------------------------------------------
    // TextMateSharp state
    // -----------------------------------------------------------------------
    private readonly Registry _registry;
    private IGrammar? _csGrammar;
    private IGrammar? _gumlGrammar;
    private readonly TextMateSharp.Themes.Theme? _theme;

    // -----------------------------------------------------------------------
    // Token cache: (code, languageId) -> tokenized lines
    // -----------------------------------------------------------------------
    private readonly Dictionary<(string code, string lang), IReadOnlyList<LineTokens>> _cache = new();

    // -----------------------------------------------------------------------
    // Colors used when a token has no foreground override
    // -----------------------------------------------------------------------
    private static readonly Color s_defaultForeground = new(0.85f, 0.85f, 0.85f);
    public static readonly Color DefaultBackground  = new(0.09f, 0.09f, 0.11f); // VS Code-like code bg

    private TextMateCodeHighlighter()
    {
        var options = new RegistryOptions(ThemeName.DarkPlus);
        _registry = new Registry(options);
        _theme = _registry.GetTheme();
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Pre-initializes grammar objects to avoid first-use latency.
    /// Call this during application startup.
    /// </summary>
    public void Warmup()
    {
        GetGrammar("csharp");
        GetGrammar("guml");
    }

    /// <summary>
    /// Returns the background color associated with the current theme.
    /// </summary>
    public Color GetBackground() => DefaultBackground;

    /// <summary>
    /// Tokenizes <paramref name="code"/> using the grammar for
    /// <paramref name="languageId"/> and returns per-line colored spans.
    /// Results are cached.
    /// </summary>
    public IReadOnlyList<LineTokens> Tokenize(string code, string languageId)
    {
        var key = (code, languageId.ToLowerInvariant());
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var grammar = GetGrammar(languageId);
        if (grammar is null)
        {
            // Fallback: no grammar found, return plain white text per line
            string[] lines = code.Split('\n');
            var plain = new List<LineTokens>(lines.Length);
            foreach (string l in lines)
                plain.Add(new LineTokens([new StyledSpan(l.TrimEnd('\r'), s_defaultForeground)]));
            _cache[key] = plain;
            return plain;
        }

        var result = TokenizeWithGrammar(code, grammar);
        _cache[key] = result;
        return result;
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private IGrammar? GetGrammar(string languageId)
    {
        return languageId.ToLowerInvariant() switch
        {
            "csharp" or "cs" => _csGrammar  ??= LoadCSharpGrammar(),
            "guml"           => _gumlGrammar ??= LoadGumlGrammar(),
            _                => null
        };
    }

    private IGrammar? LoadCSharpGrammar()
    {
        try { return _registry.LoadGrammar("source.cs"); }
        catch (Exception e) { GD.PrintErr($"[TextMateCodeHighlighter] Failed to load C# grammar: {e.Message}"); return null; }
    }

    private IGrammar? LoadGumlGrammar()
    {
        // Path copied by the .csproj target: res://notebook_assets/guml.tmlanguage.json
        string path = ProjectSettings.GlobalizePath("res://notebook_assets/guml.tmlanguage.json");
        if (!File.Exists(path))
        {
            GD.PrintErr($"[TextMateCodeHighlighter] GUML grammar not found at: {path}");
            return null;
        }
        try
        {
            return _registry.LoadGrammarFromPathSync(path, 0, new Dictionary<string, int>());
        }
        catch (Exception e)
        {
            GD.PrintErr($"[TextMateCodeHighlighter] Failed to load GUML grammar: {e.Message}");
            return null;
        }
    }

    private IReadOnlyList<LineTokens> TokenizeWithGrammar(string code, IGrammar grammar)
    {
        string[] rawLines = code.Split('\n');
        var result   = new List<LineTokens>(rawLines.Length);
        IStateStack? ruleStack = null;

        foreach (string rawLine in rawLines)
        {
            string line = rawLine.TrimEnd('\r');
            var tokenized = grammar.TokenizeLine(new LineText(line), ruleStack, TimeSpan.MaxValue);
            ruleStack = tokenized.RuleStack;

            var spans = new List<StyledSpan>();
            var tokens = tokenized.Tokens;
            foreach (var token in tokens)
            {
                int start  = token.StartIndex;
                int end    = token.EndIndex;
                if (start >= end || end > line.Length) continue;

                string text = line[start..end];
                var rules = _theme?.Match(token.Scopes);
                Color fg = s_defaultForeground;
                if (rules is { Count: > 0 })
                {
                    // Rules are ordered by priority; find first that sets a foreground
                    foreach (var rule in rules)
                    {
                        if (rule.foreground > 0)
                        {
                            string hex = _theme!.GetColor(rule.foreground);
                            fg = ParseHexColor(hex) ?? s_defaultForeground;
                            break;
                        }
                    }
                }

                spans.Add(new StyledSpan(text, fg));
            }

            // Ensure empty lines still produce one span so height calculation works
            if (spans.Count == 0)
                spans.Add(new StyledSpan("", s_defaultForeground));

            result.Add(new LineTokens(spans));
        }

        return result;
    }

    private static Color? ParseHexColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6 && uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out uint rgb))
            return new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >>  8) & 0xFF) / 255f,
                ( rgb        & 0xFF) / 255f);
        if (hex.Length == 8 && uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out uint rgba))
            return new Color(
                ((rgba >> 24) & 0xFF) / 255f,
                ((rgba >> 16) & 0xFF) / 255f,
                ((rgba >>  8) & 0xFF) / 255f,
                ( rgba        & 0xFF) / 255f);
        return null;
    }
}
