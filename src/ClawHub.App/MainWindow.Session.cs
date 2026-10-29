using System.Diagnostics;
using ClawHub.App.Services;
using ClawHub.Core.Sessions;
using ClawHub.Providers.Infrastructure;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace ClawHub.App;

// Partite in corso, launcher in background, tasto MSI (nuova attivazione) e aggiornamenti.
public sealed partial class MainWindow
{
    private static readonly TimeSpan SlowPoll = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan FastPoll = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan LaunchTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromHours(6);

    private readonly GameSessionTracker _tracker = new();
    private readonly AppUpdater _updater = new();
    private DispatcherQueueTimer? _sessionTimer;
    private DispatcherQueueTimer? _updateTimer;
    private DispatcherQueueTimer? _hideTimer;
    private bool _polling;
    private bool _wasFullScreen = true;
    private string? _promptedUpdate;

    /// <summary>Gioco avviato dal launcher che non è ancora partito (lo store può metterci un po').</summary>
    private string? _waitingGameId;
    private DateTimeOffset _waitingSince;

    /// <summary>Gioco avviato dal launcher: quando si chiude, il launcher torna in primo piano.</summary>
    private string? _returnAfterGameId;

    private IntPtr WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(this);

    private bool IsInForeground => NativeMethods.GetForegroundWindow() == WindowHandle;

    private void StartBackgroundServices()
    {
        Program.ActivationRequested += () => DispatcherQueue.TryEnqueue(OnExternalActivation);

        _sessionTimer = CreateTimer(SlowPoll, repeating: true, () => _ = PollSessionsAsync());
        _sessionTimer.Start();

        // Primo controllo degli aggiornamenti poco dopo l'avvio, poi ogni 6 ore.
        _updateTimer = CreateTimer(TimeSpan.FromSeconds(20), repeating: true, () =>
        {
            _updateTimer!.Interval = UpdateInterval;
            _ = CheckForUpdatesAsync(announce: false);
        });
        _updateTimer.Start();
    }

    // ----- Partite -----

