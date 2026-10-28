using ClawHub.Core.Models;
using ClawHub.Core.Services;

namespace ClawHub.Core.Artwork;

/// <summary>Scarica in background copertina e sfondo dei giochi che non li hanno ancora in cache.</summary>
public sealed class ArtworkUpdater(LibraryService library, ArtworkService artwork)
{
    private readonly SemaphoreSlim _running = new(1, 1);

    /// <summary>Un solo giro alla volta: se ne è già in corso uno, questa chiamata non fa nulla.</summary>
    public async Task UpdateMissingAsync(string? steamGridDbApiKey, CancellationToken cancellationToken = default)
    {
        if (!await _running.WaitAsync(0, cancellationToken))
            return;
        try
        {
            var updates = new Dictionary<string, ArtworkSet>();
            foreach (var game in library.Games.Where(NeedsArtwork))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var resolved = await artwork.ResolveAsync(game, steamGridDbApiKey, cancellationToken);
                    if (resolved != game.UserData.Artwork)
                        updates[game.Id] = resolved;
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    // Chiave non valida, rete assente, risposta inattesa o file bloccato: si riprova al prossimo avvio.
                }

                // Salva a blocchi, così le copertine compaiono mentre si scaricano.
                if (updates.Count >= 8)
                {
                    await library.SetArtworkAsync(updates);
                    updates.Clear();
                }
            }
            if (updates.Count > 0)
                await library.SetArtworkAsync(updates);
        }
        finally
        {
            _running.Release();
        }
    }

    private static bool NeedsArtwork(Game game) =>
        !Exists(game.UserData.Artwork.Cover) || !Exists(game.UserData.Artwork.Hero);

    private static bool Exists(string? path) => path is not null && File.Exists(path);
}
