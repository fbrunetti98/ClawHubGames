using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClawHub.Core.Storage;

/// <summary>Salva e carica un oggetto in un file JSON, con scrittura atomica (file temporaneo + rename).</summary>
public sealed class JsonFileStore<T>(string filePath) where T : class, new()
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly SemaphoreSlim _lock = new(1, 1);

    public string FilePath { get; } = filePath;

    public async Task<T> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath))
            return new T();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var stream = File.OpenRead(FilePath);
            return await JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken) ?? new T();
        }
        catch (JsonException)
        {
            // File rovinato: lo teniamo da parte invece di perdere i dati dell'utente in silenzio.
            File.Copy(FilePath, FilePath + ".corrupt", overwrite: true);
            return new T();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(T value, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var tempPath = FilePath + ".tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, value, Options, cancellationToken);
            }
            File.Move(tempPath, FilePath, overwrite: true);
        }
        finally
        {
            _lock.Release();
        }
    }
}
