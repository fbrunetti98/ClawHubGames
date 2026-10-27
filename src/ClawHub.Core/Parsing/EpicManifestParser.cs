using System.Text.Json;

namespace ClawHub.Core.Parsing;

public sealed record EpicManifest(
    string AppName,
    string DisplayName,
    string CatalogNamespace,
    string CatalogItemId,
    string InstallLocation,
    string LaunchExecutable)
{
    /// <summary>URI con cui Epic Games Launcher avvia il gioco (gestisce login, aggiornamenti e DRM).</summary>
    public string LaunchUri =>
        $"com.epicgames.launcher://apps/{Uri.EscapeDataString($"{CatalogNamespace}:{CatalogItemId}:{AppName}")}?action=launch&silent=true";
}

/// <summary>Legge i file <c>.item</c> in <c>%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests</c>.</summary>
public static class EpicManifestParser
{
    public static EpicManifest? Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (GetBool(root, "bIsIncompleteInstall"))
            return null;

        // Solo giochi: niente plugin di Unreal Engine e niente DLC (che hanno un MainGameAppName diverso).
        if (!HasCategory(root, "games"))
            return null;

        var appName = GetString(root, "AppName");
        var mainGame = GetString(root, "MainGameAppName");
        if (string.IsNullOrEmpty(appName) || (!string.IsNullOrEmpty(mainGame) && mainGame != appName))
            return null;

        var displayName = GetString(root, "DisplayName");
        if (string.IsNullOrWhiteSpace(displayName))
            return null;

        return new EpicManifest(
            appName,
            displayName,
            GetString(root, "CatalogNamespace") ?? string.Empty,
            GetString(root, "CatalogItemId") ?? string.Empty,
            GetString(root, "InstallLocation") ?? string.Empty,
            GetString(root, "LaunchExecutable") ?? string.Empty);
    }

    private static string? GetString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static bool GetBool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;

    private static bool HasCategory(JsonElement e, string category) =>
        e.TryGetProperty("AppCategories", out var cats)
        && cats.ValueKind == JsonValueKind.Array
        && cats.EnumerateArray().Any(c => string.Equals(c.GetString(), category, StringComparison.OrdinalIgnoreCase));
}
