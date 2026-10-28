using ClawHub.Core.Models;

namespace ClawHub.Core.Services;

public enum LibrarySectionKind
{
    All,
    Recent,
    Favorites,
    Collection,
    Store,
}

/// <summary>Voce della barra laterale. <see cref="Key"/> è stabile tra un aggiornamento e l'altro.</summary>
public sealed record LibrarySection(
    LibrarySectionKind Kind,
    string Key,
    IReadOnlyList<Game> Games,
    GameCollection? Collection = null,
    GameStore? Store = null);

public static class LibrarySections
{
    /// <summary>
    /// Tutti i giochi, giocati di recente, preferiti, una voce per collezione e una per store.
    /// Le voci vuote vengono saltate, tranne "Tutti i giochi".
    /// </summary>
    public static IReadOnlyList<LibrarySection> Build(IEnumerable<Game> games, IEnumerable<GameCollection> collections, int recentLimit = 12)
    {
        var byName = games.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

        var sections = new List<LibrarySection>
        {
            new(LibrarySectionKind.All, "all", byName),
            new(LibrarySectionKind.Recent, "recent", byName
                .Where(g => g.LastPlayed is not null)
                .OrderByDescending(g => g.LastPlayed)
                .Take(recentLimit)
                .ToList()),
            new(LibrarySectionKind.Favorites, "favorites", byName.Where(g => g.IsFavorite).ToList()),
        };

        sections.AddRange(collections
            .OrderBy(c => c.Order)
            .Select(c => new LibrarySection(LibrarySectionKind.Collection, "collection:" + c.Id,
                byName.Where(g => g.CollectionIds.Contains(c.Id)).ToList(), Collection: c)));

        sections.AddRange(byName
            .GroupBy(g => g.Store)
            .OrderBy(g => g.Key)
            .Select(g => new LibrarySection(LibrarySectionKind.Store, "store:" + g.Key, g.ToList(), Store: g.Key)));

        return sections.Where(s => s.Kind == LibrarySectionKind.All || s.Games.Count > 0).ToList();
    }
}
