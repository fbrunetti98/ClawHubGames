using Microsoft.Win32;

namespace ClawHub.Providers.Infrastructure;

/// <summary>Voce di "Programmi e funzionalità" (chiavi Uninstall del registro).</summary>
public sealed record UninstallEntry(
    string KeyName,
    string? DisplayName,
    string? Publisher,
    string? InstallLocation,
    string? DisplayIcon);

internal static class RegistryReader
{
    private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public static string? ReadString(RegistryHive hive, RegistryView view, string subKey, string valueName)
    {
        using var root = RegistryKey.OpenBaseKey(hive, view);
        using var key = root.OpenSubKey(subKey);
        return key?.GetValue(valueName) as string;
    }

    /// <summary>Apre una chiave e passa le sottochiavi a <paramref name="read"/>. Nessun risultato se la chiave non esiste.</summary>
    public static List<T> ReadSubKeys<T>(RegistryHive hive, RegistryView view, string subKey, Func<string, RegistryKey, T?> read)
        where T : class
    {
        using var root = RegistryKey.OpenBaseKey(hive, view);
        using var key = root.OpenSubKey(subKey);
        if (key is null)
            return [];

        var result = new List<T>();
        foreach (var name in key.GetSubKeyNames())
        {
            using var child = key.OpenSubKey(name);
            if (child is not null && read(name, child) is { } item)
                result.Add(item);
        }
        return result;
    }

    public static bool KeyExists(RegistryHive hive, RegistryView view, string subKey)
    {
        using var root = RegistryKey.OpenBaseKey(hive, view);
        using var key = root.OpenSubKey(subKey);
        return key is not null;
    }

    /// <summary>Tutte le voci di disinstallazione: macchina (64 e 32 bit) e utente corrente.</summary>
    public static IReadOnlyList<UninstallEntry> ReadUninstallEntries()
    {
        var sources = new[]
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64),
            (RegistryHive.LocalMachine, RegistryView.Registry32),
            (RegistryHive.CurrentUser, RegistryView.Default),
        };

        return sources
            .SelectMany(s => ReadSubKeys(s.Item1, s.Item2, UninstallPath, (name, key) => new UninstallEntry(
                name,
                key.GetValue("DisplayName") as string,
                key.GetValue("Publisher") as string,
                key.GetValue("InstallLocation") as string,
                key.GetValue("DisplayIcon") as string)))
            .GroupBy(e => e.KeyName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }
}
