using ClawHub.App.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace ClawHub.App.Views;

/// <summary>
/// Tastiera a schermo per il controller: griglia QWERTY con riga dei numeri e pagina dei simboli.
/// A scrive il tasto selezionato, B cancella, X spazio, Y maiuscole, Menu conferma, View annulla.
/// </summary>
public sealed partial class OnScreenKeyboard : UserControl
{
    private const string Shift = "⇧";
    private const string Backspace = "⌫";
    private const string Symbols = "?123";
    private const string Letters = "ABC";
    private const string Space = " ";
    private const string Done = "↵";
    private const double KeyUnit = 62;
    private const double KeyGap = 6;

    private static readonly string[][] LetterRows =
    [
        ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0"],
        ["q", "w", "e", "r", "t", "y", "u", "i", "o", "p"],
        ["a", "s", "d", "f", "g", "h", "j", "k", "l", "'"],
        [Shift, "z", "x", "c", "v", "b", "n", "m", "-", Backspace],
        [Symbols, ",", Space, ".", Done],
    ];

    private static readonly string[][] SymbolRows =
    [
        ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0"],
        ["@", "#", "€", "&", "*", "(", ")", "_", "+", "="],
        ["!", "\"", ":", ";", "/", "?", "%", "<", ">", "\\"],
        [Shift, "[", "]", "{", "}", "|", "~", "^", "$", Backspace],
        [Letters, ",", Space, ".", Done],
    ];

    private readonly List<List<(string Key, Border View, double Center)>> _keys = [];
    private Action<string>? _onDone;
    private Action? _onCancel;
    private Action<string>? _onChanged;
    private bool _symbols;
    private bool _shift;
    private int _row = 1;
    private int _column;

    public OnScreenKeyboard()
    {
        InitializeComponent();
        Input.TextChanged += (_, _) => _onChanged?.Invoke(Input.Text);
    }

    public bool IsOpen { get; private set; }

    public void Open(string title, string text, int maxLength, Action<string> onDone, Action? onCancel = null, Action<string>? onChanged = null)
    {
        _onDone = onDone;
        _onCancel = onCancel;
        _onChanged = null; // Il testo iniziale non è una modifica.
        TitleText.Text = title.ToUpperInvariant();
        Input.MaxLength = maxLength;
        Input.Text = text;
        Input.SelectionStart = text.Length;
        _onChanged = onChanged;
        _symbols = false;
        _shift = false;
        _row = 1;
        _column = 0;
        IsOpen = true;
        Visibility = Visibility.Visible;
        BuildKeys();
        Input.Focus(FocusState.Programmatic);
    }

    public void Close()
    {
        IsOpen = false;
        Visibility = Visibility.Collapsed;
        _onDone = null;
        _onCancel = null;
        _onChanged = null;
    }

    /// <summary>Tasti del controller mentre la tastiera è aperta.</summary>
    public void Handle(PadButton button)
    {
        switch (button)
        {
            case PadButton.Left: MoveColumn(-1); break;
            case PadButton.Right: MoveColumn(1); break;
            case PadButton.Up: MoveRow(-1); break;
            case PadButton.Down: MoveRow(1); break;
            case PadButton.A: Press(_keys[_row][_column].Key); break;
            case PadButton.B: Press(Backspace); break;
            case PadButton.X: Press(Space); break;
            case PadButton.Y: Press(Shift); break;
            case PadButton.Menu: Confirm(); break;
            case PadButton.View: Cancel(); break;
        }
    }

    public void Confirm()
    {
        var done = _onDone;
        var text = Input.Text;
        Close();
        done?.Invoke(text);
    }

    public void Cancel()
    {
        var cancel = _onCancel;
        Close();
        cancel?.Invoke();
    }

    private void Press(string key)
    {
        switch (key)
        {
            case Shift:
                _shift = !_shift;
                BuildKeys();
                return;
            case Symbols:
            case Letters:
                _symbols = key == Symbols;
                _shift = false;
                BuildKeys();
                return;
            case Done:
                Confirm();
                return;
            case Backspace:
                DeleteBeforeCaret();
                return;
        }

        Insert(_shift ? key.ToUpperInvariant() : key);
        if (_shift)
        {
            _shift = false; // Maiuscola per un solo carattere, come sui telefoni.
            BuildKeys();
        }
    }

