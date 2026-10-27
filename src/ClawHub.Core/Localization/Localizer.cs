using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace ClawHub.Core.Localization;

/// <summary>
/// Testi dell'interfaccia letti da file JSON incorporati (<c>Strings/it.json</c>, <c>Strings/en.json</c>).
/// La lingua si cambia a runtime senza riavviare l'app. Le chiavi mancanti ripiegano sull'inglese.
/// </summary>
public sealed class Localizer
{
    public const string SystemLanguage = "system";
    public const string FallbackLanguage = "en";

    public static IReadOnlyList<string> SupportedLanguages { get; } = ["it", "en"];

    private readonly Dictionary<string, string> _fallback;
    private Dictionary<string, string> _current;

    public Localizer(string language = SystemLanguage)
    {
        _fallback = LoadStrings(FallbackLanguage);
        _current = _fallback;
        SetLanguage(language);
    }

    public event EventHandler? LanguageChanged;

    /// <summary>Lingua effettiva in uso (mai "system").</summary>
    public string CurrentLanguage { get; private set; } = FallbackLanguage;

    public string this[string key] =>
        _current.TryGetValue(key, out var value) || _fallback.TryGetValue(key, out value) ? value : key;

    public string Format(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, this[key], args);

    public void SetLanguage(string language)
    {
        var resolved = Resolve(language);
        if (resolved == CurrentLanguage && _current.Count > 0)
            return;

        _current = resolved == FallbackLanguage ? _fallback : LoadStrings(resolved);
        CurrentLanguage = resolved;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public static string Resolve(string language)
    {
        if (string.IsNullOrWhiteSpace(language) || language == SystemLanguage)
            language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        language = language.Split('-')[0].ToLowerInvariant();
        return SupportedLanguages.Contains(language) ? language : FallbackLanguage;
    }

    private static Dictionary<string, string> LoadStrings(string language)
    {
        var assembly = typeof(Localizer).Assembly;
        using var stream = assembly.GetManifestResourceStream($"ClawHub.Strings.{language}.json");
        if (stream is null)
            return [];
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
    }
}
