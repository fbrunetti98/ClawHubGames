using ClawHub.Core.Parsing;

namespace ClawHub.Core.Tests;

public class ParserTests
{
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void Vdf_ReadsNestedObjectsEscapesAndIgnoresComments()
    {
        var root = VdfParser.Parse("""
            "a" { "b" "C:\\x" // commento
              "c" { "d" "1" } }
            """);

        Assert.Equal(@"C:\x", root.GetObject("A")!.GetString("b"));
        Assert.Equal(1, root.GetObject("a")!.GetObject("c")!.GetLong("d"));
    }

    [Fact]
    public void Vdf_ThrowsOnUnbalancedBraces()
    {
        Assert.Throws<FormatException>(() => VdfParser.Parse("\"a\" { \"b\" \"c\""));
    }

    [Fact]
    public void Steam_ParsesLibraryFolders()
    {
        var folders = SteamLibraryParser.ParseLibraryFolders(Fixture("libraryfolders.vdf"));

        Assert.Equal([@"C:\Program Files (x86)\Steam", @"D:\SteamLibrary"], folders);
    }

    [Fact]
    public void Steam_ParsesLegacyLibraryFolders()
    {
        var folders = SteamLibraryParser.ParseLibraryFolders("""
            "LibraryFolders" { "TimeNextStatsReport" "123" "ContentStatsID" "-1" "1" "E:\\Games\\Steam" }
            """);

        Assert.Equal([@"E:\Games\Steam"], folders);
    }

    [Fact]
    public void Steam_ParsesAppManifest()
    {
        var manifest = SteamLibraryParser.ParseAppManifest(Fixture("appmanifest_1245620.acf"))!;

        Assert.Equal("1245620", manifest.AppId);
        Assert.Equal("ELDEN RING", manifest.Name);
        Assert.Equal("ELDEN RING", manifest.InstallDir);
        Assert.True(manifest.IsFullyInstalled);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1730000000), manifest.LastPlayed);
        Assert.True(SteamLibraryParser.IsGame(manifest));
    }

    [Fact]
    public void Steam_ExcludesRedistributablesAndIncompleteInstalls()
    {
        var redist = new SteamAppManifest("228980", "Steamworks Common Redistributables", "x", true, null);
        var downloading = new SteamAppManifest("10", "Counter-Strike", "x", false, null);

        Assert.False(SteamLibraryParser.IsGame(redist));
        Assert.False(SteamLibraryParser.IsGame(downloading));
    }

    [Fact]
    public void Epic_ParsesGameAndBuildsLaunchUri()
    {
        var manifest = EpicManifestParser.Parse(Fixture("epic_game.item"))!;

        Assert.Equal("Hades II", manifest.DisplayName);
        Assert.Equal("com.epicgames.launcher://apps/ns123%3Aitem456%3AHades2?action=launch&silent=true", manifest.LaunchUri);
    }

    [Fact]
    public void Epic_SkipsPluginsAndDlc()
    {
        Assert.Null(EpicManifestParser.Parse(Fixture("epic_plugin.item")));
        Assert.Null(EpicManifestParser.Parse("""
            { "DisplayName": "DLC", "AppName": "Dlc1", "MainGameAppName": "Base", "AppCategories": ["games"] }
            """));
        Assert.Null(EpicManifestParser.Parse("""
            { "DisplayName": "Half", "AppName": "A", "bIsIncompleteInstall": true, "AppCategories": ["games"] }
            """));
    }

    [Fact]
    public void Ea_ParsesOriginFormat()
    {
        var data = EaInstallerDataParser.Parse(Fixture("ea_origin.xml"));

        Assert.Equal("Titanfall 2", data.Title);
        Assert.Equal(["1063865", "1063866"], data.ContentIds);
        Assert.Equal("Titanfall2.exe", data.LauncherRelativePath);
        Assert.Equal("origin2://game/launch?offerIds=1063865%2C1063866&autoDownload=1", data.LaunchUri);
    }

    [Fact]
    public void Ea_ParsesEaAppFormat()
    {
        var data = EaInstallerDataParser.Parse(Fixture("ea_app.xml"));

        Assert.Equal("EA SPORTS FC 25", data.Title);
        Assert.Equal(["196784"], data.ContentIds);
        Assert.Null(data.LauncherRelativePath);
    }

    [Theory]
    [InlineData("Diablo III", "D3")]
    [InlineData("Diablo II: Resurrected", "OSI")]
    [InlineData("Diablo IV", "Fen")]
    [InlineData("StarCraft II", "S2")]
    [InlineData("StarCraft", "S1")]
    [InlineData("Overwatch", "Pro")]
    [InlineData("Some Unknown Game", null)]
    public void BattleNet_FindsProductCodes(string name, string? expected)
    {
        Assert.Equal(expected, BattleNetProducts.FindCode(name));
    }
}
