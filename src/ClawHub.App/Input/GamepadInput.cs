using Microsoft.UI.Dispatching;
using Windows.Gaming.Input;

namespace ClawHub.App.Input;

public enum PadButton
{
    Up,
    Down,
    Left,
    Right,
    A,
    B,
    X,
    Y,
    LeftShoulder,
    RightShoulder,
    Menu,
    View,
}

/// <summary>
/// Legge il controller (XInput, quindi anche quello integrato del Claw) circa 60 volte al secondo.
/// Le direzioni si ripetono tenendole premute, come la tastiera.
/// </summary>
public sealed class GamepadInput
{
    private const double StickThreshold = 0.6;
    private static readonly TimeSpan FirstRepeat = TimeSpan.FromMilliseconds(380);
    private static readonly TimeSpan NextRepeat = TimeSpan.FromMilliseconds(110);

    private static readonly (GamepadButtons Button, PadButton Action)[] Buttons =
    [
        (GamepadButtons.A, PadButton.A),
        (GamepadButtons.B, PadButton.B),
        (GamepadButtons.X, PadButton.X),
        (GamepadButtons.Y, PadButton.Y),
        (GamepadButtons.LeftShoulder, PadButton.LeftShoulder),
        (GamepadButtons.RightShoulder, PadButton.RightShoulder),
        (GamepadButtons.Menu, PadButton.Menu),
        (GamepadButtons.View, PadButton.View),
    ];

    private readonly DispatcherQueueTimer _timer;
    private GamepadButtons _previous;
    private PadButton? _heldDirection;
    private DateTime _nextRepeatAt;

    public GamepadInput(DispatcherQueue dispatcher)
    {
        // Senza un gestore registrato, in alcune app desktop Gamepad.Gamepads resta vuoto.
        Gamepad.GamepadAdded += (_, _) => { };

        _timer = dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(16);
        _timer.IsRepeating = true;
        _timer.Tick += (_, _) => Poll();
        _timer.Start();
    }

    public event Action<PadButton>? Pressed;

    /// <summary>Falso quando la finestra non è in primo piano: i tasti premuti in gioco non devono arrivare al launcher.</summary>
    public bool Enabled { get; set; } = true;

    private void Poll()
    {
        var pad = Gamepad.Gamepads.FirstOrDefault();
        if (pad is null)
            return;

        var reading = pad.GetCurrentReading();
        var buttons = reading.Buttons;
        if (!Enabled)
        {
            _previous = buttons;
            _heldDirection = null;
            return;
        }

        foreach (var (button, action) in Buttons)
        {
            if (buttons.HasFlag(button) && !_previous.HasFlag(button))
                Pressed?.Invoke(action);
        }
        _previous = buttons;

        var direction = Direction(buttons, reading.LeftThumbstickX, reading.LeftThumbstickY);
        var now = DateTime.UtcNow;
        if (direction is null)
        {
            _heldDirection = null;
        }
        else if (direction != _heldDirection)
        {
            _heldDirection = direction;
            _nextRepeatAt = now + FirstRepeat;
            Pressed?.Invoke(direction.Value);
        }
        else if (now >= _nextRepeatAt)
        {
            _nextRepeatAt = now + NextRepeat;
            Pressed?.Invoke(direction.Value);
        }
    }

    private static PadButton? Direction(GamepadButtons buttons, double x, double y)
    {
        // Sullo stick, Y positivo è verso l'alto.
        if (buttons.HasFlag(GamepadButtons.DPadUp) || y > StickThreshold)
            return PadButton.Up;
        if (buttons.HasFlag(GamepadButtons.DPadDown) || y < -StickThreshold)
            return PadButton.Down;
        if (buttons.HasFlag(GamepadButtons.DPadLeft) || x < -StickThreshold)
            return PadButton.Left;
        if (buttons.HasFlag(GamepadButtons.DPadRight) || x > StickThreshold)
            return PadButton.Right;
        return null;
    }
}
