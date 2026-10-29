using ClawHub.Core.Abstractions;
using ClawHub.Core.Models;
using ClawHub.Core.Services;
using ClawHub.Core.Storage;

namespace ClawHub.Core.Tests;

public sealed class LibraryServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "clawhub-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FakeProvider _steam = new(GameStore.Steam, Game("1", "Half-Life 2"), Game("2", "Pokémon Legends"));
    private readonly FakeProvider _epic = new(GameStore.Epic, Game("a", "Alan Wake 2", GameStore.Epic));

    private string LibraryFile => Path.Combine(_dir, "library.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private LibraryService CreateService() =>
        new([_steam, _epic], new JsonFileStore<UserLibrary>(LibraryFile));

    private static DetectedGame Game(string id, string name, GameStore store = GameStore.Steam) => new()
    {
        Store = store,
        StoreId = id,
        Name = name,
        Launch = LaunchInfo.FromUri($"test://{id}"),
    };

    [Fact]
    public async Task Scan_MergesAllProviders()
    {
        var service = CreateService();

        var report = await service.ScanAsync();

        Assert.Equal(3, report.GameCount);
        Assert.Empty(report.Errors);
    }

    [Fact]
    public async Task Scan_KeepsOnlyTheFirstStoreForTheSameFolder()
    {
        var steam = new FakeProvider(GameStore.Steam, Game("1237970", "Titanfall® 2") with { InstallDirectory = @"D:\SteamLibrary\steamapps\common\Titanfall2" });
        var ea = new FakeProvider(GameStore.Ea, Game("OFB-EAST:1234", "Titanfall® 2", GameStore.Ea) with { InstallDirectory = @"d:\steamlibrary\steamapps\common\Titanfall2\" });
        var service = new LibraryService([steam, ea], new JsonFileStore<UserLibrary>(LibraryFile));

        await service.ScanAsync();

        var game = Assert.Single(service.Games);
        Assert.Equal(GameStore.Steam, game.Store);
    }

    [Fact]
    public async Task SetArtwork_UpdatesSeveralGamesAndPersists()
    {
        var service = CreateService();
        await service.ScanAsync();

        await service.SetArtworkAsync(new Dictionary<string, ArtworkSet>
        {
            ["steam:1"] = new(Cover: @"C:\cache\1.jpg"),
            ["epic:a"] = new(Hero: @"C:\cache\a.jpg"),
        });

        var reloaded = CreateService();
        await reloaded.LoadAsync();
        Assert.Equal(@"C:\cache\1.jpg", reloaded.Find("steam:1")!.UserData.Artwork.Cover);
        Assert.Equal(@"C:\cache\a.jpg", reloaded.Find("epic:a")!.UserData.Artwork.Hero);
    }

    [Fact]
    public async Task MoveCollection_ReordersAndKeepsOrderCompact()
    {
        var service = CreateService();
        var a = await service.CreateCollectionAsync("A");
        var b = await service.CreateCollectionAsync("B");
        var c = await service.CreateCollectionAsync("C");

        await service.MoveCollectionAsync(c.Id, -2);
        await service.MoveCollectionAsync(a.Id, 5); // oltre la fine: va in fondo

        Assert.Equal(new[] { "C", "B", "A" }, service.Collections.Select(x => x.Name));
        Assert.Equal(new[] { 0, 1, 2 }, service.Collections.Select(x => x.Order));
    }

    [Fact]
    public async Task ClearArtwork_ForgetsDownloadedImagesButKeepsFavorites()
    {
        var service = CreateService();
        await service.ScanAsync();
        await service.SetFavoriteAsync("steam:1", true);
        await service.SetArtworkAsync(new Dictionary<string, ArtworkSet> { ["steam:1"] = new(Cover: @"C:\cache\1.jpg") });

        await service.ClearArtworkAsync();

        var game = service.Find("steam:1")!;
        Assert.Null(game.UserData.Artwork.Cover);
        Assert.True(game.IsFavorite);
    }

    [Fact]
    public async Task RecordSession_AddsPlayTimeAndUpdatesLastPlayed()
    {
        var service = CreateService();
        await service.ScanAsync();
        var start = new DateTimeOffset(2026, 10, 29, 20, 0, 0, TimeSpan.Zero);

        await service.RecordSessionAsync("steam:1", start, TimeSpan.FromMinutes(45));
        await service.RecordSessionAsync("steam:1", start.AddDays(1), TimeSpan.FromMinutes(15));

        var game = service.Find("steam:1")!;
        Assert.Equal(TimeSpan.FromHours(1), game.PlayTime);
        Assert.Equal(start.AddDays(1).AddMinutes(15), game.LastPlayed);
    }

    [Fact]
    public async Task Scan_SkipsDisabledStores()
    {
        var service = CreateService();

        await service.ScanAsync(new HashSet<GameStore> { GameStore.Epic });

        Assert.All(service.Games, g => Assert.Equal(GameStore.Steam, g.Store));
    }

    [Fact]
    public async Task Scan_KeepsPreviousGamesWhenAStoreFails()
    {
        var service = CreateService();
        await service.ScanAsync();

        _epic.Fail = true;
        var report = await service.ScanAsync();

        Assert.Single(report.Errors);
        Assert.Contains(service.Games, g => g.Name == "Alan Wake 2");
    }

    [Fact]
    public async Task Search_IgnoresCaseAccentsAndPunctuation()
    {
        var service = CreateService();
        await service.ScanAsync();

        Assert.Equal("Pokémon Legends", Assert.Single(service.Query(new LibraryQuery { Search = "pokemon" })).Name);
        Assert.Equal("Half-Life 2", Assert.Single(service.Query(new LibraryQuery { Search = "halflife" })).Name);
        Assert.Equal("Alan Wake 2", Assert.Single(service.Query(new LibraryQuery { Search = "wake ALAN" })).Name);
    }

    [Fact]
    public async Task UserData_SurvivesRescanAndRestart()
    {
        var service = CreateService();
        await service.ScanAsync();
        var collection = await service.CreateCollectionAsync("Soulslike");
        await service.SetFavoriteAsync("steam:1", true);
        await service.AddToCollectionAsync("epic:a", collection.Id);
        await service.ScanAsync();

        var restarted = CreateService();
        await restarted.LoadAsync();

        Assert.Equal(["Half-Life 2"], restarted.Query(new LibraryQuery { FavoritesOnly = true }).Select(g => g.Name));
        Assert.Equal(["Alan Wake 2"], restarted.Query(new LibraryQuery { CollectionId = collection.Id }).Select(g => g.Name));
        Assert.Equal("Soulslike", Assert.Single(restarted.Collections).Name);
    }

    [Fact]
    public async Task DeleteCollection_RemovesItFromGames()
    {
        var service = CreateService();
        await service.ScanAsync();
        var collection = await service.CreateCollectionAsync("Da finire");
        await service.AddToCollectionAsync("steam:2", collection.Id);

        await service.DeleteCollectionAsync(collection.Id);

        Assert.Empty(service.Collections);
        Assert.Empty(service.Find("steam:2")!.CollectionIds);
    }

    [Fact]
    public async Task HiddenGames_AreExcludedUnlessRequested()
    {
        var service = CreateService();
        await service.ScanAsync();
        await service.SetHiddenAsync("steam:2", true);

        Assert.DoesNotContain(service.Query(LibraryQuery.All), g => g.Id == "steam:2");
        Assert.Contains(service.Query(new LibraryQuery { IncludeHidden = true }), g => g.Id == "steam:2");
    }

    [Fact]
    public async Task ManualGames_AreAddedAndRemoved()
    {
        var service = CreateService();
        var game = await service.AddManualGameAsync("RetroArch", @"C:\RetroArch\retroarch.exe", "-f");

        Assert.Equal(GameStore.Manual, game.Store);
        Assert.Equal(LaunchKind.Executable, game.Source.Launch.Kind);
        Assert.Equal("-f", game.Source.Launch.Arguments);

        await service.RemoveManualGameAsync(game.Id);
        Assert.Null(service.Find(game.Id));
    }

    [Fact]
    public async Task RecentSort_UsesLaunchHistory()
    {
        var service = CreateService();
        await service.ScanAsync();
        await service.RecordLaunchAsync("epic:a", DateTimeOffset.Now.AddDays(-1));
        await service.RecordLaunchAsync("steam:2", DateTimeOffset.Now);

        var names = service.Query(new LibraryQuery { Sort = LibrarySort.LastPlayed }).Select(g => g.Name).ToList();

        Assert.Equal(["Pokémon Legends", "Alan Wake 2", "Half-Life 2"], names);
    }

    [Fact]
    public async Task GroupByStore_FollowsStoreOrder()
    {
        var service = CreateService();
        await service.ScanAsync();

        var groups = LibraryService.GroupByStore(service.Query(LibraryQuery.All));

        Assert.Equal([GameStore.Steam, GameStore.Epic], groups.Select(g => g.Key));
    }

    private sealed class FakeProvider(GameStore store, params DetectedGame[] games) : ILibraryProvider
    {
        public bool Fail { get; set; }
        public GameStore Store => store;
        public bool IsAvailable() => true;

        public Task<IReadOnlyList<DetectedGame>> GetInstalledGamesAsync(CancellationToken cancellationToken = default) =>
            Fail ? throw new IOException("boom") : Task.FromResult<IReadOnlyList<DetectedGame>>(games);
    }
}
