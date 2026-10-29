using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Velopack;

namespace ClawHub.App;

/// <summary>
/// Punto di avvio: prima Velopack (installazione, aggiornamenti già scaricati), poi l'istanza singola.
/// Se il launcher è già aperto (es. tasto MSI tramite ClawTweaks) gli si manda un segnale e si esce.
/// </summary>
public static class Program
{
    private const string MutexName = "ClawHubGames.SingleInstance";
    private const string ActivateEventName = "ClawHubGames.Activate";

    /// <summary>L'exe è stato riaperto mentre il launcher era già in esecuzione. Arriva da un thread in background.</summary>
    public static event Action? ActivationRequested;

    [STAThread]
    private static int Main(string[] args)
    {
        VelopackApp.Build().Run();

        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            SignalRunningInstance();
            return 0;
        }

        using var activate = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        new Thread(() =>
        {
            while (activate.WaitOne())
                ActivationRequested?.Invoke();
        })
        { IsBackground = true, Name = "Attivazione da un'altra istanza" }.Start();

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(callbackParams =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _ = new App();
        });
        GC.KeepAlive(mutex);
        return 0;
    }

    private static void SignalRunningInstance()
    {
        // Chi ha appena avviato l'exe può dare il primo piano: lo cede al launcher già aperto.
        NativeMethods.AllowSetForegroundWindow(NativeMethods.AsfwAny);
        if (EventWaitHandle.TryOpenExisting(ActivateEventName, out var activate))
        {
            using (activate)
                activate.Set();
        }
    }
}