    /// <summary>Da chiamare dopo aver avviato un gioco: il launcher si fa da parte e aspetta che il gioco parta.</summary>
    private void OnGameLaunched(string gameId)
    {
        _waitingGameId = gameId;
        _returnAfterGameId = gameId;
        _waitingSince = DateTimeOffset.Now;
        UpdatePollInterval();

        // Un attimo per far vedere l'avviso "Avvio di…", poi si lascia lo schermo al gioco o allo store.
        _hideTimer ??= CreateTimer(TimeSpan.FromSeconds(1.5), repeating: false, HideLauncher);
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private async Task PollSessionsAsync()
    {
        if (_polling)
            return;
        _polling = true;
        try
        {
            var games = App.Library.Games;
            var processes = await Task.Run(() => RunningProcesses.Snapshot());
            var changes = _tracker.Update(games, processes, DateTimeOffset.Now);

            if (changes.Started.Any(s => s.GameId == _waitingGameId))
                _waitingGameId = null;

            foreach (var ended in changes.Ended)
            {
                await App.Library.RecordSessionAsync(ended.Session.GameId, ended.Session.Started, ended.Duration);
                if (ended.Session.GameId == _returnAfterGameId)
                {
                    _returnAfterGameId = null;
                    ShowLauncher();
                }
            }

            // Il gioco non è mai partito (store chiuso, errore, login annullato): si smette di aspettarlo.
            if (_waitingGameId is not null && DateTimeOffset.Now - _waitingSince > LaunchTimeout)
            {
                _waitingGameId = null;
                if (_tracker.Find(_returnAfterGameId ?? string.Empty) is null)
                    _returnAfterGameId = null;
            }

            if (!changes.IsEmpty)
                UpdateInGameChip();
            UpdatePollInterval();
        }
        catch (Exception)
        {
            // Un controllo andato male non deve fermare il launcher: si riprova al prossimo giro.
        }
        finally
        {
            _polling = false;
        }
    }

    private void UpdatePollInterval()
    {
        if (_sessionTimer is null)
            return;
        // Più spesso mentre si aspetta un gioco o se ne segue uno avviato dal launcher, per tornare subito.
        var fast = _waitingGameId is not null || (_returnAfterGameId is not null && _tracker.Find(_returnAfterGameId) is not null);
        _sessionTimer.Interval = fast ? FastPoll : SlowPoll;
    }

    /// <summary>La partita da mostrare: quella avviata dal launcher, altrimenti la più recente.</summary>
    private GameSession? CurrentSession =>
        (_returnAfterGameId is null ? null : _tracker.Find(_returnAfterGameId))
        ?? _tracker.ActiveSessions.OrderByDescending(s => s.Started).FirstOrDefault();

    private void UpdateInGameChip()
    {
        if (CurrentSession is { } session && App.Library.Find(session.GameId) is { } game)
        {
            InGameText.Text = App.Strings.Format("InGame.Chip", game.Name);
            InGameChip.Visibility = Visibility.Visible;
        }
        else
        {
            InGameChip.Visibility = Visibility.Collapsed;
        }
    }

    private void OpenInGameMenu()
    {
        if (CurrentSession is not { } current)
            return;
        var gameId = current.GameId;
        var confirmClose = false;
        string Name() => App.Library.Find(gameId)?.Name ?? string.Empty;

        OpenMenu(
            () => App.Strings.Format("InGame.Chip", Name()),
            () => _tracker.Find(gameId) is { } session
                ? App.Strings.Format("InGame.Since", FormatPlayTime(DateTimeOffset.Now - session.Started))
                : string.Empty,
            () =>
            [
                new MenuItem(App.Strings["InGame.Return"], string.Empty, () =>
                {
                    CloseMenu();
                    ReturnToGame(gameId);
                    return Task.CompletedTask;
                }),
                confirmClose
                    ? new MenuItem(App.Strings.Format("InGame.ConfirmClose", Name()), string.Empty, () =>
                    {
                        CloseMenu();
                        CloseGame(gameId);
                        return Task.CompletedTask;
                    })
                    : new MenuItem(App.Strings["InGame.Close"], string.Empty, () =>
                    {
                        confirmClose = true; // Serve una seconda conferma: si perdono i progressi non salvati.
                        return Task.CompletedTask;
                    }),
                new MenuItem(App.Strings["InGame.Stay"], string.Empty, () =>
                {
                    CloseMenu();
                    return Task.CompletedTask;
                }),
            ]);
    }

    private void ReturnToGame(string gameId)
    {
        var window = IntPtr.Zero;
        foreach (var id in _tracker.Find(gameId)?.ProcessIds ?? Array.Empty<int>())
        {
            try
            {
                using var process = Process.GetProcessById(id);
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    window = process.MainWindowHandle;
                    break;
                }
            }
            catch (ArgumentException)
            {
                // Processo già chiuso.
            }
        }
        HideLauncher();
        NativeMethods.BringToFront(window);
    }

    private void CloseGame(string gameId)
    {
        var name = App.Library.Find(gameId)?.Name ?? string.Empty;
        foreach (var id in _tracker.Find(gameId)?.ProcessIds ?? Array.Empty<int>())
        {
            try
            {
                using var process = Process.GetProcessById(id);
                process.Kill(entireProcessTree: true);
            }
            catch (Exception)
            {
                // Già chiuso o protetto: si prova con gli altri processi.
            }
        }
        if (_returnAfterGameId == gameId)
            _returnAfterGameId = null; // Il launcher è già in primo piano.
        ShowToast(App.Strings.Format("Toast.GameClosed", name));
    }

    // ----- Finestra -----

