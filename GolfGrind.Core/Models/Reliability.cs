namespace GolfGrind.Core.Models;

public enum LaunchMonitorConnectionState
{
    Disconnected,
    Discovering,
    Connecting,
    Initializing,
    Connected,
    BallReady,
    Reconnecting,
    Faulted
}

public sealed record LaunchMonitorHealth(
    LaunchMonitorConnectionState State,
    DateTimeOffset? LastPacketAt,
    DateTimeOffset? LastHeartbeatAt,
    int ReconnectAttempts,
    int AcceptedShots,
    int RejectedShots,
    int DuplicateShots,
    string? LastRejectionReason = null,
    string? LastError = null);

public sealed record EnvironmentalCalibration(
    double AltitudeFeet = 0,
    double TemperatureFahrenheit = 70,
    double RelativeHumidityPercent = 50,
    double ReferenceCarryScale = 1.0,
    string? ReferenceLabel = null);

public sealed record PersonalShotAdjustment(
    double CarryScale = 1.0,
    double OfflineBiasYards = 0,
    string? Label = null);

public sealed record ShotCalculationMetadata(
    string FlightModel,
    string CalculationProfile,
    EnvironmentalCalibration Environment,
    PersonalShotAdjustment PersonalAdjustment,
    DateTimeOffset CalculatedAt);

public enum ShotRejectionReason
{
    None,
    MissingRequiredMeasurement,
    InvalidBallSpeed,
    InvalidLaunchAngle,
    InvalidLaunchDirection,
    InvalidSpin,
    InvalidFlightResult,
    Duplicate
}

public sealed record ShotValidationResult(
    bool Accepted,
    ShotRejectionReason Reason,
    string Message)
{
    public static ShotValidationResult Accept() => new(true, ShotRejectionReason.None, "Shot accepted");
    public static ShotValidationResult Reject(ShotRejectionReason reason, string message) => new(false, reason, message);
}
