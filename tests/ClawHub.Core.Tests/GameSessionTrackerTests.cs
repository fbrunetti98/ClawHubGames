using ClawHub.Core.Models;
using ClawHub.Core.Sessions;

namespace ClawHub.Core.Tests;

public class GameSessionTrackerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 29, 20, 0, 0, TimeSpan.Zero);

    private static Game Game(string id, string? folder, GameStore store = GameStore.Steam) => new()
    {
        Source = new DetectedGame { Store = store, StoreId = id, Name = id, Launch = LaunchInfo.FromUri("test://" + id), InstallDirectory = folder },
        UserData = GameUserData.Empty,
    };

    private static readonly Game[] Games =
    [
        Game("titanfall", @"D:\SteamLibrary\steamapps\common\Titanfall2"),
        Game("gta", @"C:\Program Files\Rockstar Games\Grand Theft Auto V Enhanced", GameStore.Rockstar),
        Game("generic", @"C:\Giochi", GameStore.Manual),
    ];

    [Fact]
    public void Update_StartsAndEndsSessionFromInstallFolder()
    {
        var tracker = new GameSessionTracker(missesBeforeEnd: 2);
        var running = new[] { new RunningProcess(42, @"d:\steamlibrary\steamapps\common\Titanfall2\Titanfall2.exe") };

        var start = tracker.Update(Games, running, T0);
        tracker.Update(Games, running, T0.AddMinutes(30));
        var firstMiss = tracker.Update(Games, [], T0.AddMinutes(31));
        var end = tracker.Update(Games, [], T0.AddMinutes(32));

        Assert.Equal("titanfall", Assert.Single(start.Started).GameId);
        Assert.True(firstMiss.IsEmpty); // Un controllo senza processi non basta: il gioco potrebbe riavviarsi.
        var ended = Assert.Single(end.Ended);
        Assert.Equal(TimeSpan.FromMinutes(30), ended.Duration);
        Assert.Empty(tracker.ActiveSessions);
    }

    [Fact]
    public void Update_KeepsSessionWhenTheGameSwitchesExecutable()
    {
        var tracker = new GameSessionTracker(missesBeforeEnd: 2);
        tracker.Update(Games, [new RunningProcess(1, @"C:\Program Files\Rockstar Games\Grand Theft Auto V Enhanced\PlayGTAV.exe")], T0);
        tracker.Update(Games, [], T0.AddSeconds(15));
        var changes = tracker.Update(Games, [new RunningProcess(2, @"C:\Program Files\Rockstar Games\Grand Theft Auto V Enhanced\GTA5_Enhanced.exe")], T0.AddSeconds(30));

        Assert.True(changes.IsEmpty);
        Assert.Equal(new[] { 2 }, tracker.Find("gta")!.ProcessIds);
    }

    [Fact]
    public void Update_IgnoresGenericFoldersAndSimilarNames()
    {
        var tracker = new GameSessionTracker();
        var changes = tracker.Update(Games,
        [
            new RunningProcess(1, @"C:\Giochi\qualcosa.exe"),
            new RunningProcess(2, @"D:\SteamLibrary\steamapps\common\Titanfall2Server\server.exe"),
        ], T0);

        Assert.True(changes.IsEmpty);
    }

    [Theory]
    [InlineData(@"C:\", null)]
    [InlineData(@"C:\Giochi", null)]
    [InlineData(@"C:\Giochi\Celeste\", @"C:\Giochi\Celeste")]
    [InlineData(@"""C:\XboxGames\Minecraft""", @"C:\XboxGames\Minecraft")]
    [InlineData(null, null)]
    public void NormalizeFolder_RequiresTwoLevelsBelowTheDrive(string? folder, string? expected)
    {
        Assert.Equal(expected, GameSessionTracker.NormalizeFolder(folder));
    }
}
