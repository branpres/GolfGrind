using GolfGrind.Core.Models;

namespace GolfGrind.Core.Services;

public sealed class ConnectionStateMachine
{
    private static readonly IReadOnlyDictionary<LaunchMonitorConnectionState, LaunchMonitorConnectionState[]> Allowed =
        new Dictionary<LaunchMonitorConnectionState, LaunchMonitorConnectionState[]>
        {
            [LaunchMonitorConnectionState.Disconnected] = [LaunchMonitorConnectionState.Discovering, LaunchMonitorConnectionState.Reconnecting],
            [LaunchMonitorConnectionState.Discovering] = [LaunchMonitorConnectionState.Connecting, LaunchMonitorConnectionState.Faulted, LaunchMonitorConnectionState.Disconnected],
            [LaunchMonitorConnectionState.Connecting] = [LaunchMonitorConnectionState.Initializing, LaunchMonitorConnectionState.Faulted, LaunchMonitorConnectionState.Reconnecting, LaunchMonitorConnectionState.Disconnected],
            [LaunchMonitorConnectionState.Initializing] = [LaunchMonitorConnectionState.Connected, LaunchMonitorConnectionState.BallReady, LaunchMonitorConnectionState.Faulted, LaunchMonitorConnectionState.Reconnecting, LaunchMonitorConnectionState.Disconnected],
            [LaunchMonitorConnectionState.Connected] = [LaunchMonitorConnectionState.BallReady, LaunchMonitorConnectionState.Reconnecting, LaunchMonitorConnectionState.Disconnected, LaunchMonitorConnectionState.Faulted],
            [LaunchMonitorConnectionState.BallReady] = [LaunchMonitorConnectionState.Connected, LaunchMonitorConnectionState.Reconnecting, LaunchMonitorConnectionState.Disconnected, LaunchMonitorConnectionState.Faulted],
            [LaunchMonitorConnectionState.Reconnecting] = [LaunchMonitorConnectionState.Discovering, LaunchMonitorConnectionState.Connecting, LaunchMonitorConnectionState.Initializing, LaunchMonitorConnectionState.Connected, LaunchMonitorConnectionState.BallReady, LaunchMonitorConnectionState.Faulted, LaunchMonitorConnectionState.Disconnected],
            [LaunchMonitorConnectionState.Faulted] = [LaunchMonitorConnectionState.Reconnecting, LaunchMonitorConnectionState.Discovering, LaunchMonitorConnectionState.Disconnected]
        };

    public LaunchMonitorConnectionState State { get; private set; } = LaunchMonitorConnectionState.Disconnected;

    public void TransitionTo(LaunchMonitorConnectionState next)
    {
        if (State == next)
            return;
        if (!Allowed.TryGetValue(State, out var allowed) || !allowed.Contains(next))
            throw new InvalidOperationException($"Invalid launch-monitor transition: {State} → {next}.");
        State = next;
    }

    public void Reset() => State = LaunchMonitorConnectionState.Disconnected;

    public void RecoverTo(LaunchMonitorConnectionState state) => State = state;
}
