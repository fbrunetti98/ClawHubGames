using ClawHub.Core.Abstractions;
using ClawHub.Core.Models;
using ClawHub.Core.Parsing;
using ClawHub.Providers.Infrastructure;
using Microsoft.Win32;

namespace ClawHub.Providers.Stores;

/// <summary>Giochi Steam: <c>libraryfolders.vdf</c> + <c>appmanifest_*.acf</c> di ogni libreria.</summary>
public sealed class SteamProvider : ILibraryProvider
{
    private const string CdnBase = "https://cdn.cloudflare.steamstatic.com/steam/apps";

    public GameStore Store => GameStore.Steam;

    public bool IsAvailable() => FindSteamPath() is not null;

    public Task<IReadOnlyList<DetectedGame>> GetInstalledGamesAsync(CancellationToken cancellationToken = default)
    {
        var steamPath = FindSteamPath();
        if (steamPath is null)
            return Task.FromResult<IReadOnlyList<DetectedGame>>([]);

        var libraries = new List<string> { steamPath };
        var libraryFile = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(libraryFile))
            libraries.AddRange(SteamLibraryParser.ParseLibraryFolders(File.ReadAllText(libraryFile)));

        var games = new List<DetectedGame>();
        foreach (var library in libraries.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var steamApps = Path.Combine(library, "steamapps");
            if (!Directory.Exists(steamApps))
                continue;

            foreach (var acf in Directory.EnumerateFiles(steamApps, "appmanifest_*.acf"))
            {
                SteamAppManifest? manifest;
                try
                {
                    manifest = SteamLibraryParser.ParseAppManifest(File.ReadAllText(acf));
                }
                catch (FormatException)
                {
                    continue; // Manifest danneggiato: lo salta senza bloccare gli altri.
                }

                if (manifest is null || !SteamLibraryParser.IsGame(manifest))
                    continue;

                games.Add(new DetectedGame
                {
                    Store = GameStore.Steam,
                    StoreId = manifest.AppId,
                    Name = manifest.Name,
                    Launch = LaunchInfo.FromUri($"steam://rungameid/{manifest.AppId}"),
                    InstallDirectory = Path.Combine(steamApps, "common", manifest.InstallDir),
                    StoreLastPlayed = manifest.LastPlayed,
                    KnownArtwork = FindArtwork(steamPath, manifest.AppId),
                });
            }
        }

        return Task.FromResult<IReadOnlyList<DetectedGame>>(games);
    }

    private static string? FindSteamPath()
    {
        var path = RegistryReader.ReadString(RegistryHive.CurrentUser, RegistryView.Default, @"Software\Valve\Steam", "SteamPath")
                   ?? RegistryReader.ReadString(RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Valve\Steam", "InstallPath");
        return path is not null && Directory.Exists(path) ? Path.GetFullPath(path) : null;
    }

    /// <summary>Usa le immagini già scaricate dal client Steam; altrimenti gli URL della CDN.</summary>
    private static ArtworkSet FindArtwork(string steamPath, string appId)
    {
        var cache = Path.Combine(steamPath, "appcache", "librarycache");
        var cdn = new ArtworkSet(
            Cover: $"{CdnBase}/{appId}/library_600x900_2x.jpg",
            Hero: $"{CdnBase}/{appId}/library_hero.jpg",
            Logo: $"{CdnBase}/{appId}/logo.png");

        return new ArtworkSet(
            Cover: FindCached(cache, appId, "library_600x900*.jpg"),
            Hero: FindCached(cache, appId, "library_hero.jpg"),
            Logo: FindCached(cache, appId, "logo.png"),
            Icon: null).Or(cdn);
    }

    private static string? FindCached(string cache, string appId, string pattern)
    {
        // Client recente: librarycache\<appid>\…\file. Client vecchio: librarycache\<appid>_file.
        var folder = Path.Combine(cache, appId);
        if (Directory.Exists(folder))
        {
            var match = Directory.EnumerateFiles(folder, pattern, SearchOption.AllDirectories).FirstOrDefault();
            if (match is not null)
                return match;
        }

        return Directory.Exists(cache)
            ? Directory.EnumerateFiles(cache, $"{appId}_{pattern}").FirstOrDefault()
            : null;
    }
}
