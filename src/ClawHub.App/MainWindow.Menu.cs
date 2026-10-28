using System.Globalization;
using ClawHub.App.Input;
using ClawHub.Core.Models;
using ClawHub.Providers.Infrastructure;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace ClawHub.App;

// Menu sopra l'interfaccia (collezioni, aggiunta di un gioco, "Altro") e richiesta di testo con la tastiera a schermo.
public sealed partial class MainWindow
{
    private sealed record MenuItem(string Text, string Value, Func<Task> Run);

    /// <summary>Un menu si ricostruisce ogni volta dai dati attuali: titolo, sottotitolo e voci sono funzioni.</summary>
    private sealed record MenuSpec(Func<string> Title, Func<string> Subtitle, Func<IReadOnlyList<MenuItem>> Items, int Index);

    private readonly Stack<MenuSpec> _menuStack = new();
    private readonly List<Border> _menuRows = [];
    private IReadOnlyList<MenuItem> _menuItems = [];
    private MenuSpec? _menu;
    private int _menuIndex;

    // ----- Menu generico -----

    /// <summary>Apre un menu; se ce n'è già uno aperto, B ci torna.</summary>
    private void OpenMenu(Func<string> title, Func<string> subtitle, Func<IReadOnlyList<MenuItem>> items)
    {
        if (_menu is not null)
            _menuStack.Push(_menu with { Index = _menuIndex });
        _menu = new MenuSpec(title, subtitle, items, 0);
        _menuIndex = 0;
        MenuOverlay.Visibility = Visibility.Visible;
        RefreshMenu();
        AfterOverlayChanged();
    }

    private void BackMenu()
    {
        if (_menuStack.Count == 0)
        {
            CloseMenu();
            return;
        }
        _menu = _menuStack.Pop();
        _menuIndex = _menu.Index;
        RefreshMenu();
    }

    private void CloseMenu()
    {
        _menuStack.Clear();
        _menu = null;
        MenuOverlay.Visibility = Visibility.Collapsed;
        AfterOverlayChanged();
    }

    private void AfterOverlayChanged()
    {
        UpdateDetailButtons();
        UpdateSettingsHighlight();
        BuildLegend();
    }

    private void RefreshMenu()
    {
        if (_menu is null)
            return;

        MenuTitle.Text = _menu.Title().ToUpper(CultureInfo.CurrentCulture);
        var subtitle = _menu.Subtitle();
        MenuSubtitle.Text = subtitle;
        MenuSubtitle.Visibility = subtitle.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

        _menuItems = _menu.Items();
        MenuList.Children.Clear();
        _menuRows.Clear();
        for (var i = 0; i < _menuItems.Count; i++)
        {
            var row = ListRow(_menuItems[i].Text, _menuItems[i].Value, 17);
            row.Padding = new Thickness(16, 12, 16, 12);
            var index = i;
            row.Tapped += (_, _) =>
            {
                if (Keyboard.IsOpen)
                    return;
                _menuIndex = index;
                UpdateMenuRows();
                _ = ActivateMenuItemAsync();
            };
            _menuRows.Add(row);
            MenuList.Children.Add(row);
        }
        _menuIndex = Math.Clamp(_menuIndex, 0, Math.Max(0, _menuRows.Count - 1));
        UpdateMenuRows();
    }

    private void UpdateMenuRows()
    {
        for (var i = 0; i < _menuRows.Count; i++)
            SetRowState(_menuRows[i], i == _menuIndex, focused: i == _menuIndex && !Keyboard.IsOpen);
        if (_menuIndex < _menuRows.Count)
            _menuRows[_menuIndex].StartBringIntoView();
    }

    private bool HandleMenu(PadButton button)
    {
        if (_menu is null)
            return false;

        switch (button)
        {
            case PadButton.Up:
            case PadButton.Down:
                _menuIndex = Math.Clamp(_menuIndex + (button == PadButton.Down ? 1 : -1), 0, Math.Max(0, _menuRows.Count - 1));
                UpdateMenuRows();
                break;
            case PadButton.A:
                _ = ActivateMenuItemAsync();
                break;
            case PadButton.B:
                BackMenu();
                break;
        }
        return true;
    }

    private async Task ActivateMenuItemAsync()
    {
        if (_menuIndex >= _menuItems.Count)
            return;
        var menu = _menu;
        try
        {
            await _menuItems[_menuIndex].Run();
        }
        catch (Exception ex)
        {
            ShowToast(ex.Message);
        }
        if (_menu is not null && ReferenceEquals(_menu, menu))
            RefreshMenu();
    }