    /// <summary>Riduce a icona il launcher. A schermo intero Windows non lo permette: si passa prima alla finestra.</summary>
    private void HideLauncher()
    {
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized })
            return;
        _wasFullScreen = AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;
        if (_wasFullScreen)
            AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
        (AppWindow.Presenter as OverlappedPresenter)?.Minimize();
        _clock.Stop();
    }

    private void ShowLauncher()
    {
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } overlapped)
            overlapped.Restore();
        if (_wasFullScreen)
            AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        NativeMethods.BringToFront(WindowHandle);
        Activate();
        KeySink.Focus(FocusState.Programmatic);
        _clock.Start();
        UpdateClock();
        RefreshSettings();
    }

    /// <summary>
    /// L'exe è stato riaperto (tasto MSI, AnyFSE, menu Start). Se il launcher è già davanti e c'è un gioco aperto
    /// si torna al gioco; altrimenti il launcher viene in primo piano, con il menu del gioco in corso se c'è.
    /// </summary>
    private void OnExternalActivation()
    {
        var session = CurrentSession;
        if (IsInForeground && session is not null && _menu is null && !Keyboard.IsOpen)
        {
            ReturnToGame(session.GameId);
            return;
        }

        ShowLauncher();
        if (session is not null && _menu is null && !Keyboard.IsOpen)
            OpenInGameMenu();
        else
            OfferUpdate();
    }

    // ----- Aggiornamenti -----

    private async Task CheckForUpdatesAsync(bool announce)
    {
        if (!_updater.IsInstalled)
        {
            if (announce)
                ShowToast(App.Strings["Update.NotInstalled"]);
            return;
        }

        RefreshSettings();
        try
        {
            var ready = await _updater.CheckAndDownloadAsync(App.Settings.ReceivePreviewUpdates);
            if (ready is null && announce)
                ShowToast(App.Strings["Toast.UpToDate"]);
        }
        catch (Exception)
        {
            if (announce)
                ShowToast(App.Strings["Toast.UpdateFailed"]);
        }
        RefreshSettings();
        if (announce && _updater.ReadyVersion is not null)
            _promptedUpdate = null; // Richiesto a mano: si chiede di nuovo.
        OfferUpdate();
    }

    /// <summary>Propone di riavviare per aggiornare, una volta per versione e mai durante una partita.</summary>
    private void OfferUpdate()
    {
        var version = _updater.ReadyVersion;
        if (version is null || version == _promptedUpdate || CurrentSession is not null || !IsInForeground || _menu is not null || Keyboard.IsOpen)
            return;

        _promptedUpdate = version;
        OpenMenu(
            () => App.Strings["Update.PromptTitle"],
            () => App.Strings.Format("Update.PromptSubtitle", version),
            () =>
            [
                new MenuItem(App.Strings["Update.RestartNow"], string.Empty, () =>
                {
                    _updater.ApplyAndRestart();
                    return Task.CompletedTask;
                }),
                new MenuItem(App.Strings["Update.Later"], string.Empty, () =>
                {
                    CloseMenu();
                    return Task.CompletedTask;
                }),
            ]);
    }

    private string UpdateStatusText() =>
        !_updater.IsInstalled ? App.Strings["Update.NotInstalled"]
        : _updater.IsChecking ? App.Strings["Update.Checking"]
        : _updater.ReadyVersion is { } ready ? App.Strings.Format("Update.Ready", ready)
        : App.Strings.Format("Update.UpToDate", _updater.CurrentVersion ?? "?");

    private Task RunUpdateSettingAsync()
    {
        if (_updater.ReadyVersion is not null)
        {
            _updater.ApplyAndRestart();
            return Task.CompletedTask;
        }
        return CheckForUpdatesAsync(announce: true);
    }

    // ----- Testi -----

    private static string FormatPlayTime(TimeSpan time) =>
        time.TotalMinutes < 1 ? App.Strings["PlayTime.LessThanMinute"]
        : time.TotalHours < 1 ? App.Strings.Format("PlayTime.Minutes", (int)time.TotalMinutes)
        : App.Strings.Format("PlayTime.Hours", (int)time.TotalHours, time.Minutes);
}
