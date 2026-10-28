using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ClawHub.App.ViewModels;

/// <summary>
/// Riusa le immagini già caricate: la griglia viene ricostruita spesso (scansione, preferiti, copertine scaricate)
/// e senza cache ogni copertina lampeggerebbe. Da usare solo sul thread della UI.
/// </summary>
internal static class ImageCache
{
    private static readonly Dictionary<string, BitmapImage> Images = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Dimentica le immagini caricate (es. dopo "scarica di nuovo le copertine").</summary>
    public static void Clear() => Images.Clear();

    public static ImageSource? Get(string? pathOrUrl, int decodeWidth)
    {
        if (!Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var uri))
            return null;

        var key = decodeWidth + "|" + pathOrUrl;
        if (!Images.TryGetValue(key, out var image))
            Images[key] = image = new BitmapImage { DecodePixelWidth = decodeWidth, UriSource = uri };
        return image;
    }
}
