using ClawHub.Core.Models;

namespace ClawHub.Core.Artwork;

/// <summary>
/// Procura le immagini di un gioco e le salva nella cache locale.
/// Ordine: immagini già scelte dall'utente → immagini note dello store → SteamGridDB (se c'è la chiave).
/// </summary>
public sealed class ArtworkService(HttpClient http, string cacheDirectory)
{
    private static readonly ArtworkKind[] AllKinds = Enum.GetValues<ArtworkKind>();

    public async Task<ArtworkSet> ResolveAsync(Game game, string? steamGridDbApiKey, CancellationToken cancellationToken = default)
    {
        var result = KeepExistingFiles(game.UserData.Artwork);
        var known = game.Source.KnownArtwork;

        foreach (var kind in AllKinds)
        {
            if (result.Get(kind) is not null || known.Get(kind) is not { } source)
                continue;
            result = result.With(kind, await TryCacheAsync(game.Id, kind, source, cancellationToken));
        }

        if (string.IsNullOrWhiteSpace(steamGridDbApiKey) || AllKinds.All(k => result.Get(k) is not null))
            return result;

        var sgdb = new SteamGridDbClient(http, steamGridDbApiKey);
        var sgdbId = await sgdb.FindGameIdAsync(game.Source, cancellationToken);
        if (sgdbId is null)
            return result;

        foreach (var kind in AllKinds.Where(k => result.Get(k) is null))
        {
            var url = await sgdb.GetImageUrlAsync(sgdbId.Value, kind, cancellationToken);
            if (url is not null)
                result = result.With(kind, await TryCacheAsync(game.Id, kind, url, cancellationToken));
        }
        return result;
    }

    /// <summary>Copia in cache un file locale o scarica un URL. Restituisce il percorso in cache, o null.</summary>
    private async Task<string?> TryCacheAsync(string gameId, ArtworkKind kind, string source, CancellationToken cancellationToken)
    {
        try
        {
            if (File.Exists(source))
                return source; // Già locale (es. cache di Steam): non serve copiarlo.

            if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                return null;

            var directory = Path.Combine(cacheDirectory, SafeFileName(gameId));
            Directory.CreateDirectory(directory);
            var extension = Path.GetExtension(uri.AbsolutePath) is { Length: > 1 and <= 5 } ext ? ext : ".img";
            var target = Path.Combine(directory, kind.ToString().ToLowerInvariant() + extension);

            using var response = await http.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            await using (var file = File.Create(target))
                await response.Content.CopyToAsync(file, cancellationToken);
            return target;
        }
        catch (HttpRequestException)
        {
            return null; // Offline o immagine non disponibile: si riproverà alla prossima occasione.
        }
    }

    private static ArtworkSet KeepExistingFiles(ArtworkSet set)
    {
        var result = ArtworkSet.Empty;
        foreach (var kind in AllKinds)
        {
            if (set.Get(kind) is { } path && File.Exists(path))
                result = result.With(kind, path);
        }
        return result;
    }

    private static string SafeFileName(string value) =>
        string.Concat(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ':' ? '_' : c));
}
