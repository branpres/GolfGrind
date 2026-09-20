using GolfGrind.Core.Models;

namespace GolfGrind.Core.Services;

public sealed record ClubStatistics(
    GolfClub Club,
    int ShotCount,
    double? StockCarryYards,
    double? AverageCarryYards,
    double? MedianCarryYards,
    double? TypicalLowYards,
    double? TypicalHighYards,
    double? CarryStandardDeviation,
    double? AverageBallSpeedMph,
    double? AverageLaunchAngleDeg,
    double? AverageSpinRpm,
    double? AverageApexYards,
    double? LeftEdgeYards,
    double? RightEdgeYards);

public sealed record WedgeMatrixCell(int ShotCount, double? MedianCarryYards);
public sealed record PerformanceDistanceMetrics(
    double? AverageTotalYards,
    double? AverageRolloutYards,
    double? AverageOfflineYards);

public static class ShotAnalytics
{
    public const string BagMappingSessionType = "Bag Mapping";
    public const string WedgeMatrixSessionType = "Wedge Matrix";

    public static IReadOnlyList<PracticeSession> PerformanceSessions(IEnumerable<PracticeSession> sessions) =>
        sessions.Where(session => !IsSessionType(session, BagMappingSessionType) &&
                                  !IsSessionType(session, WedgeMatrixSessionType)).ToList();

    public static IReadOnlyList<ClubStatistics> CalculateBag(
        IEnumerable<GolfClub> bag,
        IEnumerable<PracticeSession> sessions) =>
        bag.Select(club =>
        {
            var shots = BagMappingShotsForClub(club, sessions);
            if (club.Kind == GolfClubKind.Wedge)
                shots = shots.Where(shot => NormalizeSwing(shot.SwingType) == "Full").ToList();
            return CalculateClub(club, shots);
        }).ToList();

    public static IReadOnlyList<StoredShot> BagMappingShotsForClub(
        GolfClub club,
        IEnumerable<PracticeSession> sessions) =>
        ShotsForClub(club, sessions.Where(session => IsSessionType(session, BagMappingSessionType)));

    public static IReadOnlyList<StoredShot> WedgeMatrixShotsForClub(
        GolfClub club,
        IEnumerable<PracticeSession> sessions) =>
        ShotsForClub(club, sessions.Where(session => IsSessionType(session, WedgeMatrixSessionType)));

    public static IReadOnlyList<StoredShot> ShotsForClub(
        GolfClub club,
        IEnumerable<PracticeSession> sessions) =>
        sessions
            .SelectMany(session => session.Shots)
            .Where(shot => !shot.ExcludedFromAnalytics)
            .Where(shot => MatchesClub(shot, club))
            .ToList();

    /// <summary>
    /// Matches the durable ID first, then falls back to the recorded club
    /// label. The label fallback repairs sessions created before a default bag
    /// was persisted and also survives removing and re-adding the same club.
    /// </summary>
    public static bool MatchesClub(StoredShot shot, GolfClub club) =>
        shot.ClubId == club.Id ||
        string.Equals(shot.Club, club.DisplayName, StringComparison.OrdinalIgnoreCase) ||
        (club.LoftDegrees is null &&
         string.Equals(shot.Club, club.Name, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<StoredShot> DispersionShots(
        GolfClub club,
        IEnumerable<PracticeSession> sessions)
    {
        var shots = BagMappingShotsForClub(club, sessions);
        if (club.Kind == GolfClubKind.Wedge)
            shots = shots.Where(shot => NormalizeSwing(shot.SwingType) == "Full").ToList();
        return shots
            .Where(shot => shot.CarryYards is not null && shot.OfflineYards is not null)
            .ToList();
    }

    public static WedgeMatrixCell CalculateWedgeCell(IEnumerable<StoredShot> shots, string swingType)
    {
        var carries = shots
            .Where(shot => !shot.ExcludedFromAnalytics)
            .Where(shot => NormalizeSwing(shot.SwingType) == swingType)
            .Select(shot => shot.CarryYards)
            .Where(carry => carry is not null)
            .Select(carry => carry!.Value)
            .OrderBy(carry => carry)
            .ToList();

        return new(carries.Count, carries.Count == 0 ? null : Median(carries));
    }

    public static string NormalizeSwing(string? swingType) => swingType switch
    {
        "Quarter" => "Quarter",
        "Half" => "Half",
        "ThreeQuarter" => "ThreeQuarter",
        _ => "Full"
    };

    private static bool IsSessionType(PracticeSession session, string sessionType) =>
        string.Equals(session.SessionType, sessionType, StringComparison.OrdinalIgnoreCase);

    public static ClubStatistics CalculateClub(GolfClub club, IReadOnlyList<StoredShot> shots)
    {
        shots = shots.Where(shot => !shot.ExcludedFromAnalytics).ToList();
        var carries = shots.Where(shot => shot.CarryYards is not null)
            .Select(shot => shot.CarryYards!.Value).OrderBy(value => value).ToList();
        var offline = shots.Where(shot => shot.OfflineYards is not null)
            .Select(shot => shot.OfflineYards!.Value).ToList();

        double? median = carries.Count == 0 ? null : Median(carries);
        double? average = carries.Count == 0 ? null : carries.Average();
        double? standardDeviation = carries.Count == 0 || average is null
            ? null
            : Math.Sqrt(carries.Average(value => Math.Pow(value - average.Value, 2)));

        return new(
            club,
            shots.Count,
            club.StockCarryYards ?? median,
            average,
            median,
            carries.Count == 0 ? null : Percentile(carries, 0.20),
            carries.Count == 0 ? null : Percentile(carries, 0.80),
            standardDeviation,
            Average(shots.Select(shot => (double?)shot.BallSpeedMph)),
            Average(shots.Select(shot => (double?)shot.LaunchAngleDeg)),
            Average(shots.Select(shot => shot.TotalSpinRpm)),
            Average(shots.Select(shot => shot.ApexYards)),
            offline.Count == 0 ? null : offline.Min(),
            offline.Count == 0 ? null : offline.Max());
    }

    public static PerformanceDistanceMetrics CalculatePerformanceDistanceMetrics(IEnumerable<StoredShot> shots)
    {
        var retained = shots.Where(shot => !shot.ExcludedFromAnalytics).ToList();
        return new(
            Average(retained.Select(shot => shot.TotalYards)),
            Average(retained
                .Where(shot => shot.TotalYards is not null && shot.CarryYards is not null)
                .Select(shot => (double?)(shot.TotalYards!.Value - shot.CarryYards!.Value))),
            Average(retained.Select(shot => shot.OfflineYards)));
    }

    private static double? Average(IEnumerable<double?> values)
    {
        var present = values.Where(value => value is not null).Select(value => value!.Value).ToList();
        return present.Count == 0 ? null : present.Average();
    }

    private static double Median(IReadOnlyList<double> sorted) => Percentile(sorted, 0.5);

    private static double Percentile(IReadOnlyList<double> sorted, double percentile)
    {
        if (sorted.Count == 1)
            return sorted[0];

        var position = percentile * (sorted.Count - 1);
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        var fraction = position - lower;
        return sorted[lower] + (sorted[upper] - sorted[lower]) * fraction;
    }
}
