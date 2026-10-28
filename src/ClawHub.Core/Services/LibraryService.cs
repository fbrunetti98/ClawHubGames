using ClawHub.Core.Abstractions;
using ClawHub.Core.Models;
using ClawHub.Core.Storage;

namespace ClawHub.Core.Services;

/// <summary>
/// Unisce i giochi trovati dagli store con i dati dell'utente (preferiti, collezioni, giochi manuali)
/// e salva ogni modifica su disco.
/// </summary>
public sealed class LibraryService(IEnumerable<ILibraryProvider> providers, JsonFileStore<UserLibrary> store)
{
    private readonly IReadOnlyList<ILibraryProvider> _providers = providers.ToList();
    private readonly object _sync = new();
    private UserLibrary _library = new();
    private IReadOnlyList<Game> _games = [];

    /// <summary>Scatta dopo ogni scansione o modifica dei dati. Può arrivare da un thread in background.</summary>
    public event EventHandler? Changed;

    public IReadOnlyList<Game> Games
    {
        get { lock (_sync) return _games; }
    }

    public IReadOnlyList<GameCollection> Collections
    {
        get { lock (_sync) return _library.Collections.OrderBy(c => c.Order).ToList(); }
    }

    public Game? Find(string gameId) => Games.FirstOrDefault(g => g.Id == gameId);

