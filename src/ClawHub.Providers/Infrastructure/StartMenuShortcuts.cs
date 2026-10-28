using ClawHub.Core.Services;

namespace ClawHub.Providers.Infrastructure;

/// <summary>Collegamenti (.lnk) del menu Start, di tutti gli utenti e dell'utente corrente.</summary>
public static class StartMenuShortcuts
{
    public static IReadOnlyList<StartMenuEntry> Find()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
        };

        var entries = new List<StartMenuEntry>();
        foreach (var root in roots.Where(r => !string.IsNullOrEmpty(r) && Directory.Exists(r)))
        {
            try
            {
                var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
                entries.AddRange(Directory.EnumerateFiles(root, "*.lnk", options)
                    .Select(path => new StartMenuEntry(Path.GetFileNameWithoutExtension(path), path)));
            }
            catch (IOException)
            {
                // Cartella non leggibile: si usa quello che c'è.
            }
        }
        return StartMenuFilter.Clean(entries);
    }
}
