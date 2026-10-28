using System.Globalization;
using ClawHub.App.ViewModels;
using ClawHub.Core.Artwork;
using ClawHub.Core.Models;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace ClawHub.App;

// Scheda Impostazioni: un elenco di voci raggruppate, ognuna con la sua azione.
public sealed partial class MainWindow
{
    private static readonly string[] LanguageCycle = ["system", "it", "en"];

    private readonly List<Func<Task>> _settingActions = [];

    private void RefreshSettings()
    {
        SettingsItems.Children.Clear();
        _settingsRows.Clear();
        _settingActions.Clear();

        AddSettingsGroup("Settings.Group.Library");
        AddSetting(App.Strings["Library.Rescan"],
            _scanning ? App.Strings["Library.Scanning"] : App.Strings.Format("Library.ScanDone", App.Library.Games.Count),
            () => RescanAsync(announce: true));
        AddSetting(App.Strings["Library.AddGame"], string.Empty, OpenAddGameMenuAsync);
        AddSetting(App.Strings["Settings.ManageCollections"], App.Strings.Format("Settings.CollectionsCount", App.Library.Collections.Count), () =>
        {
            OpenCollectionsManager();
            return Task.CompletedTask;
        });
        AddSetting(App.Strings["Settings.ShowHidden"], OnOff(App.Settings.ShowHiddenGames), async () =>
        {
            await SaveSettingsAsync(App.Settings with { ShowHiddenGames = !App.Settings.ShowHiddenGames });
            Refresh();
        });

        AddSettingsGroup("Settings.Group.Artwork");
        var key = App.Settings.SteamGridDbApiKey;
        AddSetting(App.Strings["Settings.SteamGridDbKey"],
            string.IsNullOrWhiteSpace(key) ? App.Strings["Settings.KeyNotSet"] : App.Strings.Format("Settings.KeySet", key[^Math.Min(4, key.Length)..]),
            () =>
            {
                AskText(App.Strings["Settings.SteamGridDbKey"], key ?? string.Empty, 64, value => _ = SetSteamGridDbKeyAsync(value));
                return Task.CompletedTask;
            });
        AddSetting(App.Strings["Settings.RedownloadArtwork"], string.Empty, RedownloadArtworkAsync);

        AddSettingsGroup("Settings.Stores");
        foreach (var store in Enum.GetValues<GameStore>().Where(s => s != GameStore.Manual))
            AddSetting(App.Strings[$"Store.{store}"], OnOff(!App.Settings.DisabledStores.Contains(store)), () => ToggleStoreAsync(store));

        AddSettingsGroup("Settings.Group.System");
        var language = App.Settings.Language == "system" ? "Settings.Language.System" : $"Settings.Language.{App.Settings.Language}";
        AddSetting(App.Strings["Settings.Language"], App.Strings[language], CycleLanguageAsync);
        AddSetting(App.Strings["Settings.Fullscreen"], OnOff(AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen), () =>
        {
            ToggleFullScreen();
            return Task.CompletedTask;
        });
        AddSetting(App.Strings["Settings.Exit"], string.Empty, () =>
        {
            Close();
            return Task.CompletedTask;
        });

        _settingsIndex = Math.Clamp(_settingsIndex, 0, _settingsRows.Count - 1);
        UpdateSettingsHighlight();
    }

    private void AddSettingsGroup(string key)
    {
        SettingsItems.Children.Add(new TextBlock
        {
            Text = App.Strings[key].ToUpper(CultureInfo.CurrentCulture),
            FontFamily = Res<FontFamily>("DisplayFont"),
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            FontSize = 11,
            CharacterSpacing = 160,
            Foreground = Res<Brush>("FaintBrush"),
            Margin = new Thickness(18, SettingsItems.Children.Count == 0 ? 4 : 18, 0, 4),
        });
    }

    private void AddSetting(string title, string value, Func<Task> run)
    {
        var index = _settingsRows.Count;
        var row = ListRow(title, value, 18);
        row.Padding = new Thickness(18, 13, 18, 13);
        row.Tapped += (_, _) =>
        {
            if (_menu is not null || Keyboard.IsOpen)
                return;
            _settingsIndex = index;
            UpdateSettingsHighlight();
            _ = ActivateSettingAsync(index);
            KeySink.Focus(FocusState.Programmatic);
        };
        _settingsRows.Add(row);
        _settingActions.Add(run);
        SettingsItems.Children.Add(row);
    }

    private void UpdateSettingsHighlight()
    {
        var active = _tab == Tab.Settings && _menu is null && !Keyboard.IsOpen;
        for (var i = 0; i < _settingsRows.Count; i++)
            SetRowState(_settingsRows[i], i == _settingsIndex, focused: i == _settingsIndex && active);
        if (_tab == Tab.Settings && _settingsIndex < _settingsRows.Count)
            _settingsRows[_settingsIndex].StartBringIntoView();
    }

    private async Task ActivateSettingAsync(int index)
    {
        if (index >= _settingActions.Count)
            return;
        try
        {
            await _settingActions[index]();
        }
        catch (Exception ex)
        {
            ShowToast(ex.Message);
        }
    }

    private async Task SaveSettingsAsync(AppSettings settings)
    {
        App.Settings = settings;
        await App.SettingsStore.SaveAsync(settings);
        RefreshSettings();
    }

    private async Task CycleLanguageAsync()
    {
        var current = Array.IndexOf(LanguageCycle, App.Settings.Language);
        var next = LanguageCycle[(current + 1) % LanguageCycle.Length];
        await SaveSettingsAsync(App.Settings with { Language = next });
        App.Strings.SetLanguage(next);
    }

    private async Task ToggleStoreAsync(GameStore store)
    {
        var disabled = new HashSet<GameStore>(App.Settings.DisabledStores);
        if (!disabled.Remove(store))
            disabled.Add(store);
        await SaveSettingsAsync(App.Settings with { DisabledStores = disabled });
        await RescanAsync(announce: true);
    }

    /// <summary>Salva la chiave solo se SteamGridDB non la rifiuta; senza rete la salva comunque.</summary>
    private async Task SetSteamGridDbKeyAsync(string key)
    {
        key = key.Trim();
        if (key.Length == 0)
        {
            await SaveSettingsAsync(App.Settings with { SteamGridDbApiKey = null });
            ShowToast(App.Strings["Toast.KeyRemoved"]);
            return;
        }

        bool? valid;
        try
        {
            valid = await new SteamGridDbClient(App.Http, key).IsKeyValidAsync();
        }
        catch (Exception)
        {
            valid = null;
        }

        if (valid == false)
        {
            ShowToast(App.Strings["Toast.KeyInvalid"]);
            return;
        }

        await SaveSettingsAsync(App.Settings with { SteamGridDbApiKey = key });
        ShowToast(App.Strings[valid == true ? "Toast.KeyValid" : "Toast.KeyUnchecked"]);
        StartArtworkUpdate();
    }

    private async Task RedownloadArtworkAsync()
    {
        await App.Library.ClearArtworkAsync();
        ImageCache.Clear(); // I file nuovi hanno lo stesso percorso di quelli vecchi.
        ShowToast(App.Strings["Toast.ArtworkRefresh"]);
        StartArtworkUpdate();
    }

    private void StartArtworkUpdate()
    {
        var apiKey = App.Settings.SteamGridDbApiKey;
        _ = Task.Run(() => App.Artwork.UpdateMissingAsync(apiKey));
    }

    private static string OnOff(bool value) => App.Strings[value ? "Settings.On" : "Settings.Off"];
}
