using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ClawHub.Core.Sessions;

namespace ClawHub.Providers.Infrastructure;

/// <summary>Elenco dei processi con il percorso dell'eseguibile, letto senza privilegi di amministratore.</summary>
public static class RunningProcesses
{
    private const uint QueryLimitedInformation = 0x1000;

    public static IReadOnlyList<RunningProcess> Snapshot()
    {
        var self = Environment.ProcessId;
        var result = new List<RunningProcess>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.Id == self || process.Id <= 4)
                    continue;
                if (TryGetPath(process.Id) is { } path)
                    result.Add(new RunningProcess(process.Id, path));
            }
        }
        return result;
    }

    /// <summary>
    /// QueryFullProcessImageName funziona anche con processi a 32 bit e con molti processi di altri utenti,
    /// dove Process.MainModule fallisce. I processi protetti (alcuni anti-cheat) vengono saltati.
    /// </summary>
    private static string? TryGetPath(int processId)
    {
        var handle = OpenProcess(QueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero)
            return null;
        try
        {
            var buffer = new StringBuilder(1024);
            var size = buffer.Capacity;
            return QueryFullProcessImageName(handle, 0, buffer, ref size) ? buffer.ToString(0, size) : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder exeName, ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
