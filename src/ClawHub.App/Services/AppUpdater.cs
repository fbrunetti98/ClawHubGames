using Velopack;
using Velopack.Sources;

namespace ClawHub.App.Services;

/// <summary>
/// Aggiornamenti dalle release di GitHub tramite Velopack. Funziona solo nella versione installata con il Setup;
/// nella versione portable (zip) non fa nulla. Gli aggiornamenti scaricati si installano al riavvio.
/// </summary>
internal sealed class AppUpdater
{
    private const string RepositoryUrl = "https://github.com/fbrunetti98/ClawHubGames";

    private VelopackAsset? _ready;

    public AppUpdater()
    {
        try
        {
            var manager = CreateManager(previews: true);
            IsInstalled = manager.IsInstalled;
            CurrentVersion = manager.CurrentVersion?.ToString();
        }
        catch (Exception)
        {
            IsInstalled = false;
        }
    }

    public bool IsInstalled { get; }

    public string? CurrentVersion { get; }

    /// <summary>Versione scaricata e pronta da installare, o null.</summary>
    public string? ReadyVersion => _ready?.Version.ToString();

    public bool IsChecking { get; private set; }

    /// <summary>Cerca e scarica l'ultima versione. Restituisce la versione pronta o null se non ce ne sono.</summary>
    public async Task<string?> CheckAndDownloadAsync(bool previews)
    {
        if (!IsInstalled || IsChecking)
            return ReadyVersion;

        IsChecking = true;
        try
        {
            var manager = CreateManager(previews);
            var update = await manager.CheckForUpdatesAsync();
            if (update is null)
                return ReadyVersion;
            await manager.DownloadUpdatesAsync(update, null, CancellationToken.None);
            _ready = update.TargetFullRelease;
            return ReadyVersion;
        }
        finally
        {
            IsChecking = false;
        }
    }

    /// <summary>Chiude il launcher, installa la versione scaricata e lo riapre.</summary>
    public void ApplyAndRestart()
    {
        if (_ready is not null)
            CreateManager(previews: true).ApplyUpdatesAndRestart(_ready, []);
    }

    private static UpdateManager CreateManager(bool previews) => new(new GithubSource(RepositoryUrl, null, previews));
}
