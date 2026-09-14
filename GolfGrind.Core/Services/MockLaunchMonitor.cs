using GolfGrind.Core.Abstractions;
using GolfGrind.Core.Models;

namespace GolfGrind.Core.Services;

public sealed class MockLaunchMonitor : ILaunchMonitor
{
    private readonly Random _random = new();
    private string _club = "7 Iron";

    public event EventHandler<ShotData>? ShotReceived;
    public event EventHandler<LaunchMonitorStatus>? StatusChanged;

    public bool IsConnected { get; private set; }
    public string? DiagnosticsPath => null;

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = true;
        StatusChanged?.Invoke(this, new(true, true, 84, "Connected to simulated Square", LaunchMonitorConnectionState.BallReady));
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = false;
        StatusChanged?.Invoke(this, new(false, false, null, "Disconnected", LaunchMonitorConnectionState.Disconnected));
        return Task.CompletedTask;
    }

    public Task SelectClubAsync(string club, CancellationToken cancellationToken = default)
    {
        _club = club;
        return Task.CompletedTask;
    }

    public void SimulateShot()
    {
        if (!IsConnected)
            return;

        var shot = BallFlightCalculator.Calculate(new ShotData(
            DateTimeOffset.Now,
            _club,
            null,
            null,
            null,
            null,
            null,
            Math.Round(111 + NextGaussian() * 3, 1),
            Math.Round(18 + NextGaussian() * 1.5, 1),
            Math.Round(NextGaussian() * 1.2, 1),
            Math.Round(5750 + NextGaussian() * 350),
            Math.Round(NextGaussian() * 450),
            TotalSpinRpm: Math.Round(5750 + NextGaussian() * 350),
            SpinAxisDeg: Math.Round(NextGaussian() * 4, 1)));

        ShotReceived?.Invoke(this, shot);
    }

    private double NextGaussian()
    {
        var u1 = 1.0 - _random.NextDouble();
        var u2 = 1.0 - _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