    private void Insert(string value)
    {
        var text = Input.Text;
        var caret = Math.Clamp(Input.SelectionStart, 0, text.Length);
        var selected = Math.Min(Input.SelectionLength, text.Length - caret);
        if (Input.MaxLength > 0 && text.Length - selected + value.Length > Input.MaxLength)
            return;
        Input.Text = text[..caret] + value + text[(caret + selected)..];
        Input.SelectionStart = caret + value.Length;
    }

    private void DeleteBeforeCaret()
    {
        var text = Input.Text;
        var caret = Math.Clamp(Input.SelectionStart, 0, text.Length);
        if (Input.SelectionLength > 0)
        {
            Input.Text = text.Remove(caret, Math.Min(Input.SelectionLength, text.Length - caret));
            Input.SelectionStart = caret;
        }
        else if (caret > 0)
        {
            Input.Text = text.Remove(caret - 1, 1);
            Input.SelectionStart = caret - 1;
        }
    }

    private void MoveColumn(int delta)
    {
        _column = Math.Clamp(_column + delta, 0, _keys[_row].Count - 1);
        UpdateHighlight();
    }

    /// <summary>Cambiando riga si va al tasto più vicino in orizzontale.</summary>
    private void MoveRow(int delta)
    {
        var target = Math.Clamp(_row + delta, 0, _keys.Count - 1);
        if (target == _row)
            return;
        var center = _keys[_row][_column].Center;
        _column = _keys[target]
            .Select((k, i) => (Distance: Math.Abs(k.Center - center), Index: i))
            .MinBy(k => k.Distance).Index;
        _row = target;
        UpdateHighlight();
    }

    private void BuildKeys()
    {
        Rows.Children.Clear();
        _keys.Clear();

        var rows = _symbols ? SymbolRows : LetterRows;
        for (var r = 0; r < rows.Length; r++)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = KeyGap, HorizontalAlignment = HorizontalAlignment.Center };
            var keys = new List<(string, Border, double)>();
            var rowWidth = rows[r].Sum(k => Width(k)) + KeyGap * (rows[r].Length - 1);
            var x = -rowWidth / 2;
            for (var c = 0; c < rows[r].Length; c++)
            {
                var key = rows[r][c];
                var width = Width(key);
                var view = KeyView(key, width);
                var row = r;
                var column = c;
                view.Tapped += (_, _) =>
                {
                    _row = row;
                    _column = column;
                    UpdateHighlight();
                    Press(key);
                };
                panel.Children.Add(view);
                keys.Add((key, view, x + width / 2));
                x += width + KeyGap;
            }
            _keys.Add(keys);
            Rows.Children.Add(panel);
        }

        _row = Math.Clamp(_row, 0, _keys.Count - 1);
        _column = Math.Clamp(_column, 0, _keys[_row].Count - 1);
        UpdateHighlight();
    }

    private Border KeyView(string key, double width)
    {
        var label = key switch
        {
            Space => App.Strings["Keyboard.Space"],
            Done => App.Strings["Keyboard.Done"],
            _ when _shift && key.Length == 1 && char.IsLetter(key[0]) => key.ToUpperInvariant(),
            _ => key,
        };
        return new Border
        {
            Width = width,
            Height = 44,
            CornerRadius = new CornerRadius(8),
            Child = new TextBlock
            {
                Text = label,
                FontSize = key.Length == 1 && key != Space ? 19 : 15,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
    }

    private void UpdateHighlight()
    {
        for (var r = 0; r < _keys.Count; r++)
        {
            for (var c = 0; c < _keys[r].Count; c++)
            {
                var (key, view, _) = _keys[r][c];
                var focused = r == _row && c == _column;
                var special = key.Length > 1 || key is Shift or Backspace or Done;
                var shiftOn = key == Shift && _shift;
                view.Background = focused
                    ? Res("AccentBrush")
                    : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(shiftOn ? (byte)0x33 : special ? (byte)0x0A : (byte)0x14, 255, 255, 255));
                ((TextBlock)view.Child).Foreground = Res(focused ? "AccentInkBrush" : special && !shiftOn ? "MutedBrush" : "TextBrush");
            }
        }
    }

    private static double Width(string key) => key switch
    {
        Space => KeyUnit * 5 + KeyGap * 4,
        Done => KeyUnit * 2 + KeyGap,
        Symbols or Letters => KeyUnit * 1.5,
        _ => KeyUnit,
    };

    private static Brush Res(string key) => (Brush)Application.Current.Resources[key];
}
