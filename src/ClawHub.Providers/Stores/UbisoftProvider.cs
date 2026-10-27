using ClawHub.Core.Abstractions;
using ClawHub.Core.Models;
using ClawHub.Providers.Infrastructure;
using Microsoft.Win32;

namespace ClawHub.Providers.Stores;

/// <summary>
/// Giochi Ubisoft Connect: <c>HKLM\SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs\&lt;id&gt;</c>.
/// Il nome arriva dalla voce di disinstallazione "Uplay Install &lt;id&gt;".
/// </summary>
public sealed class UbisoftProvider : ILibraryProvider
{
    private const string InstallsKey = @"SOFTWARE\Ubisoft\Launcher\Installs";

    public GameStore Store => GameStore.Ubisoft;

    public bool IsAvailable() => RegistryReader.KeyExists(RegistryHive.LocalMachine, RegistryView.Registry32, InstallsKey);

    public Task<IReadOnlyList<DetectedGame>> GetInstalledGamesAsync(CancellationToken cancellationToken = default)
    {
        var names = RegistryReader.ReadUninstallEntries()
            .Where(e => e.KeyName.StartsWith("Uplay Install ", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(e.DisplayName))
            .ToDictionary(e => e.KeyName["Uplay Install ".Length..].Trim(), e => e.DisplayName!, StringComparer.OrdinalIgnoreCase);

        var games = RegistryReader.ReadSubKeys(RegistryHive.LocalMachine, RegistryView.Registry32, InstallsKey, (gameId, key) =>
        {
            var installDir = key.GetValue("InstallDir") as string;
            if (string.IsNullOrWhiteSpace(installDir) || !Directory.Exists(installDir))
                return null;

            var name = names.GetValueOrDefault(gameId) ?? new DirectoryInfo(installDir).Name;
            return new DetectedGame
            {
                Store = GameStore.Ubisoft,
                StoreId = gameId,
                Name = name,
                Launch = LaunchInfo.FromUri($"uplay://launch/{gameId}/0"),
                InstallDirectory = installDir,
            };
        });

        return Task.FromResult<IReadOnlyList<DetectedGame>>(games);
    }
}
