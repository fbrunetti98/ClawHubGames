namespace ClawHub.Core.Storage;

/// <summary>Cartelle dei dati dell'app, di default in <c>%LocalAppData%\ClawHubGames</c>.</summary>
public sealed class AppPaths(string rootDirectory)
{
    public static AppPaths Default { get; } = new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClawHubGames"));

    public string Root { get; } = rootDirectory;
    public string LibraryFile => Path.Combine(Root, "library.json");
    public string SettingsFile => Path.Combine(Root, "settings.json");
    public string ArtworkCache => Path.Combine(Root, "cache", "artwork");
    public string Logs => Path.Combine(Root, "logs");
}
