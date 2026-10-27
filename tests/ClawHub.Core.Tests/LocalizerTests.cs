using System.Text.Json;
using ClawHub.Core.Localization;

namespace ClawHub.Core.Tests;

public class LocalizerTests
{
    [Fact]
    public void SwitchesLanguageAtRuntime()
    {
        var localizer = new Localizer("en");
        var changed = false;
        localizer.LanguageChanged += (_, _) => changed = true;

        localizer.SetLanguage("it");

        Assert.True(changed);
        Assert.Equal("Gioca", localizer["Game.Play"]);
    }

    [Theory]
    [InlineData("it-IT", "it")]
    [InlineData("en", "en")]
    [InlineData("de", "en")]
    public void ResolvesUnsupportedLanguagesToEnglish(string requested, string expected)
    {
        Assert.Equal(expected, Localizer.Resolve(requested));
    }

    [Fact]
    public void UnknownKeyReturnsTheKey()
    {
        Assert.Equal("Missing.Key", new Localizer("it")["Missing.Key"]);
    }

    [Fact]
    public void AllLanguagesHaveTheSameKeys()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "ClawHub.Core", "Localization", "Strings");
        var english = Keys(Path.Combine(root, "en.json"));

        foreach (var language in Localizer.SupportedLanguages)
            Assert.Equal(english, Keys(Path.Combine(root, $"{language}.json")));
    }

    private static SortedSet<string> Keys(string path) =>
        new(JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!.Keys);
}
