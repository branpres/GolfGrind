using GolfGrind.Core.Models;

namespace GolfGrind.Core.Abstractions;

public interface ILaunchMonitor : IAsyncDisposable
{
    event EventHandler<ShotData>? ShotReceived;
    event EventHandler<LaunchMonitorStatus>? StatusChanged;

    bool IsConnected { get; }
    string? DiagnosticsPath { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task SelectClubAsync(string club, CancellationToken cancellationToken = default);
}
