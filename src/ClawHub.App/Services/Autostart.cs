using Microsoft.Win32;

namespace ClawHub.App.Services;

/// <summary>"Avvia con Windows": voce in HKCU\...\Run, senza permessi di amministratore.</summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ClawHubGames";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    /// <summary>Usa il percorso dell'exe in uso: con l'installer è fisso anche dopo gli aggiornamenti.</summary>
    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled && Environment.ProcessPath is { } exe)
            key.SetValue(ValueName, $"\"{exe}\"");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
