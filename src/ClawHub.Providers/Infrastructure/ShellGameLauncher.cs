using System.Diagnostics;
using ClawHub.Core.Abstractions;
using ClawHub.Core.Models;

namespace ClawHub.Providers.Infrastructure;

/// <summary>Avvia i giochi tramite la shell di Windows (protocolli, eseguibili, app pacchettizzate).</summary>
public sealed class ShellGameLauncher : IGameLauncher
{
    public Task LaunchAsync(LaunchInfo launch, CancellationToken cancellationToken = default)
    {
        var startInfo = launch.Kind switch
        {
            LaunchKind.Uri => new ProcessStartInfo(launch.Target) { UseShellExecute = true },
            LaunchKind.Executable => new ProcessStartInfo(launch.Target, launch.Arguments ?? string.Empty)
            {
                UseShellExecute = true,
                WorkingDirectory = launch.WorkingDirectory ?? Path.GetDirectoryName(launch.Target) ?? string.Empty,
            },
            LaunchKind.PackagedApp => new ProcessStartInfo("explorer.exe", $@"shell:AppsFolder\{launch.Target}")
            {
                UseShellExecute = true,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(launch)),
        };

        if (launch.Kind == LaunchKind.Executable && !File.Exists(launch.Target))
            throw new FileNotFoundException("Game executable not found.", launch.Target);

        using var _ = Process.Start(startInfo);
        return Task.CompletedTask;
    }
}
