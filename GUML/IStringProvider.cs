using System.ComponentModel;

namespace GUML;

/// <summary>
/// Abstraction for i18n string translation used by GUML binding expressions.
/// Implement this interface (e.g., via gettext) and assign it to
/// <see cref="Guml.StringProvider"/> before loading any GUML file that uses
/// <c>tr()</c> or <c>ntr()</c> expressions.
/// </summary>
/// <remarks>
/// Must implement <see cref="INotifyPropertyChanged"/> and raise
/// <see cref="INotifyPropertyChanged.PropertyChanged"/> with
/// <see cref="CurrentLocale"/> as the property name whenever the active locale
/// changes. This allows <see cref="GuiController"/> to relay the notification
/// to all dependent binding expressions.
/// </remarks>
public interface IStringProvider : INotifyPropertyChanged
{
    /// <summary>
    /// The identifier of the currently active locale (e.g., <c>"en"</c>, <c>"zh_CN"</c>).
    /// Changing this property must raise <see cref="INotifyPropertyChanged.PropertyChanged"/>
    /// so that <see cref="GuiController"/> can refresh i18n bindings.
    /// </summary>
    string CurrentLocale { get; }

    /// <summary>
    /// Translates a singular message.
    /// </summary>
    /// <param name="msgid">
    /// The source-language (developer-language) message string used as the gettext key.
    /// </param>
    /// <param name="context">
    /// Optional gettext context (<c>msgctxt</c>) to disambiguate identical source strings.
    /// </param>
    /// <param name="args">
    /// Optional named arguments for placeholder substitution inside the translated string.
    /// </param>
    /// <returns>The translated string, or <paramref name="msgid"/> if no translation is found.</returns>
    string Tr(string msgid, string? context = null,
        IReadOnlyDictionary<string, object?>? args = null);

    /// <summary>
    /// Translates a message with singular/plural forms.
    /// </summary>
    /// <param name="msgidSingular">The singular source-language string (gettext key).</param>
    /// <param name="msgidPlural">The plural source-language string.</param>
    /// <param name="count">The quantity that determines which plural form to use.</param>
    /// <param name="context">Optional gettext context (<c>msgctxt</c>).</param>
    /// <param name="args">Optional named arguments for placeholder substitution.</param>
    /// <returns>The appropriate translated plural form.</returns>
    string Ntr(string msgidSingular, string msgidPlural, long count,
        string? context = null,
        IReadOnlyDictionary<string, object?>? args = null);
}
