using System.Globalization;
using ClawHub.App.Input;
using ClawHub.App.ViewModels;
using ClawHub.Core.Models;
using ClawHub.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace ClawHub.App;

// Pagina di dettaglio del gioco e pannello delle collezioni.
public sealed partial class MainWindow
{
    private enum DetailAction { Play, Favorite, Collections, Hide }

    private static readonly DetailAction[] DetailActionsOrder = Enum.GetValues<DetailAction>();

    /// <summary>Immagini che non si sono caricate (es. 404 dalla CDN): non vanno riprovate a ogni apertura.</summary>
    private static readonly HashSet<string> FailedImages = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<Border> _detailButtons = [];
    private readonly List<Border> _collectionRows = [];
    private bool _detailOpen;
    private string? _detailGameId;
    private int _detailButton;
    private string? _detailHeroPath;
    private bool _collectionsOpen;
    private bool _namingCollection;
    private int _collectionIndex;
    private bool _detailImagesHooked;

    private Game? DetailGame => _detailGameId is null ? null : App.Library.Find(_detailGameId);

    // ----- Dettaglio -----

    private void OpenDetail()
    {
        if (CurrentTile is not { } tile)
            return;

        HookDetailImages();
        _detailOpen = true;
        _detailGameId = tile.Game.Id;
        _detailButton = 0;
        _detailHeroPath = null;
        LibraryPanel.Visibility = Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Visible;
        RefreshDetail();
        BuildLegend();
    }

    private void CloseDetail()
    {
        _detailOpen = false;
        _detailGameId = null;
        DetailPanel.Visibility = Visibility.Collapsed;
        LibraryPanel.Visibility = _tab == Tab.Library ? Visibility.Visible : Visibility.Collapsed;
        BuildLegend();
        UpdateGridHighlight(scroll: true);
        UpdateHeader();
    }

    /// <summary>Gestisce i tasti quando il dettaglio è aperto. Restituisce false se non è aperto.</summary>
    private bool HandleDetail(PadButton button)
    {
        if (!_detailOpen || _tab != Tab.Library || DetailGame is not { } game)
            return false;

        switch (button)
        {
            case PadButton.Left:
            case PadButton.Right:
                _detailButton = Math.Clamp(_detailButton + (button == PadButton.Right ? 1 : -1), 0, _detailButtons.Count - 1);
                UpdateDetailButtons();
                break;
            case PadButton.A:
                _ = RunDetailActionAsync(DetailActionsOrder[_detailButton], game);
                break;
            case PadButton.X:
                _ = RunDetailActionAsync(DetailAction.Play, game);
                break;
            case PadButton.Y:
                _ = RunDetailActionAsync(DetailAction.Favorite, game);
                break;
            case PadButton.B:
                CloseDetail();
                break;
        }
        return true;
    }

    private async Task RunDetailActionAsync(DetailAction action, Game game)
    {
        var tile = new GameTile(game);
        switch (action)
        {
            case DetailAction.Play:
                await LaunchAsync(tile);
                break;
            case DetailAction.Favorite:
                await ToggleFavoriteAsync(tile);
                break;
            case DetailAction.Collections:
                OpenCollectionsPanel();
                break;
            case DetailAction.Hide:
                var hide = !game.IsHidden;
                await App.Library.SetHiddenAsync(game.Id, hide);
                ShowToast(App.Strings.Format(hide ? "Toast.Hidden" : "Toast.Unhidden", game.Name));
                // Il gioco cambia sezione (entra o esce da "Nascosti"): si torna alla griglia.
                CloseDetail();
                break;
        }
    }

    /// <summary>Ridisegna il dettaglio con i dati aggiornati del gioco (preferito, collezioni, immagini).</summary>
    private void RefreshDetail()
    {
        if (!_detailOpen)
            return;
        if (DetailGame is not { } game)
        {
            CloseDetail();
            return;
        }

        var tile = new GameTile(game);

        // Immagine grande: quella orizzontale, altrimenti la copertina.
        var hero = Usable(tile.HeroPath) ?? Usable(tile.CoverPath);
        if (hero != _detailHeroPath)
        {
            _detailHeroPath = hero;
            DetailHeroBrush.ImageSource = ImageCache.Get(hero, 1600);
        }
        SetBackdrop(tile.BackdropPath);

        // Logo del gioco, altrimenti il titolo scritto.
        var logo = Usable(tile.LogoPath);
        DetailLogo.Source = ImageCache.Get(logo, 800);
        DetailLogo.Visibility = logo is null ? Visibility.Collapsed : Visibility.Visible;
        DetailTitle.Visibility = logo is null ? Visibility.Visible : Visibility.Collapsed;
        DetailTitle.Text = game.Name.ToUpper(CultureInfo.CurrentCulture);

        BuildDetailButtons(game);
        BuildDetailInfo(game);
    }

