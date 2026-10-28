using ClawHub.Core.Models;
using Microsoft.UI.Xaml.Media;

namespace ClawHub.App.ViewModels;

/// <summary>Elemento della griglia della libreria.</summary>
public sealed class GameTile(Game game)
{
    private readonly ArtworkSet _artwork = game.UserData.Artwork.Or(game.Source.KnownArtwork);

    public Game Game { get; } = game;
    public string Name => Game.Name;
    public bool IsFavorite => Game.IsFavorite;

    public ImageSource? Cover => ImageCache.Get(_artwork.Cover, 300);

    public string? CoverPath => _artwork.Cover;
    public string? HeroPath => _artwork.Hero;
    public string? LogoPath => _artwork.Logo;

    /// <summary>Immagine per lo sfondo: quella orizzontale se c'è, altrimenti la copertina.</summary>
    public string? BackdropPath => _artwork.Hero ?? _artwork.Cover;
}
