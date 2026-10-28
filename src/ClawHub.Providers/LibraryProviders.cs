using ClawHub.Core.Abstractions;
using ClawHub.Providers.Stores;

namespace ClawHub.Providers;

public static class LibraryProviders
{
    /// <summary>Tutti i provider disponibili su Windows. I giochi manuali sono gestiti da <c>LibraryService</c>.</summary>
    public static IReadOnlyList<ILibraryProvider> CreateAll() =>
    [
        new SteamProvider(),
        new EpicProvider(),
        new GogProvider(),
        new XboxProvider(),
        new EaProvider(),
        new UbisoftProvider(),
        new BattleNetProvider(),
        new RockstarProvider(),
    ];
}
