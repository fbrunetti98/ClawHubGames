namespace ClawHub.Core.Models;

/// <summary>Un gioco trovato da un provider (o aggiunto a mano), prima dei dati utente.</summary>
public sealed record DetectedGame
{
    public required GameStore Store { get; init; }

    /// <summary>Identificativo nello store (appid Steam, AppName Epic, AUMID Xbox…). Stabile tra le scansioni.</summary>
    public required string StoreId { get; init; }

    public required string Name { get; init; }

    public required LaunchInfo Launch { get; init; }

    public string? InstallDirectory { get; init; }

    /// <summary>Immagini già disponibili localmente o via URL noto (es. CDN Steam), prima di SteamGridDB.</summary>
    public ArtworkSet KnownArtwork { get; init; } = ArtworkSet.Empty;

    /// <summary>Ultima partita secondo lo store, se lo store la espone.</summary>
    public DateTimeOffset? StoreLastPlayed { get; init; }

    public string Id => GameId.Create(Store, StoreId);
}

/// <summary>Gioco come lo vede la UI: dati dello store uniti ai dati dell'utente.</summary>
public sealed record Game
{
    public required DetectedGame Source { get; init; }
    public required GameUserData UserData { get; init; }

    public string Id => Source.Id;
    public GameStore Store => Source.Store;
    public string Name => string.IsNullOrWhiteSpace(UserData.CustomName) ? Source.Name : UserData.CustomName!;
    public bool IsFavorite => UserData.IsFavorite;
    public bool IsHidden => UserData.IsHidden;
    public IReadOnlyCollection<string> CollectionIds => UserData.CollectionIds;
    public DateTimeOffset? LastPlayed => Max(UserData.LastPlayed, Source.StoreLastPlayed);
    public TimeSpan PlayTime => TimeSpan.FromSeconds(UserData.PlayTimeSeconds);

    private static DateTimeOffset? Max(DateTimeOffset? a, DateTimeOffset? b) =>
        a is null ? b : b is null ? a : (a > b ? a : b);
}

public static class GameId
{
    public static string Create(GameStore store, string storeId) => $"{store.ToString().ToLowerInvariant()}:{storeId}";
}
