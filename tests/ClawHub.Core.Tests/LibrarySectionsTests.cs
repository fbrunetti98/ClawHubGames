using ClawHub.Core.Models;
using ClawHub.Core.Services;

namespace ClawHub.Core.Tests;

public class LibrarySectionsTests
{
    private static Game Game(string id, string name, GameStore store = GameStore.Steam, bool favorite = false,
        DateTimeOffset? lastPlayed = null, params string[] collections) => new()
    {
        Source = new DetectedGame { Store = store, StoreId = id, Name = name, Launch = LaunchInfo.FromUri($"test://{id}") },
        UserData = new GameUserData { IsFavorite = favorite, LastPlayed = lastPlayed, CollectionIds = collections },
    };

    [Fact]
    public void Build_OrdersSectionsAndSkipsEmptyOnes()
    {
        var friends = new GameCollection("c1", "Con gli amici", 0);
        var empty = new GameCollection("c2", "Vuota", 1);
        var games = new[]
        {
            Game("1", "Titanfall 2", favorite: true, lastPlayed: DateTimeOffset.Now.AddDays(-2)),
            Game("2", "Steep", GameStore.Ubisoft, collections: ["c1"]),
            Game("3", "Arma 3"),
        };

        var sections = LibrarySections.Build(games, [friends, empty]);

        Assert.Equal(new[] { "all", "recent", "favorites", "collection:c1", "store:Steam", "store:Ubisoft" }, sections.Select(s => s.Key));
        Assert.Equal(new[] { "Arma 3", "Steep", "Titanfall 2" }, sections[0].Games.Select(g => g.Name));
    }

    [Fact]
    public void Build_KeepsAllGamesEvenWhenLibraryIsEmpty()
    {
        var section = Assert.Single(LibrarySections.Build([], []));
        Assert.Equal(LibrarySectionKind.All, section.Kind);
    }

    [Fact]
    public void Build_RecentIsSortedByLastPlayedAndLimited()
    {
        var now = DateTimeOffset.Now;
        var games = Enumerable.Range(1, 5).Select(i => Game(i.ToString(), "Gioco " + i, lastPlayed: now.AddDays(-i))).ToList();

        var recent = LibrarySections.Build(games, [], recentLimit: 3).Single(s => s.Kind == LibrarySectionKind.Recent);

        Assert.Equal(new[] { "Gioco 1", "Gioco 2", "Gioco 3" }, recent.Games.Select(g => g.Name));
    }

    [Theory]
    [InlineData(0, "Time.Today", 0)]
    [InlineData(1, "Time.Yesterday", 1)]
    [InlineData(3, "Time.DaysAgo", 3)]
    [InlineData(8, "Time.WeekAgo", 1)]
    [InlineData(15, "Time.WeeksAgo", 2)]
    [InlineData(45, "Time.MonthAgo", 1)]
    [InlineData(100, "Time.MonthsAgo", 3)]
    [InlineData(400, "Time.YearAgo", 1)]
    [InlineData(800, "Time.YearsAgo", 2)]
    public void RelativeTime_DescribesCalendarDays(int daysAgo, string key, int value)
    {
        var now = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(new RelativeTimeText(key, value), RelativeTime.Describe(now.AddDays(-daysAgo), now));
    }
}
