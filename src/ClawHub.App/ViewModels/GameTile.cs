using ClawHub.Core.Models;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ClawHub.App.ViewModels;

/// <summary>Elemento della griglia della libreria.</summary>
public sealed class GameTile(Game game)
{
    public Game Game { get; } = game;
    public string Name => Game.Name;
    public string StoreName => App.Strings[$"Store.{Game.Store}"];

    public ImageSource? Cover { get; } = CreateImage(game.UserData.Artwork.Or(game.Source.KnownArtwork).Cover);

    private static ImageSource? CreateImage(string? pathOrUrl) =>
        Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var uri) ? new BitmapImage(uri) : null;
}