    private void BuildDetailButtons(Game game)
    {
        DetailActions.Children.Clear();
        _detailButtons.Clear();

        foreach (var action in DetailActionsOrder)
        {
            UIElement? glyph = action switch
            {
                DetailAction.Play => Glyph("X"),
                DetailAction.Favorite => Glyph("Y"),
                _ => null,
            };
            var text = action switch
            {
                DetailAction.Play => App.Strings["Game.Play"],
                DetailAction.Favorite => App.Strings[game.IsFavorite ? "Game.RemoveFavorite" : "Game.AddFavorite"],
                DetailAction.Collections => App.Strings["Detail.Collections"],
                _ => App.Strings[game.IsHidden ? "Game.Unhide" : "Game.Hide"],
            };

            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            if (glyph is not null)
                content.Children.Add(glyph);
            content.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Res<Brush>(action == DetailAction.Play ? "AccentInkBrush" : "TextBrush"),
            });

            var button = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(glyph is null ? 24 : 14, 12, 24, 12),
                BorderThickness = new Thickness(3),
                Background = Res<Brush>(action == DetailAction.Play ? "AccentBrush" : "SelectedBrush"),
                Child = content,
                CenterPoint = new System.Numerics.Vector3(90, 26, 0),
                ScaleTransition = new Vector3Transition { Duration = TimeSpan.FromMilliseconds(140) },
            };
            var index = _detailButtons.Count;
            button.Tapped += (_, _) =>
            {
                _detailButton = index;
                UpdateDetailButtons();
                if (DetailGame is { } current)
                    _ = RunDetailActionAsync(action, current);
                KeySink.Focus(FocusState.Programmatic);
            };
            _detailButtons.Add(button);
            DetailActions.Children.Add(button);
        }

        _detailButton = Math.Clamp(_detailButton, 0, _detailButtons.Count - 1);
        UpdateDetailButtons();
    }

    private void UpdateDetailButtons()
    {
        for (var i = 0; i < _detailButtons.Count; i++)
        {
            var focused = i == _detailButton && !_collectionsOpen;
            _detailButtons[i].BorderBrush = focused ? Res<Brush>("TextBrush") : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            _detailButtons[i].Scale = focused ? new System.Numerics.Vector3(1.04f, 1.04f, 1f) : System.Numerics.Vector3.One;
        }
    }

    private void BuildDetailInfo(Game game)
    {
        DetailInfo.Children.Clear();

        var lastPlayed = game.LastPlayed is { } when
            ? RelativeText(when)
            : App.Strings["Game.NeverPlayed"];
        var launches = game.UserData.PlayCount switch
        {
            0 => App.Strings["Detail.NeverLaunched"],
            1 => App.Strings["Detail.LaunchOnce"],
            var count => App.Strings.Format("Detail.LaunchCount", count),
        };
        var collections = App.Library.Collections.Where(c => game.CollectionIds.Contains(c.Id)).Select(c => c.Name).ToList();

        AddInfo(0, 0, "Detail.Store", App.Strings[$"Store.{game.Store}"]);
        AddInfo(0, 1, "Detail.LastPlayed", lastPlayed);
        AddInfo(1, 0, "Detail.Launches", launches);
        AddInfo(1, 1, "Detail.Collections", collections.Count == 0 ? App.Strings["Detail.NoCollections"] : string.Join(", ", collections));
        AddInfo(2, 0, "Detail.Folder", game.Source.InstallDirectory ?? App.Strings["Detail.Unknown"], monospace: true, span: 2);
    }

    private void AddInfo(int row, int column, string labelKey, string value, bool monospace = false, int span = 1)
    {
        var item = new StackPanel { Spacing = 4 };
        item.Children.Add(new TextBlock
        {
            Text = App.Strings[labelKey].ToUpper(CultureInfo.CurrentCulture),
            FontFamily = Res<FontFamily>("DisplayFont"),
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            FontSize = 11,
            CharacterSpacing = 140,
            Foreground = Res<Brush>("FaintBrush"),
        });
        item.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = monospace ? 14 : 17,
            FontFamily = monospace ? new FontFamily("Cascadia Mono, Consolas") : Res<FontFamily>("BodyFont"),
            FontWeight = monospace ? Microsoft.UI.Text.FontWeights.Normal : Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Res<Brush>(monospace ? "MutedBrush" : "TextBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        Grid.SetRow(item, row);
        Grid.SetColumn(item, column);
        Grid.SetColumnSpan(item, span);
        DetailInfo.Children.Add(item);
    }

    private void HookDetailImages()
    {
        if (_detailImagesHooked)
            return;
        _detailImagesHooked = true;

        DetailHeroBrush.ImageFailed += (_, _) =>
        {
            if (_detailHeroPath is null)
                return;
            FailedImages.Add(_detailHeroPath);
            _detailHeroPath = null;
            RefreshDetail(); // Riprova con la copertina.
        };
        DetailLogo.ImageFailed += (_, _) =>
        {
            if (DetailGame is { } game && new GameTile(game).LogoPath is { } logo)
                FailedImages.Add(logo);
            DetailLogo.Visibility = Visibility.Collapsed;
            DetailTitle.Visibility = Visibility.Visible;
        };
    }

    private static string? Usable(string? path) => path is null || FailedImages.Contains(path) ? null : path;

    private static string RelativeText(DateTimeOffset when)
    {
        var relative = RelativeTime.Describe(when, DateTimeOffset.Now);
        return App.Strings.Format(relative.Key, relative.Value);
    }

    // ----- Collezioni -----

    private void OpenCollectionsPanel()
    {
        _collectionsOpen = true;
        _collectionIndex = 0;
        CollectionsOverlay.Visibility = Visibility.Visible;
        RefreshCollectionsPanel();
        UpdateDetailButtons();
        BuildLegend();
    }

    private void CloseCollectionsPanel()
    {
        StopNaming();
        _collectionsOpen = false;
        CollectionsOverlay.Visibility = Visibility.Collapsed;
        UpdateDetailButtons();
        BuildLegend();
    }

    private bool HandleCollectionsPanel(PadButton button)
    {
        if (!_collectionsOpen)
            return false;

        if (_namingCollection)
        {
            if (button == PadButton.A)
                _ = CreateCollectionAsync();
            else if (button == PadButton.B)
                StopNaming();
            return true;
        }

        switch (button)
        {
            case PadButton.Up:
            case PadButton.Down:
                _collectionIndex = Math.Clamp(_collectionIndex + (button == PadButton.Down ? 1 : -1), 0, _collectionRows.Count - 1);
                UpdateCollectionRows();
                break;
            case PadButton.A:
                _ = ActivateCollectionRowAsync(_collectionIndex);
                break;
            case PadButton.B:
                CloseCollectionsPanel();
                break;
        }
        return true;
    }

    private void RefreshCollectionsPanel()
    {
        if (!_collectionsOpen)
            return;
        if (DetailGame is not { } game)
        {
            CloseCollectionsPanel();
            return;
        }

        CollectionsTitle.Text = App.Strings["Game.AddToCollection"].ToUpper(CultureInfo.CurrentCulture);
        CollectionsSubtitle.Text = game.Name;
        NewCollectionBox.PlaceholderText = App.Strings["Collection.NamePlaceholder"];

        CollectionsList.Children.Clear();
        _collectionRows.Clear();
        foreach (var collection in App.Library.Collections)
            AddCollectionRow(collection.Name, game.CollectionIds.Contains(collection.Id) ? "✓" : string.Empty);
        AddCollectionRow("+  " + App.Strings["Collection.New"], string.Empty);

        _collectionIndex = Math.Clamp(_collectionIndex, 0, _collectionRows.Count - 1);
        UpdateCollectionRows();
    }

    private void AddCollectionRow(string title, string value)
    {
        var row = ListRow(title, value, 17);
        row.Padding = new Thickness(16, 12, 16, 12);
        var index = _collectionRows.Count;
        row.Tapped += (_, _) =>
        {
            _collectionIndex = index;
            UpdateCollectionRows();
            _ = ActivateCollectionRowAsync(index);
        };
        _collectionRows.Add(row);
        CollectionsList.Children.Add(row);
    }

    private void UpdateCollectionRows()
    {
        for (var i = 0; i < _collectionRows.Count; i++)
            SetRowState(_collectionRows[i], i == _collectionIndex, focused: i == _collectionIndex && !_namingCollection);
        if (_collectionIndex < _collectionRows.Count)
            _collectionRows[_collectionIndex].StartBringIntoView();
    }

    private async Task ActivateCollectionRowAsync(int index)
    {
        if (DetailGame is not { } game)
            return;

        var collections = App.Library.Collections;
        if (index >= collections.Count)
        {
            StartNaming();
            return;
        }

        var collection = collections[index];
        var inside = game.CollectionIds.Contains(collection.Id);
        if (inside)
            await App.Library.RemoveFromCollectionAsync(game.Id, collection.Id);
        else
            await App.Library.AddToCollectionAsync(game.Id, collection.Id);
        ShowToast(App.Strings.Format(inside ? "Toast.RemovedFromCollection" : "Toast.AddedToCollection", game.Name, collection.Name));
        RefreshCollectionsPanel();
    }

    /// <summary>Mostra la casella per il nome. Sul Claw, senza tastiera fisica, Windows apre la tastiera touch.</summary>
    private void StartNaming()
    {
        _namingCollection = true;
        NewCollectionBox.Text = string.Empty;
        NewCollectionBox.Visibility = Visibility.Visible;
        UpdateCollectionRows();
        NewCollectionBox.Focus(FocusState.Programmatic);
    }

    private void StopNaming()
    {
        if (!_namingCollection)
            return;
        _namingCollection = false;
        NewCollectionBox.Visibility = Visibility.Collapsed;
        UpdateCollectionRows();
        KeySink.Focus(FocusState.Programmatic);
    }

    private async Task CreateCollectionAsync()
    {
        if (DetailGame is not { } game)
            return;

        var name = NewCollectionBox.Text.Trim();
        if (name.Length == 0)
            name = App.Strings.Format("Collection.DefaultName", App.Library.Collections.Count + 1);

        StopNaming();
        var collection = await App.Library.CreateCollectionAsync(name);
        await App.Library.AddToCollectionAsync(game.Id, collection.Id);
        ShowToast(App.Strings.Format("Toast.CollectionCreated", collection.Name));
        RefreshCollectionsPanel();
    }
}
