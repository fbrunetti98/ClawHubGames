using ClawHub.Core.Abstractions;
using ClawHub.Core.Models;
using ClawHub.Core.Parsing;
using ClawHub.Providers.Infrastructure;

namespace ClawHub.Providers.Stores;

/// <summary>
/// Giochi del Rockstar Games Launcher (sperimentale): voci di disinstallazione con <c>-uninstall=&lt;titleId&gt;</c>.
/// Avvio tramite <c>Launcher.exe -launchTitleInFolder "&lt;cartella&gt;"</c>, che gestisce login e aggiornamenti.
/// </summary>
public sealed class RockstarProvider : ILibraryProvider
{
    public GameStore Store => GameStore.Rockstar;

    public bool IsAvailable() => FindClient(RegistryReader.ReadUninstallEntries()) is not null;

    public Task<IReadOnlyList<DetectedGame>> GetInstalledGamesAsync(CancellationToken cancellationToken = default)
    {
        var entries = RegistryReader.ReadUninstallEntries();
        var client = FindClient(entries);
        if (client is null)
            return Task.FromResult<IReadOnlyList<DetectedGame>>([]);

        var games = new List<DetectedGame>();
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var titleId = RockstarTitles.FindTitleId(entry.UninstallString);
            var installDir = entry.InstallLocation?.Trim().Trim('"');
            if (titleId is null || string.IsNullOrWhiteSpace(installDir) || !Directory.Exists(installDir)
                || entry.DisplayName?.StartsWith("Rockstar Games", StringComparison.OrdinalIgnoreCase) == true)
                continue;

            games.Add(new DetectedGame
            {
                Store = GameStore.Rockstar,
                StoreId = titleId,
                Name = string.IsNullOrWhiteSpace(entry.DisplayName) ? new DirectoryInfo(installDir).Name : entry.DisplayName,
                Launch = LaunchInfo.FromExecutable(client, RockstarTitles.LaunchArguments(installDir)),
                InstallDirectory = installDir,
            });
        }
        return Task.FromResult<IReadOnlyList<DetectedGame>>(games);
    }

    private static string? FindClient(IEnumerable<UninstallEntry> entries)
    {
        var location = entries.FirstOrDefault(e => string.Equals(e.DisplayName, "Rockstar Games Launcher", StringComparison.OrdinalIgnoreCase))
            ?.InstallLocation?.Trim().Trim('"');
        var exe = string.IsNullOrWhiteSpace(location) ? null : Path.Combine(location, "Launcher.exe");
        return exe is not null && File.Exists(exe) ? exe : null;
    }
}
