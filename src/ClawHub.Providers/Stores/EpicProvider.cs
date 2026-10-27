using ClawHub.Core.Abstractions;
using ClawHub.Core.Models;
using ClawHub.Core.Parsing;

namespace ClawHub.Providers.Stores;

/// <summary>Giochi Epic: file <c>.item</c> scritti da Epic Games Launcher in ProgramData.</summary>
public sealed class EpicProvider : ILibraryProvider
{
    private static readonly string ManifestsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Epic", "EpicGamesLauncher", "Data", "Manifests");

    public GameStore Store => GameStore.Epic;

    public bool IsAvailable() => Directory.Exists(ManifestsPath);

    public async Task<IReadOnlyList<DetectedGame>> GetInstalledGamesAsync(CancellationToken cancellationToken = default)
    {
        var games = new List<DetectedGame>();
        foreach (var file in Directory.EnumerateFiles(ManifestsPath, "*.item"))
        {
            EpicManifest? manifest;
            try
            {
                manifest = EpicManifestParser.Parse(await File.ReadAllTextAsync(file, cancellationToken));
            }
            catch (System.Text.Json.JsonException)
            {
                continue;
            }

            if (manifest is null || !Directory.Exists(manifest.InstallLocation))
                continue;

            games.Add(new DetectedGame
            {
                Store = GameStore.Epic,
                StoreId = manifest.AppName,
                Name = manifest.DisplayName,
                Launch = LaunchInfo.FromUri(manifest.LaunchUri),
                InstallDirectory = manifest.InstallLocation,
            });
        }
        return games;
    }
}
