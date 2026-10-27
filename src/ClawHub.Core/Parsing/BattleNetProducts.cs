namespace ClawHub.Core.Parsing;

/// <summary>
/// Codici prodotto di Battle.net, usati con <c>Battle.net.exe --exec="launch CODICE"</c>.
/// Il nome è quello che Battle.net scrive nelle voci di disinstallazione di Windows.
/// </summary>
public static class BattleNetProducts
{
    private static readonly (string NamePrefix, string Code)[] Known =
    [
        ("World of Warcraft", "WoW"),
        ("Diablo IV", "Fen"),
        ("Diablo III", "D3"),
        ("Diablo II: Resurrected", "OSI"),
        ("Diablo Immortal", "ANBS"),
        ("Overwatch", "Pro"),
        ("Hearthstone", "WTCG"),
        ("StarCraft II", "S2"),
        ("StarCraft", "S1"),
        ("Heroes of the Storm", "Hero"),
        ("Warcraft III", "W3"),
    ];

    /// <summary>Codice prodotto per il nome mostrato, o null se il gioco non è noto.</summary>
    public static string? FindCode(string displayName)
    {
        // I prefissi più lunghi vanno provati prima ("Diablo III" prima di "Diablo II…", "StarCraft II" prima di "StarCraft").
        foreach (var (prefix, code) in Known.OrderByDescending(k => k.NamePrefix.Length))
        {
            if (displayName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return code;
        }
        return null;
    }
}