    // ----- Richiesta di testo -----

    /// <summary>Apre la tastiera a schermo. <paramref name="done"/> riceve il testo confermato.</summary>
    private void AskText(string title, string text, int maxLength, Action<string> done, Action? cancel = null, Action<string>? changed = null)
    {
        Keyboard.Open(title, text, maxLength,
            value =>
            {
                AfterKeyboardClosed();
                done(value);
            },
            () =>
            {
                AfterKeyboardClosed();
                cancel?.Invoke();
            },
            changed);
        UpdateMenuRows();
        BuildLegend();
    }

    private void AfterKeyboardClosed()
    {
        KeySink.Focus(FocusState.Programmatic);
        UpdateMenuRows();
        BuildLegend();
    }

    // ----- Collezioni di un gioco (dal dettaglio) -----

    private void OpenGameCollectionsMenu(string gameId)
    {
        OpenMenu(
            () => App.Strings["Game.AddToCollection"],
            () => App.Library.Find(gameId)?.Name ?? string.Empty,
            () =>
            {
                if (App.Library.Find(gameId) is not { } game)
                    return [];
                var items = App.Library.Collections
                    .Select(c => new MenuItem(c.Name, game.CollectionIds.Contains(c.Id) ? "✓" : string.Empty,
                        () => ToggleCollectionAsync(gameId, c)))
                    .ToList();
                items.Add(new MenuItem("+  " + App.Strings["Collection.New"], string.Empty, () =>
                {
                    AskText(App.Strings["Collection.New"], string.Empty, 40, name => _ = CreateCollectionForAsync(gameId, name));
                    return Task.CompletedTask;
                }));
                return items;
            });
    }

    private async Task ToggleCollectionAsync(string gameId, GameCollection collection)
    {
        if (App.Library.Find(gameId) is not { } game)
            return;
        var inside = game.CollectionIds.Contains(collection.Id);
        if (inside)
            await App.Library.RemoveFromCollectionAsync(gameId, collection.Id);
        else
            await App.Library.AddToCollectionAsync(gameId, collection.Id);
        ShowToast(App.Strings.Format(inside ? "Toast.RemovedFromCollection" : "Toast.AddedToCollection", game.Name, collection.Name));
    }

    private async Task CreateCollectionForAsync(string gameId, string name)
    {
        name = name.Trim();
        if (name.Length == 0)
            name = App.Strings.Format("Collection.DefaultName", App.Library.Collections.Count + 1);

        var collection = await App.Library.CreateCollectionAsync(name);
        await App.Library.AddToCollectionAsync(gameId, collection.Id);
        ShowToast(App.Strings.Format("Toast.CollectionCreated", collection.Name));
        RefreshMenu();
    }

    // ----- Gestione delle collezioni (dalle impostazioni) -----

    private void OpenCollectionsManager()
    {
        OpenMenu(
            () => App.Strings["Library.Collections"],
            () => App.Library.Collections.Count == 0 ? App.Strings["Collections.Empty"] : string.Empty,
            () => App.Library.Collections
                .Select(c => new MenuItem(c.Name,
                    App.Strings.Format("Collection.GameCount", App.Library.Games.Count(g => g.CollectionIds.Contains(c.Id))),
                    () =>
                    {
                        OpenCollectionActions(c.Id);
                        return Task.CompletedTask;
                    }))
                .ToList());
    }

    private void OpenCollectionActions(string collectionId)
    {
        GameCollection? Find() => App.Library.Collections.FirstOrDefault(c => c.Id == collectionId);
        var confirmDelete = false;

        OpenMenu(
            () => Find()?.Name ?? string.Empty,
            () => string.Empty,
            () =>
            {
                if (Find() is not { } collection)
                    return [];
                return
                [
                    new MenuItem(App.Strings["Collection.Rename"], string.Empty, () =>
                    {
                        AskText(App.Strings["Collection.Rename"], collection.Name, 40, name => _ = RenameCollectionAsync(collection.Id, name));
                        return Task.CompletedTask;
                    }),
                    new MenuItem(App.Strings["Collection.MoveUp"], string.Empty, () => App.Library.MoveCollectionAsync(collection.Id, -1)),
                    new MenuItem(App.Strings["Collection.MoveDown"], string.Empty, () => App.Library.MoveCollectionAsync(collection.Id, 1)),
                    confirmDelete
                        ? new MenuItem(App.Strings.Format("Collection.ConfirmDelete", collection.Name), string.Empty, async () =>
                        {
                            await App.Library.DeleteCollectionAsync(collection.Id);
                            ShowToast(App.Strings.Format("Toast.CollectionDeleted", collection.Name));
                            BackMenu();
                        })
                        : new MenuItem(App.Strings["Collection.Delete"], string.Empty, () =>
                        {
                            confirmDelete = true; // Serve una seconda conferma.
                            return Task.CompletedTask;
                        }),
                ];
            });
    }

