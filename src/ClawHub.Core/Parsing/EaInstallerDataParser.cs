using System.Xml.Linq;

namespace ClawHub.Core.Parsing;

public sealed record EaInstallerData(string? Title, IReadOnlyList<string> ContentIds, string? LauncherRelativePath)
{
    /// <summary>URI gestito da EA app (eredità di Origin) che avvia il gioco tramite il client.</summary>
    public string? LaunchUri => ContentIds.Count == 0
        ? null
        : $"origin2://game/launch?offerIds={Uri.EscapeDataString(string.Join(',', ContentIds))}&autoDownload=1";
}

/// <summary>
/// Legge <c>__Installer\installerdata.xml</c> presente nella cartella dei giochi EA.
/// Supporta sia il vecchio formato Origin (<c>DiPManifest</c>) sia quello di EA app (<c>game</c>).
/// </summary>
public static class EaInstallerDataParser
{
    public static EaInstallerData Parse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root ?? throw new FormatException("Empty installerdata.xml.");

        var contentIds = root.Descendants()
            .Where(e => e.Name.LocalName == "contentID")
            .Select(e => e.Value.Trim())
            .Where(v => v.Length > 0)
            .Distinct()
            .ToList();

        return new EaInstallerData(ReadTitle(root), contentIds, ReadLauncherPath(root));
    }

    private static string? ReadTitle(XElement root)
    {
        // Origin: <gameTitles><gameTitle locale="en_US">…</gameTitle></gameTitles>
        // EA app: <metadata><localeInfo locale="en_US"><title>…</title></localeInfo></metadata>
        var candidates = root.Descendants()
            .Where(e => e.Name.LocalName == "gameTitle"
                        || (e.Name.LocalName == "title" && e.Parent?.Name.LocalName == "localeInfo"))
            .Select(e => new
            {
                Locale = (string?)e.Attribute("locale") ?? (string?)e.Parent?.Attribute("locale"),
                Text = e.Value.Trim(),
            })
            .Where(c => c.Text.Length > 0)
            .ToList();

        return (candidates.FirstOrDefault(c => string.Equals(c.Locale, "en_US", StringComparison.OrdinalIgnoreCase))
                ?? candidates.FirstOrDefault())?.Text;
    }

    private static string? ReadLauncherPath(XElement root)
    {
        var filePath = root.Descendants()
            .Where(e => e.Name.LocalName == "filePath" && e.Parent?.Name.LocalName == "launcher")
            .Select(e => e.Value.Trim())
            .FirstOrDefault(v => v.Length > 0);
        if (filePath is null)
            return null;

        // "[HKEY_LOCAL_MACHINE\SOFTWARE\…\Install Dir]Game.exe" → "Game.exe" (relativo alla cartella del gioco)
        var close = filePath.LastIndexOf(']');
        return (close >= 0 ? filePath[(close + 1)..] : filePath).TrimStart('\\', '/');
    }
}
