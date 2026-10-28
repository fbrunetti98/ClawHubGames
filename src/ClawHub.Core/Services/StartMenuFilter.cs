namespace ClawHub.Core.Services;

/// <summary>Collegamento del menu Start proposto per l'aggiunta manuale di un gioco.</summary>
public sealed record StartMenuEntry(string Name, string Path);

/// <summary>Scarta dal menu Start i collegamenti che non sono programmi da avviare (disinstallazione, guide, siti…).</summary>
public static class StartMenuFilter
{
    private static readonly string[] NotApps =
    [
        "uninstall", "disinstall", "remove", "readme", "read me", "leggimi", "help", "guida", "manual",
        "documentation", "documentazione", "website", "web site", "sito web", "on the web", "support",
        "license", "licenza", "release notes", "changelog", "what's new", "setup", "repair", "ripara",
    ];

    public static bool IsLikelyApp(string name)
    {
        var normalized = name.Trim().ToLowerInvariant();
        return normalized.Length > 0 && !NotApps.Any(normalized.Contains);
    }

    /// <summary>Filtra, toglie i doppioni (stesso nome in "tutti gli utenti" e "utente corrente") e ordina per nome.</summary>
    public static IReadOnlyList<StartMenuEntry> Clean(IEnumerable<StartMenuEntry> entries) =>
        entries
            .Where(e => IsLikelyApp(e.Name))
            .GroupBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => g.First())
            .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
}
