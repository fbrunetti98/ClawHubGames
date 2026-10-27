using ClawHub.Core.Models;

namespace ClawHub.Core.Abstractions;

/// <summary>Trova i giochi installati di uno store.</summary>
public interface ILibraryProvider
{
    GameStore Store { get; }

    /// <summary>True se lo store (o il suo client) risulta installato su questo PC.</summary>
    bool IsAvailable();

    Task<IReadOnlyList<DetectedGame>> GetInstalledGamesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Avvia un gioco sulla piattaforma corrente.</summary>
public interface IGameLauncher
{
    Task LaunchAsync(LaunchInfo launch, CancellationToken cancellationToken = default);
}
