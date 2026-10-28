using System.Globalization;
using System.Numerics;
using ClawHub.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace ClawHub.App.Views;

/// <summary>Copertina della griglia. Il contenuto arriva dal DataContext impostato da ItemsRepeater.</summary>
public sealed partial class GameCard : UserControl
{
    private static readonly Vector3 Raised = new(1.08f, 1.08f, 1f);

    public GameCard()
    {
        InitializeComponent();
        CenterPoint = new Vector3(75, 112, 0);
        ScaleTransition = new Vector3Transition { Duration = TimeSpan.FromMilliseconds(160) };
        FocusRing.OpacityTransition = new ScalarTransition { Duration = TimeSpan.FromMilliseconds(160) };
        DataContextChanged += (_, e) => Show(e.NewValue as GameTile);
    }

    private void Show(GameTile? tile)
    {
        FallbackTitle.Text = tile?.Name.ToUpper(CultureInfo.CurrentCulture) ?? string.Empty;
        NameText.Text = tile?.Name ?? string.Empty;
        CoverBrush.ImageSource = tile?.Cover;
        FavoriteStar.Visibility = tile?.IsFavorite == true ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <param name="selected">È il gioco selezionato.</param>
    /// <param name="active">La griglia ha il controllo (non la barra laterale): la selezione va messa in risalto.</param>
    public void SetHighlight(bool selected, bool active)
    {
        Scale = selected && active ? Raised : Vector3.One;
        FocusRing.Opacity = selected ? (active ? 1 : 0.45) : 0;
        NameText.Foreground = (Brush)Application.Current.Resources[selected ? "TextBrush" : "MutedBrush"];
    }
}
