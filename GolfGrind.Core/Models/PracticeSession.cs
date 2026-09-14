namespace GolfGrind.Core.Models;

public sealed class PracticeSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? EndedAt { get; set; }
    public string? Name { get; set; }
    public string? SessionType { get; set; }
    public PracticeGameKind? PracticeGame { get; set; }
    public PracticeGameSettingsSnapshot? GameSettings { get; set; }
    public PracticeGameSummary? GameSummary { get; set; }
    public List<StoredShot> Shots { get; set; } = [];
}

public sealed record StoredShot(
    DateTimeOffset HitAt,
    string Club,
    double? CarryYards,
    double? TotalYards,
    double? OfflineYards,
    double? ApexYards,
    double? FlightTimeSeconds,
    double BallSpeedMph,
    double LaunchAngleDeg,
    double LaunchDirectionDeg,
    double BackSpinRpm,
    double SideSpinRpm,
    double? TotalSpinRpm,
    double? SpinAxisDeg,
    Guid? ClubId = null,
    string? SwingType = null,
    string? PracticeMode = null,
    double? TargetYards = null,
    double? ProximityYards = null,
    int? PracticeScore = null,
    bool ExcludedFromAnalytics = false,
    PracticeGameShotSnapshot? PracticeGame = null,
    ShotCalculationMetadata? Calculation = null)
{
    public static StoredShot FromShot(ShotData shot, Guid? clubId = null) => new(
        shot.HitAt,
        shot.Club,
        shot.CarryYards,
        shot.TotalYards,
        shot.OfflineYards,
        shot.ApexYards,
        shot.FlightTimeSeconds,
        shot.BallSpeedMph,
        shot.LaunchAngleDeg,
        shot.LaunchDirectionDeg,
        shot.BackSpinRpm,
        shot.SideSpinRpm,
        shot.TotalSpinRpm,
        shot.SpinAxisDeg,
        clubId,
        shot.SwingType,
        shot.PracticeMode,
        shot.TargetYards,
        shot.ProximityYards,
        shot.PracticeScore,
        false,
        shot.PracticeGame,
        shot.Calculation);
}
