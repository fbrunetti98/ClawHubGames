using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ClawHub.Core.Models;

namespace ClawHub.Core.Artwork;

/// <summary>Client minimale per l'API v2 di SteamGridDB (https://www.steamgriddb.com/api/v2).</summary>
public sealed class SteamGridDbClient(HttpClient http, string apiKey)
{
    private const string BaseUrl = "https://www.steamgriddb.com/api/v2/";

    /// <summary>Id SteamGridDB del gioco: tramite appid per Steam, altrimenti cercando per nome.</summary>
    public async Task<long?> FindGameIdAsync(DetectedGame game, CancellationToken cancellationToken = default)
    {
        if (game.Store == GameStore.Steam)
        {
            using var steamDoc = await GetAsync($"games/steam/{Uri.EscapeDataString(game.StoreId)}", cancellationToken);
            if (steamDoc?.RootElement.GetProperty("data") is { ValueKind: JsonValueKind.Object } data
                && data.TryGetProperty("id", out var id))
                return id.GetInt64();
        }

        using var doc = await GetAsync($"search/autocomplete/{Uri.EscapeDataString(game.Name)}", cancellationToken);
        if (doc?.RootElement.GetProperty("data") is { ValueKind: JsonValueKind.Array } results)
        {
            foreach (var result in results.EnumerateArray())
                return result.GetProperty("id").GetInt64();
        }
        return null;
    }

    /// <summary>
    /// Controlla la chiave con una richiesta leggera. Falso se SteamGridDB la rifiuta;
    /// gli errori di rete vengono rilanciati (la chiave potrebbe essere giusta).
    /// </summary>
    public async Task<bool> IsKeyValidAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var _ = await GetAsync("games/steam/220", cancellationToken);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>URL della prima immagine disponibile del tipo richiesto, o null.</summary>
    public async Task<string?> GetImageUrlAsync(long gameId, ArtworkKind kind, CancellationToken cancellationToken = default)
    {
        var path = kind switch
        {
            ArtworkKind.Cover => $"grids/game/{gameId}?dimensions=600x900",
            ArtworkKind.Hero => $"heroes/game/{gameId}",
            ArtworkKind.Logo => $"logos/game/{gameId}",
            ArtworkKind.Icon => $"icons/game/{gameId}",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        using var doc = await GetAsync(path, cancellationToken);
        if (doc?.RootElement.GetProperty("data") is { ValueKind: JsonValueKind.Array } images)
        {
            foreach (var image in images.EnumerateArray())
            {
                if (image.TryGetProperty("url", out var url) && url.GetString() is { Length: > 0 } value)
                    return value;
            }
        }
        return null;
    }

    private async Task<JsonDocument?> GetAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + relativeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("SteamGridDB API key is not valid.");
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }
}
