using ClawHub.Core.Abstractions;
using ClawHub.Core.Models;
using ClawHub.Providers.Infrastructure;
using Microsoft.Win32;

namespace ClawHub.Providers.Stores;

/// <summary>
/// Giochi GOG: chiavi <c>HKLM\SOFTWARE\WOW6432Node\GOG.com\Games\&lt;id&gt;</c>, scritte sia da GOG Galaxy
/// sia dagli installer offline. I giochi GOG sono senza DRM, quindi si avviano direttamente dall'eseguibile.
/// </summary>
public sealed class GogProvider : ILibraryProvider
{
    private const string GamesKey = @"SOFTWARE\GOG.com\Games";

    public GameStore Store => GameStore.Gog;

    public bool IsAvailable() => RegistryReader.KeyExists(RegistryHive.LocalMachine, RegistryView.Registry32, GamesKey);

    public Task<IReadOnlyList<DetectedGame>> GetInstalledGamesAsync(CancellationToken cancellationToken = default)
    {
        var games = RegistryReader.ReadSubKeys(RegistryHive.LocalMachine, RegistryView.Registry32, GamesKey, (keyName, key) =>
        {
            // I DLC hanno "dependsOn" valorizzato con l'id del gioco base.
            if (key.GetValue("dependsOn") is string dependsOn && dependsOn.Length > 0)
                return null;

            var name = key.GetValue("gameName") as string;
            var exe = key.GetValue("exe") as string;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
                return null;

            return new DetectedGame
            {
                Store = GameStore.Gog,
                StoreId = key.GetValue("gameID") as string ?? keyName,
                Name = name,
                Launch = LaunchInfo.FromExecutable(exe, key.GetValue("launchParam") as string, key.GetValue("workingDir") as string),
                InstallDirectory = key.GetValue("path") as string,
            };
        });

        return Task.FromResult<IReadOnlyList<DetectedGame>>(games);
    }
}
