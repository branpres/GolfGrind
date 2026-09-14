namespace GolfGrind.Core.Models;

public sealed record ShotData(
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
    double? ClubSpeedMph = null,
    double? ClubPathDeg = null,
    double? FaceAngleDeg = null,
    double? AttackAngleDeg = null,
    double? DynamicLoftDeg = null,
    double? TotalSpinRpm = null,
    double? SpinAxisDeg = null,
    IReadOnlyList<FlightPoint>? FlightPath = null,
    string? SwingType = null,
    string? PracticeMode = null,
    double? TargetYards = null,
    double? ProximityYards = null,
    int? PracticeScore = null,
    PracticeGameShotSnapshot? PracticeGame = null,
    ShotCalculationMetadata? Calculation = null);

public readonly record struct FlightPoint(
    double DownRangeYards,
    double OfflineYards,
    double HeightYards);
