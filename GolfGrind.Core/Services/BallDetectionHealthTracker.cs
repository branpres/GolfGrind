namespace GolfGrind.Core.Services;

public enum BallDetectionRecoveryAction
{
    None,
    ReArm,
    Reconnect
}

/// <summary>
/// Tracks the Square monitor's ball-state telemetry independently from general
/// Bluetooth traffic. Heartbeats may continue even when ball detection stalls.
/// </summary>
public sealed class BallDetectionHealthTracker(TimeSpan? staleAfter = null)
{
    private readonly object _sync = new();
    private readonly TimeSpan _staleAfter = staleAfter ?? TimeSpan.FromSeconds(12);
    private DateTimeOffset? _armedAt;
    private DateTimeOffset? _lastTelemetryAt;
    private DateTimeOffset? _rearmAttemptedAt;
    private bool _sawNotReadySinceArm;

    public void Reset()
    {
        lock (_sync)
        {
            _armedAt = null;
            _lastTelemetryAt = null;
            _rearmAttemptedAt = null;
            _sawNotReadySinceArm = false;
        }
    }

    public void MarkArmed(DateTimeOffset observedAt)
    {
        lock (_sync)
        {
            _armedAt = observedAt;
            _lastTelemetryAt = null;
            _sawNotReadySinceArm = false;
        }
    }

    public bool ObserveBallState(bool ready, DateTimeOffset observedAt)
    {
        lock (_sync)
        {
            if (ready && !_sawNotReadySinceArm)
                return false;

            if (!ready)
                _sawNotReadySinceArm = true;
            _lastTelemetryAt = observedAt;
            _rearmAttemptedAt = null;
            return ready;
        }
    }

    public BallDetectionRecoveryAction Evaluate(DateTimeOffset observedAt)
    {
        lock (_sync)
        {
            var baseline = _lastTelemetryAt ?? _armedAt;
            if (baseline is null || observedAt - baseline.Value <= _staleAfter)
                return BallDetectionRecoveryAction.None;

            if (_rearmAttemptedAt is null)
            {
                _rearmAttemptedAt = observedAt;
                return BallDetectionRecoveryAction.ReArm;
            }

            return observedAt - _rearmAttemptedAt.Value > _staleAfter
                ? BallDetectionRecoveryAction.Reconnect
                : BallDetectionRecoveryAction.None;
        }
    }
}
