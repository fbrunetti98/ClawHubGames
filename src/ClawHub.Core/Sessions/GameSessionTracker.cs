using ClawHub.Core.Models;

namespace ClawHub.Core.Sessions;

/// <summary>Processo in esecuzione: id e percorso completo dell'eseguibile.</summary>
public sealed record RunningProcess(int Id, string Path);

/// <summary>Partita in corso: gioco, inizio e processi che le appartengono.</summary>
public sealed record GameSession(string GameId, DateTimeOffset Started, IReadOnlyList<int> ProcessIds);

public sealed record EndedSession(GameSession Session, TimeSpan Duration);

public sealed record SessionChanges(IReadOnlyList<GameSession> Started, IReadOnlyList<EndedSession> Ended)
{
    public bool IsEmpty => Started.Count == 0 && Ended.Count == 0;
}

/// <summary>
/// Riconosce le partite dai processi in esecuzione: un gioco è aperto se gira un eseguibile dalla sua
/// cartella di installazione. Funziona per ogni store e anche per i giochi avviati fuori dal launcher.
/// Una partita finisce dopo <c>missesBeforeEnd</c> controlli di fila senza processi, così i giochi che
/// passano da un eseguibile all'altro (launcher → gioco) non vengono spezzati.
/// </summary>
public sealed class GameSessionTracker(int missesBeforeEnd = 2)
{
    private sealed class Active(GameSession session, DateTimeOffset lastSeen)
    {
        public GameSession Session { get; set; } = session;
        public DateTimeOffset LastSeen { get; set; } = lastSeen;
        public int Misses { get; set; }
    }

    private readonly Dictionary<string, Active> _active = [];

    public IReadOnlyList<GameSession> ActiveSessions => _active.Values.Select(a => a.Session).ToList();

    public GameSession? Find(string gameId) => _active.TryGetValue(gameId, out var active) ? active.Session : null;

    public SessionChanges Update(IEnumerable<Game> games, IEnumerable<RunningProcess> processes, DateTimeOffset now)
    {
        var folders = games
            .Select(g => (g.Id, Folder: NormalizeFolder(g.Source.InstallDirectory)))
            .Where(x => x.Folder is not null)
            .ToList();

        // Ogni processo appartiene al gioco con la cartella più specifica che lo contiene.
        var byGame = new Dictionary<string, List<int>>();
        foreach (var process in processes)
        {
            var path = Normalize(process.Path);
            var owner = folders
                .Where(f => path.StartsWith(f.Folder + "\\", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.Folder!.Length)
                .Select(f => f.Id)
                .FirstOrDefault();
            if (owner is null)
                continue;
            if (!byGame.TryGetValue(owner, out var ids))
                byGame[owner] = ids = [];
            ids.Add(process.Id);
        }

        var started = new List<GameSession>();
        foreach (var (gameId, ids) in byGame)
        {
            if (_active.TryGetValue(gameId, out var active))
            {
                active.Session = active.Session with { ProcessIds = ids };
                active.LastSeen = now;
                active.Misses = 0;
            }
            else
            {
                var session = new GameSession(gameId, now, ids);
                _active[gameId] = new Active(session, now);
                started.Add(session);
            }
        }

        var ended = new List<EndedSession>();
        foreach (var (gameId, active) in _active.Where(kv => !byGame.ContainsKey(kv.Key)).ToList())
        {
            if (++active.Misses < missesBeforeEnd)
                continue;
            _active.Remove(gameId);
            ended.Add(new EndedSession(active.Session, active.LastSeen - active.Session.Started));
        }

        return new SessionChanges(started, ended);
    }

    /// <summary>
    /// Cartella utilizzabile per riconoscere un gioco, o null. Servono almeno due livelli sotto il disco
    /// ("D:\SteamLibrary\Titanfall2"): cartelle generiche come "C:\Giochi" prenderebbero processi che non c'entrano.
    /// </summary>
    public static string? NormalizeFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return null;
        var normalized = Normalize(folder.Trim().Trim('"'));
        return normalized.Split('\\', StringSplitOptions.RemoveEmptyEntries).Length >= 3 ? normalized : null;
    }

    private static string Normalize(string path) => path.Replace('/', '\\').TrimEnd('\\');
}
