namespace ClawHub.Core.Models;

/// <summary>Dati che appartengono all'utente e sopravvivono alle scansioni.</summary>
public sealed record GameUserData
{
    public static readonly GameUserData Empty = new();

    public bool IsFavorite { get; init; }
    public bool IsHidden { get; init; }
    public string? CustomName { get; init; }
    public DateTimeOffset? LastPlayed { get; init; }
    public int PlayCount { get; init; }
    public IReadOnlyCollection<string> CollectionIds { get; init; } = [];

    /// <summary>Immagini scelte o scaricate per questo gioco; hanno la precedenza su quelle dello store.</summary>
    public ArtworkSet Artwork { get; init; } = ArtworkSet.Empty;
}

/// <summary>Categoria personalizzata creata dall'utente.</summary>
public sealed record GameCollection(string Id, string Name, int Order);

/// <summary>Gioco aggiunto a mano (eseguibile, emulatore, collegamento).</summary>
public sealed record ManualGameEntry(
    string Id,
    string Name,
    string ExecutablePath,
    string? Arguments = null,
    string? WorkingDirectory = null);

/// <summary>Tutto ciò che viene salvato in <c>library.json</c>.</summary>
public sealed record UserLibrary
{
    public int SchemaVersion { get; init; } = 1;
    public Dictionary<string, GameUserData> Games { get; init; } = [];
    public List<GameCollection> Collections { get; init; } = [];
    public List<ManualGameEntry> ManualGames { get; init; } = [];

    /// <summary>Ultima scansione, così all'avvio la libreria appare subito, prima della nuova scansione.</summary>
    public List<DetectedGame> LastScan { get; init; } = [];
}
