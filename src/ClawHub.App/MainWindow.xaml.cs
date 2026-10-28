using System.Globalization;
using System.Reflection;
using ClawHub.App.Input;
using ClawHub.App.ViewModels;
using ClawHub.App.Views;
using ClawHub.Core.Models;
using ClawHub.Core.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using VirtualKey = Windows.System.VirtualKey;

namespace ClawHub.App;

public sealed partial class MainWindow : Window
{
    private enum Tab { Library, Settings }
    private enum Zone { Sidebar, Grid }
    private enum SettingsItem { Rescan, Language, FullScreen, Exit }

    private const int MaxColumns = 5;
    private const double CardWidth = 150;
    private const double CardHeight = 260;
    private const double CardSpacing = 26;
    private static readonly string[] LanguageCycle = ["system", "it", "en"];

    private readonly GamepadInput _gamepad;
    private readonly DispatcherQueueTimer _clock;
    private readonly DispatcherQueueTimer _refreshDebounce;
    private readonly DispatcherQueueTimer _toastTimer;
    private readonly List<Border> _sidebarRows = [];
    private readonly List<Border> _settingsRows = [];

    private IReadOnlyList<LibrarySection> _sections = [];
    private List<GameTile> _tiles = [];
    private Tab _tab = Tab.Library;
    private Zone _zone = Zone.Grid;
    private int _sectionIndex;
    private int _gameIndex;
    private int _settingsIndex;
    private bool _scanning;
    private string? _backdropPath;
    private bool _backdropOnA;

    public MainWindow()
    {
        InitializeComponent();
        Title = App.Strings["App.Title"];
        AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);

        BackdropA.OpacityTransition = new ScalarTransition { Duration = TimeSpan.FromMilliseconds(500) };
        BackdropB.OpacityTransition = new ScalarTransition { Duration = TimeSpan.FromMilliseconds(500) };
        Toast.OpacityTransition = new ScalarTransition { Duration = TimeSpan.FromMilliseconds(200) };

