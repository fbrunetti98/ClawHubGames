using System.Runtime.InteropServices;

namespace ClawHub.App;

/// <summary>Funzioni di Windows per mettere in primo piano le finestre (launcher e giochi).</summary>
internal static class NativeMethods
{
    public const int AsfwAny = -1;
    private const int SwRestore = 9;
    private const byte VkMenu = 0x12;
    private const uint KeyEventKeyUp = 0x0002;

    /// <summary>
    /// Porta una finestra in primo piano. Windows lo permette solo al processo attivo: se non basta,
    /// un tasto Alt simulato sblocca SetForegroundWindow (tecnica nota e usata da molti launcher).
    /// </summary>
    public static void BringToFront(IntPtr window)
    {
        if (window == IntPtr.Zero)
            return;
        if (IsIconic(window))
            ShowWindow(window, SwRestore);
        if (SetForegroundWindow(window) && GetForegroundWindow() == window)
            return;

        keybd_event(VkMenu, 0, 0, UIntPtr.Zero);
        keybd_event(VkMenu, 0, KeyEventKeyUp, UIntPtr.Zero);
        SetForegroundWindow(window);
    }

    [DllImport("user32.dll")]
    public static extern bool AllowSetForegroundWindow(int processId);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extraInfo);
}
