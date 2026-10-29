namespace ClawHub.Core.Models;

public sealed record AppSettings
{
    /// <summary>Codice lingua dell'interfaccia (<c>it</c>, <c>en</c>) oppure <c>system</c> per seguire Windows.</summary>
    public string Language { get; init; } = "system";

    /// <summary>Chiave personale SteamGridDB, inserita dall'utente. Vuota = nessun download da SteamGridDB.</summary>
    public string? SteamGridDbApiKey { get; init; }

    /// <summary>Store da non scansionare.</summary>
    public HashSet<GameStore> DisabledStores { get; init; } = [];

    /// <summary>Mostra i giochi nascosti nella libreria.</summary>
    public bool ShowHiddenGames { get; init; }

    /// <summary>Aggiornamenti automatici anche verso le versioni di anteprima (alpha/beta).</summary>
    public bool ReceivePreviewUpdates { get; init; } = true;
}
