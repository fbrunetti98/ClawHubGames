using ClawHub.Core.Abstractions;
using ClawHub.Core.Artwork;
using ClawHub.Core.Localization;
using ClawHub.Core.Models;
using ClawHub.Core.Services;
using ClawHub.Core.Storage;
using ClawHub.Providers;
using ClawHub.Providers.Infrastructure;
using Microsoft.UI.Xaml;

namespace ClawHub.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    private static readonly HttpClient Http = CreateHttpClient();

    public static AppPaths Paths { get; } = AppPaths.Default;
    public static JsonFileStore<AppSettings> SettingsStore { get; } = new(Paths.SettingsFile);
    public static LibraryService Library { get; } = new(LibraryProviders.CreateAll(), new JsonFileStore<UserLibrary>(Paths.LibraryFile));
    public static ArtworkUpdater Artwork { get; } = new(Library, new ArtworkService(Http, Paths.ArtworkCache));
    public static IGameLauncher Launcher { get; } = new ShellGameLauncher();
    public static Localizer Strings { get; } = new();
    public static AppSettings Settings { get; set; } = new();

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        Settings = await SettingsStore.LoadAsync();
        Strings.SetLanguage(Settings.Language);

        _window = new MainWindow();
        _window.Activate();
    }

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ClawHubGames/0.2 (+https://github.com/fbrunetti98/ClawHubGames)");
        return http;
    }
}
