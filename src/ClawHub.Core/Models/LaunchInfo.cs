namespace ClawHub.Core.Models;

public enum LaunchKind
{
    /// <summary>Protocollo dello store, es. <c>steam://rungameid/440</c>.</summary>
    Uri,

    /// <summary>Eseguibile lanciato direttamente.</summary>
    Executable,

    /// <summary>App pacchettizzata avviata tramite AUMID (<c>shell:AppsFolder\…</c>).</summary>
    PackagedApp,
}

/// <summary>Come avviare un gioco. Interpretato dal launcher specifico della piattaforma.</summary>
public sealed record LaunchInfo(
    LaunchKind Kind,
    string Target,
    string? Arguments = null,
    string? WorkingDirectory = null)
{
    public static LaunchInfo FromUri(string uri) => new(LaunchKind.Uri, uri);

    public static LaunchInfo FromExecutable(string path, string? arguments = null, string? workingDirectory = null) =>
        new(LaunchKind.Executable, path, arguments, workingDirectory ?? Path.GetDirectoryName(path));

    public static LaunchInfo FromPackagedApp(string appUserModelId) => new(LaunchKind.PackagedApp, appUserModelId);
}
