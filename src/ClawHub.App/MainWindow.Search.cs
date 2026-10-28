using ClawHub.Core.Models;
using ClawHub.Core.Services;
using Microsoft.UI.Xaml;

namespace ClawHub.App;

// Ricerca: il tasto View apre la tastiera e la griglia mostra i risultati mentre si scrive.
public sealed partial class MainWindow
{
    private const double HeaderHeight = 150;

    /// <summary>Testo cercato; null quando la ricerca non è attiva.</summary>
    private string? _search;

    private void StartSearch()
    {
        if (_tab != Tab.Library || _detailOpen)
            return;

        _search ??= string.Empty;
        _zone = Zone.Grid;
        _gameIndex = 0;
        // Durante la scrittura l'intestazione lascia spazio ai risultati sopra la tastiera.
        HeaderRow.Height = new GridLength(0);
        HeaderPanel.Visibility = Visibility.Collapsed;
        ShowSection(null);

        AskText(App.Strings["Search.Title"], _search, 60,
            done: text =>
            {
                _search = text;
                ShowHeader();
                if (text.Trim().Length == 0)
                    EndSearch();
                else
                    ShowSection(null);
            },
            cancel: EndSearch,
            changed: text =>
            {
                _search = text;
                _gameIndex = 0;
                ShowSection(null);
            });
    }

    private void EndSearch()
    {
        _search = null;
        _gameIndex = 0;
        ShowHeader();
        ShowSection(null);
    }

    private void ShowHeader()
    {
        HeaderRow.Height = new GridLength(HeaderHeight);
        HeaderPanel.Visibility = Visibility.Visible;
    }

    /// <summary>Cerca tra tutti i giochi visibili (la prima sezione è sempre "Tutti i giochi").</summary>
    private IReadOnlyList<Game> SearchResults() =>
        _sections.Count == 0
            ? []
            : _sections[0].Games.Where(g => SearchText.Matches(g.Name, _search ?? string.Empty)).ToList();
}
