using System.Globalization;
using System.Text;

namespace ClawHub.Core.Services;

/// <summary>Normalizzazione per la ricerca: niente maiuscole, accenti o punteggiatura ("Pokémon: Legends" ≈ "pokemon legends").</summary>
public static class SearchText
{
    public static string Normalize(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>True se ogni parola della ricerca compare nel nome (in qualsiasi ordine).</summary>
    public static bool Matches(string name, string query)
    {
        var terms = Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0)
            return true;

        var normalizedName = Normalize(name);
        var compactName = normalizedName.Replace(" ", string.Empty);
        // Confronto anche senza spazi, così "halflife" trova "Half-Life".
        return terms.All(t => normalizedName.Contains(t, StringComparison.Ordinal) || compactName.Contains(t, StringComparison.Ordinal));
    }
}