        _gamepad = new GamepadInput(DispatcherQueue);
        _gamepad.Pressed += Handle;
        Activated += OnActivated;
        Root.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnKeyDown), true);
        Root.Loaded += (_, _) => KeySink.Focus(FocusState.Programmatic);
        GamesRepeater.ElementPrepared += (_, e) =>
            (e.Element as GameCard)?.SetHighlight(e.Index == _gameIndex, IsGridActive);
        GamesRepeater.Tapped += OnGridTapped;

        _clock = CreateTimer(TimeSpan.FromSeconds(15), repeating: true, UpdateClock);
        _clock.Start();
        _refreshDebounce = CreateTimer(TimeSpan.FromMilliseconds(250), repeating: false, Refresh);
        _toastTimer = CreateTimer(TimeSpan.FromMilliseconds(2200), repeating: false, () => Toast.Opacity = 0);

        App.Strings.LanguageChanged += (_, _) => ApplyStrings();
        App.Library.Changed += (_, _) => DispatcherQueue.TryEnqueue(() =>
        {
            _refreshDebounce.Stop();
            _refreshDebounce.Start();
        });

        ApplyStrings();
        UpdateClock();
        _ = LoadAsync();
    }

    private bool IsGridActive => _tab == Tab.Library && _zone == Zone.Grid;

    private GameTile? CurrentTile => _gameIndex < _tiles.Count ? _tiles[_gameIndex] : null;

    // ----- Dati -----

    private async Task LoadAsync()
    {
        // Prima la libreria salvata (istantanea), poi la scansione vera degli store.
        await App.Library.LoadAsync();
        await RescanAsync(announce: false);
    }

    private async Task RescanAsync(bool announce)
    {
        if (_scanning)
            return;

        _scanning = true;
        StatusText.Text = App.Strings["Library.Scanning"];
        RefreshSettings();
        try
        {
            var report = await App.Library.ScanAsync(App.Settings.DisabledStores);
            var message = report.Errors.Count == 0
                ? App.Strings.Format("Library.ScanDone", report.GameCount)
                : App.Strings.Format("Library.ScanErrors", string.Join(", ", report.Errors.Select(e => App.Strings[$"Store.{e.Store}"])));
            StatusText.Text = report.Errors.Count == 0 ? string.Empty : message;
            if (announce)
                ShowToast(message);
        }
        finally
        {
            _scanning = false;
            RefreshSettings();
        }

        // Copertine e sfondi mancanti, in background: compaiono man mano che arrivano.
        var apiKey = App.Settings.SteamGridDbApiKey;
        _ = Task.Run(() => App.Artwork.UpdateMissingAsync(apiKey));
    }

    private void Refresh()
    {
        var selectedSection = _sectionIndex < _sections.Count ? _sections[_sectionIndex].Key : "all";
        var selectedGame = CurrentTile?.Game.Id;

        _sections = LibrarySections.Build(App.Library.Games, App.Library.Collections, showHiddenEverywhere: App.Settings.ShowHiddenGames);
        _sectionIndex = Math.Max(0, IndexOf(_sections, s => s.Key == selectedSection));
        BuildSidebar();
        ShowSection(selectedGame);
        RefreshDetail();
        RefreshCollectionsPanel();
    }

    /// <summary>Mostra la sezione selezionata, mantenendo se possibile il gioco <paramref name="keepGameId"/>.</summary>
    private void ShowSection(string? keepGameId)
    {
        if (_sections.Count == 0)
            return;

        _tiles = _sections[_sectionIndex].Games.Select(g => new GameTile(g)).ToList();
        var kept = keepGameId is null ? -1 : _tiles.FindIndex(t => t.Game.Id == keepGameId);
        _gameIndex = kept >= 0 ? kept : Math.Clamp(_gameIndex, 0, Math.Max(0, _tiles.Count - 1));
        if (_tiles.Count == 0)
            _zone = Zone.Sidebar;

        GamesRepeater.ItemsSource = _tiles;
        EmptyText.Visibility = _tiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateSidebarHighlight();
        UpdateGridHighlight(scroll: true);
        UpdateHeader();
    }

    // ----- Comandi -----

    private void Handle(PadButton button)
    {
        // I pannelli aperti sopra la libreria hanno la precedenza; LB/RB cambiano sempre scheda.
        if (HandleCollectionsPanel(button))
            return;
        if (button is not (PadButton.LeftShoulder or PadButton.RightShoulder or PadButton.Menu) && HandleDetail(button))
            return;

        switch (button)
        {
            case PadButton.Up: Move(0, -1); break;
            case PadButton.Down: Move(0, 1); break;
            case PadButton.Left: Move(-1, 0); break;
            case PadButton.Right: Move(1, 0); break;
            case PadButton.LeftShoulder:
            case PadButton.RightShoulder:
                SwitchTab(_tab == Tab.Library ? Tab.Settings : Tab.Library);
                break;
            case PadButton.Menu:
                SwitchTab(_tab == Tab.Settings ? Tab.Library : Tab.Settings);
                break;
            case PadButton.B:
                if (_tab == Tab.Settings)
                    SwitchTab(Tab.Library);
                else if (_zone == Zone.Grid)
                    SetZone(Zone.Sidebar);
                break;
            case PadButton.A:
                if (_tab == Tab.Settings)
                    _ = ActivateSettingAsync((SettingsItem)_settingsIndex);
                else if (_zone == Zone.Sidebar)
                    SetZone(Zone.Grid);
                else if (CurrentTile is not null)
                    OpenDetail();
                break;
            case PadButton.X:
                if (_tab == Tab.Library && CurrentTile is { } toLaunch)
                    _ = LaunchAsync(toLaunch);
                break;
            case PadButton.Y:
                if (_tab == Tab.Library && CurrentTile is { } toToggle)
                    _ = ToggleFavoriteAsync(toToggle);
                break;
        }
    }

    private void Move(int dx, int dy)
    {
        if (_tab == Tab.Settings)
        {
            _settingsIndex = Math.Clamp(_settingsIndex + dy, 0, _settingsRows.Count - 1);
            UpdateSettingsHighlight();
            return;
        }

        if (_zone == Zone.Sidebar)
        {
            if (dx > 0)
            {
                SetZone(Zone.Grid);
            }
            else if (dy != 0)
            {
                var next = Math.Clamp(_sectionIndex + dy, 0, _sections.Count - 1);
                if (next != _sectionIndex)
                {
                    _sectionIndex = next;
                    _gameIndex = 0;
                    ShowSection(null);
                }
            }
            return;
        }

        var columns = VisibleColumns();
        var row = _gameIndex / columns;
        if (dx < 0 && _gameIndex % columns == 0)
        {
            SetZone(Zone.Sidebar);
            return;
        }

        var target = _gameIndex + dx + dy * columns;
        if (dx != 0 && (target < 0 || target >= _tiles.Count || target / columns != row))
            return;
        if (dy > 0 && target >= _tiles.Count)
            target = row < (_tiles.Count - 1) / columns ? _tiles.Count - 1 : -1; // ultima riga incompleta
        if (target < 0 || target >= _tiles.Count)
            return;

        _gameIndex = target;
        UpdateGridHighlight(scroll: true);
        UpdateHeader();
    }

    private void SetZone(Zone zone)
    {
        if (zone == Zone.Grid && _tiles.Count == 0)
            return;
        _zone = zone;
        UpdateSidebarHighlight();
        UpdateGridHighlight(scroll: false);
    }

    private void SwitchTab(Tab tab)
    {
        _tab = tab;
        LibraryPanel.Visibility = tab == Tab.Library && !_detailOpen ? Visibility.Visible : Visibility.Collapsed;
        DetailPanel.Visibility = tab == Tab.Library && _detailOpen ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = tab == Tab.Settings ? Visibility.Visible : Visibility.Collapsed;
        BuildTabs();
        BuildLegend();
        RefreshSettings();
        UpdateSidebarHighlight();
        UpdateGridHighlight(scroll: false);
    }

    private async Task LaunchAsync(GameTile tile)
    {
        try
        {
            ShowToast(App.Strings.Format("Toast.Launching", tile.Name));
            await App.Launcher.LaunchAsync(tile.Game.Source.Launch);
            await App.Library.RecordLaunchAsync(tile.Game.Id);
        }
        catch (Exception ex)
        {
            ShowToast(App.Strings.Format("Game.LaunchFailed", ex.Message));
        }
    }

    private async Task ToggleFavoriteAsync(GameTile tile)
    {
        var favorite = !tile.IsFavorite;
        await App.Library.SetFavoriteAsync(tile.Game.Id, favorite);
        ShowToast(App.Strings.Format(favorite ? "Toast.FavoriteAdded" : "Toast.FavoriteRemoved", tile.Name));
    }

    private async Task ActivateSettingAsync(SettingsItem item)
    {
        switch (item)
        {
            case SettingsItem.Rescan:
                await RescanAsync(announce: true);
                break;
            case SettingsItem.Language:
                var current = Array.IndexOf(LanguageCycle, App.Settings.Language);
                var next = LanguageCycle[(current + 1) % LanguageCycle.Length];
                App.Settings = App.Settings with { Language = next };
                await App.SettingsStore.SaveAsync(App.Settings);
                App.Strings.SetLanguage(next);
                RefreshSettings(); // Se la lingua effettiva non cambia (es. sistema = italiano) LanguageChanged non scatta.
                break;
            case SettingsItem.FullScreen:
                ToggleFullScreen();
                break;
            case SettingsItem.Exit:
                Close();
                break;
        }
    }

    private void ToggleFullScreen()
    {
        var isFullScreen = AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;
        AppWindow.SetPresenter(isFullScreen ? AppWindowPresenterKind.Overlapped : AppWindowPresenterKind.FullScreen);
        if (isFullScreen && AppWindow.Presenter is OverlappedPresenter overlapped)
            overlapped.Maximize();
        RefreshSettings();
    }

    // ----- Input -----

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        var active = args.WindowActivationState != WindowActivationState.Deactivated;
        _gamepad.Enabled = active;
        if (active)
            KeySink.Focus(FocusState.Programmatic);
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.F11)
        {
            ToggleFullScreen();
            e.Handled = true;
            return;
        }

        if (_namingCollection)
        {
            // Mentre si scrive il nome di una collezione i tasti vanno alla casella di testo.
            if (e.Key is VirtualKey.Enter or VirtualKey.Escape)
            {
                e.Handled = true;
                Handle(e.Key == VirtualKey.Enter ? PadButton.A : PadButton.B);
            }
            return;
        }

        // I tasti del controller (VirtualKey.Gamepad*) non sono qui: li legge GamepadInput.
        PadButton? button = e.Key switch
        {
            VirtualKey.Up => PadButton.Up,
            VirtualKey.Down => PadButton.Down,
            VirtualKey.Left => PadButton.Left,
            VirtualKey.Right => PadButton.Right,
            VirtualKey.Enter or VirtualKey.Space => PadButton.A,
            VirtualKey.Escape or VirtualKey.Back => PadButton.B,
            VirtualKey.X => PadButton.X,
            VirtualKey.Y => PadButton.Y,
            VirtualKey.Q or VirtualKey.PageUp => PadButton.LeftShoulder,
            VirtualKey.E or VirtualKey.PageDown => PadButton.RightShoulder,
            _ => null,
        };
        if (button is null)
            return;

        e.Handled = true;
        Handle(button.Value);
    }

    private void OnGridTapped(object sender, TappedRoutedEventArgs e)
    {
        var card = FindAncestor<GameCard>(e.OriginalSource as DependencyObject);
        var index = card is null ? -1 : GamesRepeater.GetElementIndex(card);
        if (index < 0)
            return;

        if (_zone == Zone.Grid && index == _gameIndex)
        {
            Handle(PadButton.A);
        }
        else
        {
            _zone = Zone.Grid;
            _gameIndex = index;
            UpdateSidebarHighlight();
            UpdateGridHighlight(scroll: false);
            UpdateHeader();
        }
        KeySink.Focus(FocusState.Programmatic);
    }

    // ----- Aspetto -----

    private void ApplyStrings()
    {
        Title = App.Strings["App.Title"];
        SettingsTitle.Text = App.Strings["Nav.Settings"].ToUpper(CultureInfo.CurrentCulture);
        SettingsNote.Text = App.Strings["Settings.ComingSoon"];
        VersionText.Text = App.Strings.Format("Settings.Version", AppVersion());
        EmptyText.Text = App.Strings["Library.Empty"];
        BuildTabs();
        BuildLegend();
        BuildActions();
        BuildSidebar();
        UpdateHeader();
        RefreshSettings();
        RefreshDetail();
        RefreshCollectionsPanel();
    }

    private void UpdateHeader()
    {
        if (_sections.Count == 0)
            return;

        var section = _sections[_sectionIndex];
        SectionLabel.Text = $"{SectionName(section).ToUpper(CultureInfo.CurrentCulture)} · {section.Games.Count}";

        if (CurrentTile is not { } tile)
        {
            GameTitle.Text = App.Strings["Library.Empty"].ToUpper(CultureInfo.CurrentCulture);
            GameMeta.Text = string.Empty;
            GameActions.Visibility = Visibility.Collapsed;
            SetBackdrop(null);
            return;
        }

        GameTitle.Text = tile.Name.ToUpper(CultureInfo.CurrentCulture);
        GameMeta.Text = $"{App.Strings[$"Store.{tile.Game.Store}"]}    ·    {LastPlayedText(tile.Game)}";
        GameActions.Visibility = Visibility.Visible;
        SetBackdrop(tile.BackdropPath);
    }

    private void SetBackdrop(string? path)
    {
        if (path == _backdropPath)
            return;
        _backdropPath = path;

        var (show, hide) = _backdropOnA ? (BackdropB, BackdropA) : (BackdropA, BackdropB);
        show.Source = ImageCache.Get(path, 1600);
        show.Opacity = path is null ? 0 : 0.9;
        hide.Opacity = 0;
        _backdropOnA = !_backdropOnA;
    }

    private void UpdateGridHighlight(bool scroll)
    {
        for (var i = 0; i < _tiles.Count; i++)
            (GamesRepeater.TryGetElement(i) as GameCard)?.SetHighlight(i == _gameIndex, IsGridActive);

        if (scroll && _tiles.Count > 0)
        {
            // La riga selezionata resta la seconda dall'alto, così si vede anche cosa c'è sopra.
            var row = _gameIndex / VisibleColumns();
            GridScroll.ChangeView(null, Math.Max(0, (row - 1) * (CardHeight + CardSpacing)), null);
        }
    }

    private int VisibleColumns()
    {
        var width = GamesRepeater.ActualWidth;
        return width <= 0 ? MaxColumns : Math.Clamp((int)((width + CardSpacing) / (CardWidth + CardSpacing)), 1, MaxColumns);
    }

    private void BuildSidebar()
    {
        SidebarItems.Children.Clear();
        _sidebarRows.Clear();

        string? group = null;
        for (var i = 0; i < _sections.Count; i++)
        {
            var section = _sections[i];
            var sectionGroup = section.Kind switch
            {
                LibrarySectionKind.Collection => "Section.Group.Collections",
                LibrarySectionKind.Store => "Section.Group.Stores",
                _ => "Section.Group.Library",
            };
            if (sectionGroup != group)
            {
                group = sectionGroup;
                SidebarItems.Children.Add(new TextBlock
                {
                    Text = App.Strings[sectionGroup].ToUpper(CultureInfo.CurrentCulture),
                    FontFamily = Res<FontFamily>("DisplayFont"),
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    FontSize = 11,
                    CharacterSpacing = 160,
                    Foreground = Res<Brush>("FaintBrush"),
                    Margin = new Thickness(12, SidebarItems.Children.Count == 0 ? 0 : 14, 0, 6),
                });
            }

            var index = i;
            var row = ListRow(SectionName(section), section.Games.Count.ToString(CultureInfo.CurrentCulture), 16);
            row.Tapped += (_, _) =>
            {
                _zone = Zone.Sidebar;
                if (index != _sectionIndex)
                {
                    _sectionIndex = index;
                    _gameIndex = 0;
                    ShowSection(null);
                }
                else
                {
                    UpdateSidebarHighlight();
                    UpdateGridHighlight(scroll: false);
                }
                KeySink.Focus(FocusState.Programmatic);
            };
            _sidebarRows.Add(row);
            SidebarItems.Children.Add(row);
        }
        UpdateSidebarHighlight();
    }

    private void UpdateSidebarHighlight()
    {
        for (var i = 0; i < _sidebarRows.Count; i++)
        {
            var selected = i == _sectionIndex;
            SetRowState(_sidebarRows[i], selected, focused: selected && _tab == Tab.Library && _zone == Zone.Sidebar);
        }
        if (_sectionIndex < _sidebarRows.Count)
            _sidebarRows[_sectionIndex].StartBringIntoView();
    }

    private void RefreshSettings()
    {
        SettingsItems.Children.Clear();
        _settingsRows.Clear();

        var language = App.Settings.Language == "system" ? "Settings.Language.System" : $"Settings.Language.{App.Settings.Language}";
        var fullScreen = AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;
        (string Title, string Value)[] items =
        [
            (App.Strings["Library.Rescan"], _scanning
                ? App.Strings["Library.Scanning"]
                : App.Strings.Format("Library.ScanDone", App.Library.Games.Count)),
            (App.Strings["Settings.Language"], App.Strings[language]),
            (App.Strings["Settings.Fullscreen"], App.Strings[fullScreen ? "Settings.On" : "Settings.Off"]),
            (App.Strings["Settings.Exit"], string.Empty),
        ];

        for (var i = 0; i < items.Length; i++)
        {
            var index = i;
            var row = ListRow(items[i].Title, items[i].Value, 18);
            row.Padding = new Thickness(18, 14, 18, 14);
            row.Tapped += (_, _) =>
            {
                _settingsIndex = index;
                UpdateSettingsHighlight();
                _ = ActivateSettingAsync((SettingsItem)index);
                KeySink.Focus(FocusState.Programmatic);
            };
            _settingsRows.Add(row);
            SettingsItems.Children.Add(row);
        }
        UpdateSettingsHighlight();
    }

    private void UpdateSettingsHighlight()
    {
        for (var i = 0; i < _settingsRows.Count; i++)
            SetRowState(_settingsRows[i], i == _settingsIndex, focused: i == _settingsIndex && _tab == Tab.Settings);
    }

    private void BuildTabs()
    {
        TabsPanel.Children.Clear();
        TabsPanel.Children.Add(Badge("LB"));
        foreach (var tab in new[] { Tab.Library, Tab.Settings })
        {
            var active = tab == _tab;
            var label = new Border
            {
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(14, 8, 14, 8),
                Background = active ? ActiveTabBackground() : null,
                Child = new TextBlock
                {
                    Text = App.Strings[tab == Tab.Library ? "Nav.Library" : "Nav.Settings"],
                    FontSize = 16,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = Res<Brush>(active ? "TextBrush" : "MutedBrush"),
                },
            };
            label.Tapped += (_, _) =>
            {
                SwitchTab(tab);
                KeySink.Focus(FocusState.Programmatic);
            };
            TabsPanel.Children.Add(label);
        }
        TabsPanel.Children.Add(Badge("RB"));
    }

    /// <summary>
    /// Fondo della scheda attiva con una fascia ciano in basso. Un bordo solo inferiore su una pillola
    /// verrebbe disegnato come un arco staccato; lo sfondo invece segue gli angoli arrotondati.
    /// </summary>
    private static LinearGradientBrush ActiveTabBackground()
    {
        var fill = ((SolidColorBrush)Res<Brush>("SelectedBrush")).Color;
        var accent = (Windows.UI.Color)Application.Current.Resources["AccentColor"];
        var brush = new LinearGradientBrush { StartPoint = new Windows.Foundation.Point(0, 0), EndPoint = new Windows.Foundation.Point(0, 1) };
        brush.GradientStops.Add(new GradientStop { Color = fill, Offset = 0 });
        brush.GradientStops.Add(new GradientStop { Color = fill, Offset = 0.94 });
        brush.GradientStops.Add(new GradientStop { Color = accent, Offset = 0.94 });
        brush.GradientStops.Add(new GradientStop { Color = accent, Offset = 1 });
        return brush;
    }

    private void BuildLegend()
    {
        LegendPanel.Children.Clear();
        if (_collectionsOpen)
        {
            LegendPanel.Children.Add(LegendItem("Hint.Select", Glyph("A")));
            LegendPanel.Children.Add(LegendItem("Hint.Close", Glyph("B")));
            return;
        }
        if (_tab == Tab.Library && _detailOpen)
        {
            LegendPanel.Children.Add(LegendItem("Hint.Confirm", Glyph("A")));
            LegendPanel.Children.Add(LegendItem("Hint.Play", Glyph("X")));
            LegendPanel.Children.Add(LegendItem("Hint.Favorite", Glyph("Y")));
        }
        else if (_tab == Tab.Library)
        {
            LegendPanel.Children.Add(LegendItem("Hint.Details", Glyph("A")));
            LegendPanel.Children.Add(LegendItem("Hint.Play", Glyph("X")));
            LegendPanel.Children.Add(LegendItem("Hint.Favorite", Glyph("Y")));
        }
        else
        {
            LegendPanel.Children.Add(LegendItem("Hint.Select", Glyph("A")));
        }
        LegendPanel.Children.Add(LegendItem("Hint.Back", Glyph("B")));
        LegendPanel.Children.Add(LegendItem("Hint.Tabs", ShoulderGlyph("LB"), ShoulderGlyph("RB")));
    }

    private void BuildActions()
    {
        GameActions.Children.Clear();

        var play = ActionButton(Glyph("X"), App.Strings["Hint.Play"], primary: true);
        play.Tapped += (_, _) =>
        {
            if (CurrentTile is { } tile)
                _ = LaunchAsync(tile);
            KeySink.Focus(FocusState.Programmatic);
        };
        var details = ActionButton(Glyph("A"), App.Strings["Hint.Details"], primary: false);
        details.Tapped += (_, _) =>
        {
            if (CurrentTile is not null)
                OpenDetail();
            KeySink.Focus(FocusState.Programmatic);
        };

        GameActions.Children.Add(play);
        GameActions.Children.Add(details);
    }

    private void UpdateClock()
    {
        ClockText.Text = DateTime.Now.ToString("HH:mm", CultureInfo.CurrentCulture);
        try
        {
            if (Microsoft.Windows.System.Power.PowerManager.BatteryStatus == Microsoft.Windows.System.Power.BatteryStatus.NotPresent)
            {
                BatteryPanel.Visibility = Visibility.Collapsed;
                return;
            }
            var percent = Math.Clamp(Microsoft.Windows.System.Power.PowerManager.RemainingChargePercent, 0, 100);
            BatteryLevel.Width = Math.Max(2, 20 * percent / 100.0);
            BatteryText.Text = $"{percent}%";
            BatteryPanel.Visibility = Visibility.Visible;
        }
        catch (Exception)
        {
            BatteryPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowToast(string message)
    {
        ToastText.Text = message;
        Toast.Opacity = 1;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    // ----- Elementi riutilizzati -----

    private static Border ListRow(string title, string value, double fontSize)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = fontSize,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var valueText = new TextBlock { Text = value, FontSize = fontSize - 3, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(valueText, 1);
        grid.Children.Add(valueText);

        return new Border { CornerRadius = new CornerRadius(9), Padding = new Thickness(12, 9, 12, 9), Child = grid };
    }

    private static void SetRowState(Border row, bool selected, bool focused)
    {
        row.Background = focused ? Res<Brush>("AccentBrush") : selected ? Res<Brush>("SelectedBrush") : null;
        var grid = (Grid)row.Child;
        ((TextBlock)grid.Children[0]).Foreground = Res<Brush>(focused ? "AccentInkBrush" : selected ? "TextBrush" : "MutedBrush");
        ((TextBlock)grid.Children[1]).Foreground = Res<Brush>(focused ? "AccentInkBrush" : "FaintBrush");
    }

    private static Border ActionButton(UIElement glyph, string text, bool primary)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        content.Children.Add(glyph);
        content.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 15,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Res<Brush>(primary ? "AccentInkBrush" : "TextBrush"),
        });
        return new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(9, 8, 18, 8),
            Background = primary ? Res<Brush>("AccentBrush") : Res<Brush>("SelectedBrush"),
            Child = content,
        };
    }

    private static StackPanel LegendItem(string key, params UIElement[] glyphs)
    {
        var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var glyph in glyphs)
            item.Children.Add(glyph);
        item.Children.Add(new TextBlock
        {
            Text = App.Strings[key],
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Res<Brush>("MutedBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        return item;
    }

    /// <summary>Tasto frontale: cerchio colorato con la lettera (colori generici, non le icone di Xbox).</summary>
    private static Border Glyph(string letter)
    {
        var color = letter switch
        {
            "A" => Microsoft.UI.ColorHelper.FromArgb(255, 0x2F, 0x9E, 0x44),
            "B" => Microsoft.UI.ColorHelper.FromArgb(255, 0xE0, 0x31, 0x31),
            "X" => Microsoft.UI.ColorHelper.FromArgb(255, 0x1C, 0x7E, 0xD6),
            _ => Microsoft.UI.ColorHelper.FromArgb(255, 0xF0, 0x8C, 0x00),
        };
        return new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(color),
            Child = GlyphText(letter),
        };
    }

    private static Border ShoulderGlyph(string text) => new()
    {
        Height = 22,
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(6, 0, 6, 0),
        Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0x3B, 0x42, 0x56)),
        Child = GlyphText(text),
    };

    private static TextBlock GlyphText(string text) => new()
    {
        Text = text,
        FontFamily = Res<FontFamily>("DisplayFont"),
        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
        FontSize = 11,
        Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static Border Badge(string text) => new()
    {
        CornerRadius = new CornerRadius(6),
        BorderThickness = new Thickness(1),
        BorderBrush = Res<Brush>("LineBrush"),
        Padding = new Thickness(7, 3, 7, 3),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock
        {
            Text = text,
            FontFamily = Res<FontFamily>("DisplayFont"),
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            FontSize = 11,
            Foreground = Res<Brush>("MutedBrush"),
        },
    };

    // ----- Utilità -----

    private static string SectionName(LibrarySection section) => section.Kind switch
    {
        LibrarySectionKind.All => App.Strings["Library.AllGames"],
        LibrarySectionKind.Recent => App.Strings["Section.Recent"],
        LibrarySectionKind.Favorites => App.Strings["Library.Favorites"],
        LibrarySectionKind.Hidden => App.Strings["Section.Hidden"],
        LibrarySectionKind.Collection => section.Collection?.Name ?? string.Empty,
        _ => App.Strings[$"Store.{section.Store}"],
    };

    private static string LastPlayedText(Game game)
    {
        if (game.LastPlayed is not { } when)
            return App.Strings["Game.NeverPlayed"];
        var relative = RelativeTime.Describe(when, DateTimeOffset.Now);
        return App.Strings.Format("Game.LastPlayedShort", App.Strings.Format(relative.Key, relative.Value));
    }

    private static string AppVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";
        return version.Split('+')[0]; // .NET aggiunge "+<commit>".
    }

    private static T Res<T>(string key) => (T)Application.Current.Resources[key];

    private static int IndexOf<T>(IReadOnlyList<T> items, Func<T, bool> match)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (match(items[i]))
                return i;
        }
        return -1;
    }

    private static T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element is not null and not T)
            element = VisualTreeHelper.GetParent(element);
        return element as T;
    }

    private DispatcherQueueTimer CreateTimer(TimeSpan interval, bool repeating, Action tick)
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = interval;
        timer.IsRepeating = repeating;
        timer.Tick += (_, _) => tick();
        return timer;
    }
}