    private async Task RenameCollectionAsync(string collectionId, string name)
    {
        name = name.Trim();
        if (name.Length == 0)
            return;
        await App.Library.RenameCollectionAsync(collectionId, name);
        ShowToast(App.Strings.Format("Toast.CollectionRenamed", name));
        RefreshMenu();
    }

    // ----- Aggiunta manuale di un gioco -----

    private async Task OpenAddGameMenuAsync()
    {
        var entries = await Task.Run(() => StartMenuShortcuts.Find());
        OpenMenu(
            () => App.Strings["AddGame.Title"],
            () => App.Strings["AddGame.Subtitle"],
            () =>
            {
                var items = new List<MenuItem> { new(App.Strings["AddGame.Browse"], string.Empty, BrowseForGameAsync) };
                items.AddRange(entries.Select(e => new MenuItem(e.Name, string.Empty, () =>
                {
                    AskGameName(e.Name, e.Path);
                    return Task.CompletedTask;
                })));
                return items;
            });
    }

    /// <summary>Finestra di Windows per scegliere un file: comoda col touch o col mouse.</summary>
    private async Task BrowseForGameAsync()
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
        foreach (var extension in new[] { ".exe", ".lnk", ".bat", ".cmd", ".url" })
            picker.FileTypeFilter.Add(extension);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));

        var file = await picker.PickSingleFileAsync();
        KeySink.Focus(FocusState.Programmatic);
        if (file is not null)
            AskGameName(Path.GetFileNameWithoutExtension(file.Name), file.Path);
    }

    private void AskGameName(string suggested, string path) =>
        AskText(App.Strings["AddGame.NameTitle"], suggested, 80,
            name => _ = AddManualGameAsync(name.Trim().Length == 0 ? suggested : name.Trim(), path));

    private async Task AddManualGameAsync(string name, string path)
    {
        var game = await App.Library.AddManualGameAsync(name, path);
        CloseMenu();
        ShowToast(App.Strings.Format("Toast.GameAdded", game.Name));
        StartArtworkUpdate();
    }

    // ----- "Altro" nel dettaglio -----

    private void OpenGameMoreMenu(string gameId)
    {
        OpenMenu(
            () => App.Library.Find(gameId)?.Name ?? string.Empty,
            () => string.Empty,
            () =>
            {
                if (App.Library.Find(gameId) is not { } game)
                    return [];

                var items = new List<MenuItem>
                {
                    new(App.Strings["Game.Rename"], string.Empty, () =>
                    {
                        AskText(App.Strings["Game.Rename"], game.Name, 80, name => _ = RenameGameAsync(gameId, name));
                        return Task.CompletedTask;
                    }),
                };
                if (game.UserData.CustomName is not null)
                {
                    items.Add(new(App.Strings["Game.ResetName"], game.Source.Name, async () =>
                    {
                        await App.Library.RenameGameAsync(gameId, null);
                        ShowToast(App.Strings.Format("Toast.GameRenamed", game.Source.Name));
                    }));
                }
                if (game.Store == GameStore.Manual)
                {
                    items.Add(new(App.Strings["Game.Remove"], string.Empty, async () =>
                    {
                        await App.Library.RemoveManualGameAsync(gameId);
                        CloseMenu();
                        CloseDetail();
                        ShowToast(App.Strings.Format("Toast.GameRemoved", game.Name));
                    }));
                }
                return items;
            });
    }

    private async Task RenameGameAsync(string gameId, string name)
    {
        name = name.Trim();
        if (name.Length == 0)
            return;
        await App.Library.RenameGameAsync(gameId, name);
        ShowToast(App.Strings.Format("Toast.GameRenamed", name));
        RefreshMenu();
    }
}