    /// <summary>Carica i dati salvati e mostra subito l'ultima scansione nota.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var library = await store.LoadAsync(cancellationToken);
        lock (_sync)
        {
            _library = library;
            Rebuild();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Interroga tutti gli store in parallelo. L'errore di uno store non blocca gli altri.</summary>
    public async Task<ScanReport> ScanAsync(IReadOnlySet<GameStore>? disabledStores = null, CancellationToken cancellationToken = default)
    {
        var active = _providers.Where(p => disabledStores is null || !disabledStores.Contains(p.Store)).ToList();
        var errors = new List<ProviderScanError>();

        async Task<IReadOnlyList<DetectedGame>> ScanProviderAsync(ILibraryProvider provider)
        {
            try
            {
                if (!provider.IsAvailable())
                    return [];
                return await Task.Run(() => provider.GetInstalledGamesAsync(cancellationToken), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lock (errors)
                    errors.Add(new ProviderScanError(provider.Store, ex.Message));
                return [];
            }
        }

        var results = await Task.WhenAll(active.Select(ScanProviderAsync));

        var failedStores = errors.Select(e => e.Store).ToHashSet();

        int count;
        lock (_sync)
        {
            // Se uno store fallisce teniamo i suoi giochi della scansione precedente invece di farli sparire.
            var detected = results.SelectMany(r => r)
                .Concat(_library.LastScan.Where(g => failedStores.Contains(g.Store)))
                .GroupBy(g => g.Id)
                .Select(g => g.First())
                .ToList();
            detected = RemoveSameFolderDuplicates(detected);
            _library = _library with { LastScan = detected };
            Rebuild();
            count = _games.Count;
        }

        await SaveAndNotifyAsync(cancellationToken);
        return new ScanReport(count, errors);
    }

    /// <summary>
    /// Alcuni giochi vengono trovati da due store nella stessa cartella (es. Titanfall 2 su Steam contiene ancora
    /// l'installer EA). Vince il primo provider registrato, cioè lo store da cui il gioco è stato installato davvero.
    /// </summary>
    private static List<DetectedGame> RemoveSameFolderDuplicates(List<DetectedGame> games)
    {
        var seenFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return games.Where(g => string.IsNullOrWhiteSpace(g.InstallDirectory)
                                || seenFolders.Add(g.InstallDirectory.Replace('/', '\\').TrimEnd('\\')))
            .ToList();
    }

    public IReadOnlyList<Game> Query(LibraryQuery query)
    {
        IEnumerable<Game> result = Games;

        if (!query.IncludeHidden)
            result = result.Where(g => !g.IsHidden);
        if (query.Store is { } storeFilter)
            result = result.Where(g => g.Store == storeFilter);
        if (query.FavoritesOnly)
            result = result.Where(g => g.IsFavorite);
        if (!string.IsNullOrEmpty(query.CollectionId))
            result = result.Where(g => g.CollectionIds.Contains(query.CollectionId));
        if (!string.IsNullOrWhiteSpace(query.Search))
            result = result.Where(g => SearchText.Matches(g.Name, query.Search));

        return query.Sort switch
        {
            LibrarySort.LastPlayed => result
                .OrderByDescending(g => g.LastPlayed ?? DateTimeOffset.MinValue)
                .ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            _ => result.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList(),
        };
    }

    /// <summary>Raggruppa per store nell'ordine dell'enum <see cref="GameStore"/>, saltando gli store vuoti.</summary>
    public static IReadOnlyList<IGrouping<GameStore, Game>> GroupByStore(IEnumerable<Game> games) =>
        games.GroupBy(g => g.Store).OrderBy(g => g.Key).ToList();

    public Task SetFavoriteAsync(string gameId, bool isFavorite) =>
        UpdateGameAsync(gameId, d => d with { IsFavorite = isFavorite });

    public Task SetHiddenAsync(string gameId, bool isHidden) =>
        UpdateGameAsync(gameId, d => d with { IsHidden = isHidden });

    public Task RenameGameAsync(string gameId, string? customName) =>
        UpdateGameAsync(gameId, d => d with { CustomName = string.IsNullOrWhiteSpace(customName) ? null : customName.Trim() });

    public Task SetArtworkAsync(string gameId, ArtworkSet artwork) =>
        UpdateGameAsync(gameId, d => d with { Artwork = artwork });

    /// <summary>Aggiorna le immagini di più giochi con un solo salvataggio.</summary>
    public async Task SetArtworkAsync(IReadOnlyDictionary<string, ArtworkSet> artworkByGameId)
    {
        lock (_sync)
        {
            var games = new Dictionary<string, GameUserData>(_library.Games);
            foreach (var (gameId, artwork) in artworkByGameId)
                games[gameId] = (games.GetValueOrDefault(gameId) ?? GameUserData.Empty) with { Artwork = artwork };
            _library = _library with { Games = games };
            Rebuild();
        }
        await SaveAndNotifyAsync();
    }

    public Task RecordLaunchAsync(string gameId, DateTimeOffset? when = null) =>
        UpdateGameAsync(gameId, d => d with { LastPlayed = when ?? DateTimeOffset.Now, PlayCount = d.PlayCount + 1 });

    public async Task<GameCollection> CreateCollectionAsync(string name)
    {
        GameCollection collection;
        lock (_sync)
        {
            var order = _library.Collections.Count == 0 ? 0 : _library.Collections.Max(c => c.Order) + 1;
            collection = new GameCollection(Guid.NewGuid().ToString("N"), RequireName(name), order);
            _library = _library with { Collections = [.. _library.Collections, collection] };
        }
        await SaveAndNotifyAsync();
        return collection;
    }

    public async Task RenameCollectionAsync(string collectionId, string name)
    {
        lock (_sync)
        {
            _library = _library with
            {
                Collections = _library.Collections
                    .Select(c => c.Id == collectionId ? c with { Name = RequireName(name) } : c)
                    .ToList(),
            };
        }
        await SaveAndNotifyAsync();
    }

    public async Task DeleteCollectionAsync(string collectionId)
    {
        lock (_sync)
        {
            var games = _library.Games.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.CollectionIds.Contains(collectionId)
                    ? kv.Value with { CollectionIds = kv.Value.CollectionIds.Where(id => id != collectionId).ToList() }
                    : kv.Value);
            _library = _library with
            {
                Collections = _library.Collections.Where(c => c.Id != collectionId).ToList(),
                Games = games,
            };
            Rebuild();
        }
        await SaveAndNotifyAsync();
    }

    public Task AddToCollectionAsync(string gameId, string collectionId) =>
        UpdateGameAsync(gameId, d => d.CollectionIds.Contains(collectionId)
            ? d
            : d with { CollectionIds = [.. d.CollectionIds, collectionId] });

    public Task RemoveFromCollectionAsync(string gameId, string collectionId) =>
        UpdateGameAsync(gameId, d => d with { CollectionIds = d.CollectionIds.Where(id => id != collectionId).ToList() });

    public async Task<Game> AddManualGameAsync(string name, string executablePath, string? arguments = null, string? workingDirectory = null)
    {
        var entry = new ManualGameEntry(Guid.NewGuid().ToString("N"), RequireName(name), executablePath, arguments, workingDirectory);
        lock (_sync)
        {
            _library = _library with { ManualGames = [.. _library.ManualGames, entry] };
            Rebuild();
        }
        await SaveAndNotifyAsync();
        return Find(GameId.Create(GameStore.Manual, entry.Id))!;
    }

    public async Task RemoveManualGameAsync(string gameId)
    {
        lock (_sync)
        {
            _library = _library with
            {
                ManualGames = _library.ManualGames.Where(m => GameId.Create(GameStore.Manual, m.Id) != gameId).ToList(),
                Games = _library.Games.Where(kv => kv.Key != gameId).ToDictionary(),
            };
            Rebuild();
        }
        await SaveAndNotifyAsync();
    }

    private async Task UpdateGameAsync(string gameId, Func<GameUserData, GameUserData> update)
    {
        lock (_sync)
        {
            var current = _library.Games.GetValueOrDefault(gameId) ?? GameUserData.Empty;
            var games = new Dictionary<string, GameUserData>(_library.Games) { [gameId] = update(current) };
            _library = _library with { Games = games };
            Rebuild();
        }
        await SaveAndNotifyAsync();
    }

    private async Task SaveAndNotifyAsync(CancellationToken cancellationToken = default)
    {
        UserLibrary snapshot;
        lock (_sync)
            snapshot = _library;
        await store.SaveAsync(snapshot, cancellationToken);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // Va chiamato dentro lock(_sync).
    private void Rebuild()
    {
        var manual = _library.ManualGames.Select(m => new DetectedGame
        {
            Store = GameStore.Manual,
            StoreId = m.Id,
            Name = m.Name,
            Launch = LaunchInfo.FromExecutable(m.ExecutablePath, m.Arguments, m.WorkingDirectory),
            InstallDirectory = m.WorkingDirectory ?? Path.GetDirectoryName(m.ExecutablePath),
        });

        _games = _library.LastScan
            .Concat(manual)
            .Select(d => new Game { Source = d, UserData = _library.Games.GetValueOrDefault(d.Id) ?? GameUserData.Empty })
            .ToList();
    }

    private static string RequireName(string name) =>
        string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required.", nameof(name)) : name.Trim();
}
