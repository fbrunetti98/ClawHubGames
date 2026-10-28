using ClawHub.Core.Services;

namespace ClawHub.Core.Tests;

public class StartMenuFilterTests
{
    [Theory]
    [InlineData("RetroArch", true)]
    [InlineData("Dolphin", true)]
    [InlineData("Uninstall RetroArch", false)]
    [InlineData("Disinstalla PCSX2", false)]
    [InlineData("Readme", false)]
    [InlineData("RetroArch Website", false)]
    [InlineData("  ", false)]
    public void IsLikelyApp_SkipsUninstallersAndDocs(string name, bool expected)
    {
        Assert.Equal(expected, StartMenuFilter.IsLikelyApp(name));
    }

    [Fact]
    public void Clean_RemovesDuplicatesAndSorts()
    {
        var entries = new[]
        {
            new StartMenuEntry("RetroArch", @"C:\ProgramData\Start Menu\RetroArch.lnk"),
            new StartMenuEntry("Dolphin", @"C:\ProgramData\Start Menu\Dolphin.lnk"),
            new StartMenuEntry("retroarch", @"C:\Users\fabio\Start Menu\RetroArch.lnk"),
            new StartMenuEntry("Uninstall Dolphin", @"C:\ProgramData\Start Menu\Uninstall.lnk"),
        };

        var cleaned = StartMenuFilter.Clean(entries);

        Assert.Equal(new[] { "Dolphin", "RetroArch" }, cleaned.Select(e => e.Name));
    }
}
