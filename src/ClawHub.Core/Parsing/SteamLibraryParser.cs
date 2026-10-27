namespace ClawHub.Core.Parsing;

public sealed record SteamAppManifest(string AppId, string Name, string InstallDir, bool IsFullyInstalled, DateTimeOffset? LastPlayed);

/// <summary>Legge <c>libraryfolders.vdf</c> e i file <c>appmanifest_*.acf</c> di Steam.</summary>
public static class SteamLibraryParser
{
    /// <summary>Strumenti che Steam installa come "app" ma che non sono giochi.</summary>
    private static readonly HashSet<string> NonGameAppIds =
    [
        "228980",  // Steamworks Common Redistributables
        "1070560", // Steam Linux Runtime
        "1391110", // Steam Linux Runtime - Soldier
        "1628350", // Steam Linux Runtime - Sniper
    ];

    // Flag di AppState: 4 = completamente installato.
    private const long StateFullyInstalled = 4;

    /// <summary>Percorsi delle librerie Steam elencate in <c>libraryfolders.vdf</c>.</summary>
    public static IReadOnlyList<string> ParseLibraryFolders(string vdfText)
    {
        var root = VdfParser.Parse(vdfText);
        var folders = root.GetObject("libraryfolders") ?? root.GetObject("LibraryFolders");
        if (folders is null)
            return [];

        var result = new List<string>();
        foreach (var key in folders.Keys)
        {
            // Formato nuovo: "0" { "path" "…" }. Formato vecchio: "1" "D:\\SteamLibrary".
            var path = folders.GetObject(key)?.GetString("path")
                       ?? (int.TryParse(key, out _) ? folders.GetString(key) : null);
            if (!string.IsNullOrWhiteSpace(path))
                result.Add(path);
        }
        return result;
    }

    public static SteamAppManifest? ParseAppManifest(string acfText)
    {
        var state = VdfParser.Parse(acfText).GetObject("AppState");
        var appId = state?.GetString("appid");
        var name = state?.GetString("name");
        if (state is null || string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(name))
            return null;

        var flags = state.GetLong("StateFlags") ?? 0;
        var lastPlayedSeconds = state.GetLong("LastPlayed") ?? 0;
        DateTimeOffset? lastPlayed = lastPlayedSeconds > 0 ? DateTimeOffset.FromUnixTimeSeconds(lastPlayedSeconds) : null;

        return new SteamAppManifest(
            appId,
            name,
            state.GetString("installdir") ?? string.Empty,
            (flags & StateFullyInstalled) != 0,
            lastPlayed);
    }

    public static bool IsGame(SteamAppManifest manifest) =>
        manifest.IsFullyInstalled
        && !NonGameAppIds.Contains(manifest.AppId)
        && !manifest.Name.StartsWith("Proton ", StringComparison.OrdinalIgnoreCase);
}
