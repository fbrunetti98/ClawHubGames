using ClawHub.Core.Abstractions;
using ClawHub.Core.Models;
using ClawHub.Core.Parsing;
using ClawHub.Providers.Infrastructure;

namespace ClawHub.Providers.Stores;

/// <summary>
/// Giochi Battle.net (sperimentale): voci di disinstallazione di "Blizzard Entertainment" con un codice
/// prodotto noto. Avvio tramite <c>Battle.net.exe --exec="launch CODICE"</c>, che gestisce login e aggiornamenti.
/// </summary>
public sealed class BattleNetProvider : ILibraryProvider
{
    public GameStore Store => GameStore.BattleNet;

    public bool IsAvailable() => FindClient(RegistryReader.ReadUninstallEntries()) is not null;

    public Task<IReadOnlyList<DetectedGame>> GetInstalledGamesAsync(CancellationToken cancellationToken = default)
    {
        var entries = RegistryReader.ReadUninstallEntries();
        var client = FindClient(entries);
        if (client is null)
            return Task.FromResult<IReadOnlyList<DetectedGame>>([]);

        var games = entries
            .Where(e => string.Equals(e.Publisher, "Blizzard Entertainment", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(e.DisplayName)
                        && !e.DisplayName.StartsWith("Battle.net", StringComparison.OrdinalIgnoreCase))
            .Select(e => (Entry: e, Code: BattleNetProducts.FindCode(e.DisplayName!)))
            .Where(x => x.Code is not null)
            .Select(x => new DetectedGame
            {
                Store = GameStore.BattleNet,
                StoreId = x.Code!,
                Name = x.Entry.DisplayName!,
                Launch = LaunchInfo.FromExecutable(client, $"--exec=\"launch {x.Code}\""),
                InstallDirectory = x.Entry.InstallLocation,
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<DetectedGame>>(games);
    }

    private static string? FindClient(IEnumerable<UninstallEntry> entries)
    {
        var location = entries.FirstOrDefault(e => string.Equals(e.DisplayName, "Battle.net", StringComparison.OrdinalIgnoreCase))?.InstallLocation;
        var exe = location is null ? null : Path.Combine(location, "Battle.net.exe");
        return exe is not null && File.Exists(exe) ? exe : null;
    }
}
