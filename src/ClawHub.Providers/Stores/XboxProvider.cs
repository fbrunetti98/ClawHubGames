using ClawHub.Core.Abstractions;
using ClawHub.Core.Models;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace ClawHub.Providers.Stores;

/// <summary>
/// Giochi Xbox / Game Pass per PC: pacchetti dello Store installati per l'utente che contengono
/// <c>MicrosoftGame.config</c> (il file che il GDK richiede a ogni gioco). Si avviano tramite AUMID.
/// </summary>
public sealed class XboxProvider : ILibraryProvider
{
    public GameStore Store => GameStore.Xbox;

    public bool IsAvailable() => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041);

    public async Task<IReadOnlyList<DetectedGame>> GetInstalledGamesAsync(CancellationToken cancellationToken = default)
    {
        var games = new List<DetectedGame>();
        var packageManager = new PackageManager();

        // Stringa vuota = utente corrente; non servono privilegi di amministratore.
        foreach (var package in packageManager.FindPackagesForUser(string.Empty))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (package.IsFramework || package.IsResourcePackage || package.IsBundle || package.SignatureKind != PackageSignatureKind.Store)
                continue;

            string installPath;
            try
            {
                installPath = package.InstalledPath;
            }
            catch (Exception)
            {
                continue; // Pacchetto in fase di installazione o su un disco scollegato.
            }

            if (!File.Exists(Path.Combine(installPath, "MicrosoftGame.config")))
                continue;

            var entries = await package.GetAppListEntriesAsync();
            var entry = entries.FirstOrDefault();
            if (entry is null)
                continue;

            games.Add(new DetectedGame
            {
                Store = GameStore.Xbox,
                StoreId = entry.AppUserModelId,
                Name = SafeDisplayName(package) ?? entry.DisplayInfo.DisplayName,
                Launch = LaunchInfo.FromPackagedApp(entry.AppUserModelId),
                InstallDirectory = installPath,
                KnownArtwork = new ArtworkSet(Icon: SafeLogoPath(package)),
            });
        }
        return games;
    }

    private static string? SafeDisplayName(Package package)
    {
        try
        {
            return string.IsNullOrWhiteSpace(package.DisplayName) ? null : package.DisplayName;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? SafeLogoPath(Package package)
    {
        try
        {
            return package.Logo is { IsFile: true } logo ? logo.LocalPath : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
