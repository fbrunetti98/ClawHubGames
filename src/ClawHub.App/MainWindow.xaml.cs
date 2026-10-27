using ClawHub.App.ViewModels;
using ClawHub.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ClawHub.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = App.Strings["App.Title"];
        ApplyStrings();

        App.Strings.LanguageChanged += (_, _) => ApplyStrings();
        App.Library.Changed += (_, _) => DispatcherQueue.TryEnqueue(Refresh);

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        // Prima la libreria salvata (istantanea), poi la scansione vera degli store.
        await App.Library.LoadAsync();
        await RescanAsync();
    }

    private async Task RescanAsync()
    {
        RescanButton.IsEnabled = false;
        StatusText.Text = App.Strings["Library.Scanning"];
        try
        {
            var report = await App.Library.ScanAsync(App.Settings.DisabledStores);
            StatusText.Text = report.Errors.Count == 0
                ? App.Strings.Format("Library.ScanDone", report.GameCount)
                : App.Strings.Format("Library.ScanErrors", string.Join(", ", report.Errors.Select(e => App.Strings[$"Store.{e.Store}"])));
        }
        finally
        {
            RescanButton.IsEnabled = true;
        }
    }

    private void Refresh()
    {
        var games = App.Library.Query(new LibraryQuery
        {
            Search = SearchBox.Text,
            IncludeHidden = App.Settings.ShowHiddenGames,
        });
        GamesGrid.ItemsSource = games.Select(g => new GameTile(g)).ToList();
    }

    private void ApplyStrings()
    {
        TitleText.Text = App.Strings["App.Title"];
        SearchBox.PlaceholderText = App.Strings["Library.Search"];
        RescanButton.Content = App.Strings["Library.Rescan"];
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => Refresh();

    private async void OnRescanClick(object sender, RoutedEventArgs e) => await RescanAsync();

    private async void OnGameClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not GameTile tile)
            return;

        try
        {
            await App.Launcher.LaunchAsync(tile.Game.Source.Launch);
            await App.Library.RecordLaunchAsync(tile.Game.Id);
        }
        catch (Exception ex)
        {
            StatusText.Text = App.Strings.Format("Game.LaunchFailed", ex.Message);
        }
    }
}
