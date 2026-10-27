using ClawHub.Core.Models;

namespace ClawHub.Core.Services;

public enum LibrarySort
{
    Name,
    LastPlayed,
}

/// <summary>Filtri della libreria. Tutti i criteri impostati devono essere soddisfatti.</summary>
public sealed record LibraryQuery
{
    public static readonly LibraryQuery All = new();

    public string? Search { get; init; }
    public GameStore? Store { get; init; }
    public string? CollectionId { get; init; }
    public bool FavoritesOnly { get; init; }
    public bool IncludeHidden { get; init; }
    public LibrarySort Sort { get; init; } = LibrarySort.Name;
}

public sealed record ProviderScanError(GameStore Store, string Message);

public sealed record ScanReport(int GameCount, IReadOnlyList<ProviderScanError> Errors);
