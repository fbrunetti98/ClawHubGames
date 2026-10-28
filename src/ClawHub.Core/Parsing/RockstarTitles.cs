using System.Text.RegularExpressions;

namespace ClawHub.Core.Parsing;

/// <summary>
/// Giochi del Rockstar Games Launcher. Il launcher scrive per ogni gioco una voce di disinstallazione con
/// <c>"…\Launcher\uninstall.exe" -uninstall=&lt;titleId&gt;</c> (es. <c>gta5</c> per Legacy, <c>gta5_gen9</c> per Enhanced).
/// </summary>
public static partial class RockstarTitles
{
    [GeneratedRegex(@"(?:Launcher|uninstall)\.exe.+?uninstall=""?([A-Za-z0-9_]+)", RegexOptions.IgnoreCase)]
    private static partial Regex UninstallCommand();

    /// <summary>Title ID del gioco dalla stringa di disinstallazione, o null se non è un gioco Rockstar.</summary>
    public static string? FindTitleId(string? uninstallString)
    {
        if (string.IsNullOrWhiteSpace(uninstallString))
            return null;
        var match = UninstallCommand().Match(uninstallString);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Argomenti per <c>Launcher.exe</c>: il launcher avvia il gioco installato nella cartella indicata.
    /// La barra finale va tolta, altrimenti <c>\"</c> chiude male le virgolette.
    /// </summary>
    public static string LaunchArguments(string installDirectory) =>
        $"-launchTitleInFolder \"{installDirectory.Trim().Trim('"').TrimEnd('\\', '/')}\"";
}
