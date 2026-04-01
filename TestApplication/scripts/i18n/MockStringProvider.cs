#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using Godot;
using GUML;

namespace TestApplication.scripts.i18n;

/// <summary>
/// A mock <see cref="IStringProvider"/> implementation that loads translations from JSON
/// files located under <c>res://i18n/{locale}.json</c>.
///
/// <para>JSON format: a flat object where each key is either a plain msgid or a
/// <c>context|msgid</c> compound key for context-disambiguated strings.
/// Plural forms use the singular and plural msgids as separate keys.</para>
///
/// <example>
/// <code>
/// {
///   "Hello World!": "你好，世界！",
///   "Hello, {name}!": "你好，{name}！",
///   "noun|New": "新的",
///   "verb|New": "新建",
///   "One item": "一个项目",
///   "{count} items": "{count} 个项目"
/// }
/// </code>
/// </example>
/// </summary>
public class MockStringProvider : IStringProvider
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly Dictionary<string, Dictionary<string, string>> _locales = new();
    private string _currentLocale = "en";

    /// <summary>Initializes the provider and pre-loads all bundled locale files.</summary>
    public MockStringProvider()
    {
        LoadLocale("en");
        LoadLocale("zh_CN");
        LoadLocale("ja");
    }

    /// <inheritdoc />
    public string CurrentLocale
    {
        get => _currentLocale;
        set
        {
            if (_currentLocale == value) return;
            if (!_locales.ContainsKey(value)) return;
            _currentLocale = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLocale)));
        }
    }

    /// <inheritdoc />
    public string Tr(string msgid, string? context = null,
        IReadOnlyDictionary<string, object?>? args = null)
    {
        string key = context != null ? $"{context}|{msgid}" : msgid;
        string result = Lookup(key) ?? msgid;
        return Substitute(result, args);
    }

    /// <inheritdoc />
    public string Ntr(string msgidSingular, string msgidPlural, long count,
        string? context = null,
        IReadOnlyDictionary<string, object?>? args = null)
    {
        string rawKey = count == 1 ? msgidSingular : msgidPlural;
        string key = context != null ? $"{context}|{rawKey}" : rawKey;
        string result = Lookup(key) ?? rawKey;
        return Substitute(result, args);
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private void LoadLocale(string locale)
    {
        string resPath = $"res://i18n/{locale}.json";
        string filePath = ProjectSettings.GlobalizePath(resPath);

        if (!File.Exists(filePath))
        {
            _locales[locale] = new Dictionary<string, string>();
            return;
        }

        string json = File.ReadAllText(filePath);
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? new Dictionary<string, string>();
        _locales[locale] = dict;
    }

    private string? Lookup(string key)
    {
        return _locales.TryGetValue(_currentLocale, out var dict) &&
               dict.TryGetValue(key, out string? value)
            ? value
            : null;
    }

    private static string Substitute(string template, IReadOnlyDictionary<string, object?>? args)
    {
        if (args == null) return template;
        foreach (var (key, val) in args)
            template = template.Replace($"{{{key}}}", val?.ToString() ?? string.Empty);
        return template;
    }
}
