namespace ClawHub.Core.Models;

public enum ArtworkKind
{
    /// <summary>Copertina verticale (600x900).</summary>
    Cover,

    /// <summary>Immagine orizzontale grande per lo sfondo della pagina di dettaglio.</summary>
    Hero,

    /// <summary>Logo trasparente del gioco.</summary>
    Logo,

    Icon,
}

/// <summary>Riferimenti (percorso locale o URL) alle immagini di un gioco.</summary>
public sealed record ArtworkSet(string? Cover = null, string? Hero = null, string? Logo = null, string? Icon = null)
{
    public static readonly ArtworkSet Empty = new();

    public string? Get(ArtworkKind kind) => kind switch
    {
        ArtworkKind.Cover => Cover,
        ArtworkKind.Hero => Hero,
        ArtworkKind.Logo => Logo,
        ArtworkKind.Icon => Icon,
        _ => null,
    };

    public ArtworkSet With(ArtworkKind kind, string? value) => kind switch
    {
        ArtworkKind.Cover => this with { Cover = value },
        ArtworkKind.Hero => this with { Hero = value },
        ArtworkKind.Logo => this with { Logo = value },
        ArtworkKind.Icon => this with { Icon = value },
        _ => this,
    };

    /// <summary>Completa i campi mancanti con quelli di <paramref name="fallback"/>.</summary>
    public ArtworkSet Or(ArtworkSet fallback) => new(
        Cover ?? fallback.Cover,
        Hero ?? fallback.Hero,
        Logo ?? fallback.Logo,
        Icon ?? fallback.Icon);
}
