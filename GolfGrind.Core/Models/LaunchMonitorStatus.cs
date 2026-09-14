namespace GolfGrind.Core.Models;

public sealed record LaunchMonitorStatus(
    bool IsConnected,
    bool IsBallReady,
    int? BatteryPercent,
    string Message,
    LaunchMonitorConnectionState ConnectionState = LaunchMonitorConnectionState.Disconnected,
    LaunchMonitorHealth? Health = null);
