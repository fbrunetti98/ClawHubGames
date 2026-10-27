using ClawHub.Core.Abstractions;
using ClawHub.Core.Models;
using ClawHub.Core.Parsing;
using ClawHub.Providers.Infrastructure;

namespace ClawHub.Providers.Stores;

/// <summary>
/// Giochi EA app (sperimentale): voci di disinstallazione la cui cartella contiene
/// <c>__Installer\installerdata.xml</c>. Avvio tramite EA app (<c>origin2://</c>) quando l'XML
/// dichiara i content ID, altrimenti direttamente dall'eseguibile.
/// </summary>
public sealed class EaProvider : ILibraryProvider
{
    public GameStore Store => GameStore.Ea;

    public bool IsAvailable() => true; // Non serve il client per trovare i giochi: basta l'installer data.

    public Task<IReadOnlyList<DetectedGame>> GetInstalledGamesAsync(CancellationToken cancellationToken = default)
    {
        var games = new List<DetectedGame>();
        foreach (var entry in RegistryReader.ReadUninstallEntries())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(entry.InstallLocation))
                continue;

            var dataFile = Path.Combine(entry.InstallLocation, "__Installer", "installerdata.xml");
            if (!File.Exists(dataFile))
                continue;

            EaInstallerData data;
            try
            {
                data = EaInstallerDataParser.Parse(File.ReadAllText(dataFile));
            }
            catch (Exception ex) when (ex is FormatException or System.Xml.XmlException)
            {
                continue;
            }

            var launch = data.LaunchUri is { } uri
                ? LaunchInfo.FromUri(uri)
                : data.LauncherRelativePath is { } exe && File.Exists(Path.Combine(entry.InstallLocation, exe))
                    ? LaunchInfo.FromExecutable(Path.Combine(entry.InstallLocation, exe))
                    : null;
            if (launch is null)
                continue;

            games.Add(new DetectedGame
            {
                Store = GameStore.Ea,
                StoreId = data.ContentIds.FirstOrDefault() ?? entry.KeyName,
                Name = entry.DisplayName ?? data.Title ?? new DirectoryInfo(entry.InstallLocation).Name,
                Launch = launch,
                InstallDirectory = entry.InstallLocation,
            });
        }
        return Task.FromResult<IReadOnlyList<DetectedGame>>(games);
    }
}
