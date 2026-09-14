using GolfGrind.Core.Models;

namespace GolfGrind.Core.Services;

public sealed record AnalyticsTrendPoint(int Index, string SessionName, DateTimeOffset StartedAt, double MedianCarry);
public sealed record AnalyticsTrendComparison(double CarryChange, double ConsistencyChange);
public sealed record AnalyticsSessionRow(
    string Name,
    DateTimeOffset StartedAt,
    int ShotCount,
    double? MedianCarry,
    double? StandardDeviation,
    double? DispersionWidth,
    double? AverageBallSpeed);

public sealed record AnalyticsDashboardSnapshot(
    GolfClub? Club,
    IReadOnlyList<StoredShot> MatchingShots,
    IReadOnlyList<StoredShot> IncludedShots,
    int ExcludedCount,
    ClubStatistics Statistics,
    PerformanceDistanceMetrics DistanceMetrics,
    string Confidence,
    IReadOnlyList<StoredShot> DispersionShots,
    IReadOnlyList<AnalyticsTrendPoint> TrendPoints,
    IReadOnlyList<AnalyticsSessionRow> SessionRows,
    AnalyticsTrendComparison? RecentComparison);

public static class AnalyticsDashboardBuilder
{
    public static AnalyticsDashboardSnapshot Build(
        IReadOnlyList<GolfClub> clubs,
        IReadOnlyList<PracticeSession> sessions,
        Guid selectedClubId,
        DateTimeOffset? earliestDate,
        string swingFilter)
    {
        var club = clubs.FirstOrDefault(item => item.Id == selectedClubId);
        var performanceSessions = ShotAnalytics.PerformanceSessions(sessions);
        List<StoredShot> matching = club is null
            ? []
            : performanceSessions.SelectMany(session => session.Shots)
                .Where(shot => Matches(shot, club, earliestDate, swingFilter))
                .OrderBy(shot => shot.HitAt)
                .ToList();
        var included = matching.Where(shot => !shot.ExcludedFromAnalytics).ToList();
        var statistics = club is null
            ? new ClubStatistics(new(), 0, null, null, null, null, null, null, null, null, null, null, null, null)
            : ShotAnalytics.CalculateClub(club, included);
        var distanceMetrics = ShotAnalytics.CalculatePerformanceDistanceMetrics(included);
        List<AnalyticsTrendPoint> trendPoints = club is null ? [] : BuildTrendPoints(performanceSessions, club, earliestDate, swingFilter);
        List<AnalyticsSessionRow> sessionRows = club is null ? [] : BuildSessionRows(performanceSessions, club, earliestDate, swingFilter);

        return new(
            club,
            matching,
            included,
            matching.Count - included.Count,
            statistics,
            distanceMetrics,
            Confidence(included.Count),
            included.Where(shot => shot.CarryYards is not null && shot.OfflineYards is not null).TakeLast(100).ToList(),
            trendPoints,
            sessionRows,
            RecentComparison(club, included));
    }

    private static List<AnalyticsTrendPoint> BuildTrendPoints(
        IReadOnlyList<PracticeSession> sessions,
        GolfClub club,
        DateTimeOffset? earliestDate,
        string swingFilter) => sessions
        .OrderBy(session => session.StartedAt)
        .Select(session => new
        {
            Session = session,
            Statistics = ShotAnalytics.CalculateClub(club, session.Shots
                .Where(shot => Matches(shot, club, earliestDate, swingFilter))
                .Where(shot => !shot.ExcludedFromAnalytics)
                .ToList())
        })
        .Where(item => item.Statistics.MedianCarryYards is not null)
        .Select((item, index) => new AnalyticsTrendPoint(index, item.Session.Name ?? "Session", item.Session.StartedAt, item.Statistics.MedianCarryYards!.Value))
        .ToList();

    private static List<AnalyticsSessionRow> BuildSessionRows(
        IReadOnlyList<PracticeSession> sessions,
        GolfClub club,
        DateTimeOffset? earliestDate,
        string swingFilter) => sessions
        .OrderByDescending(session => session.StartedAt)
        .Select(session => new
        {
            Session = session,
            Shots = session.Shots.Where(shot => Matches(shot, club, earliestDate, swingFilter)).Where(shot => !shot.ExcludedFromAnalytics).ToList()
        })
        .Where(item => item.Shots.Count > 0)
        .Take(12)
        .Select(item =>
        {
            var statistics = ShotAnalytics.CalculateClub(club, item.Shots);
            var dispersion = statistics.LeftEdgeYards is null || statistics.RightEdgeYards is null
                ? null
                : statistics.RightEdgeYards - statistics.LeftEdgeYards;
            return new AnalyticsSessionRow(
                item.Session.Name ?? item.Session.SessionType ?? "Session",
                item.Session.StartedAt,
                item.Shots.Count,
                statistics.MedianCarryYards,
                statistics.CarryStandardDeviation,
                dispersion,
                statistics.AverageBallSpeedMph);
        })
        .ToList();

    private static AnalyticsTrendComparison? RecentComparison(GolfClub? club, IReadOnlyList<StoredShot> shots)
    {
        if (club is null)
            return null;
        var carries = shots.Where(shot => shot.CarryYards is not null).ToList();
        if (carries.Count < 6)
            return null;
        var size = Math.Min(10, carries.Count / 2);
        var prior = ShotAnalytics.CalculateClub(club, carries.Skip(carries.Count - size * 2).Take(size).ToList());
        var recent = ShotAnalytics.CalculateClub(club, carries.TakeLast(size).ToList());
        return new(
            (recent.MedianCarryYards ?? 0) - (prior.MedianCarryYards ?? 0),
            (recent.CarryStandardDeviation ?? 0) - (prior.CarryStandardDeviation ?? 0));
    }

    private static bool Matches(StoredShot shot, GolfClub club, DateTimeOffset? earliestDate, string swingFilter) =>
        (shot.ClubId == club.Id || (shot.ClubId is null &&
         (string.Equals(shot.Club, club.DisplayName, StringComparison.OrdinalIgnoreCase) ||
          string.Equals(shot.Club, club.Name, StringComparison.OrdinalIgnoreCase)))) &&
        (earliestDate is not { } earliest || shot.HitAt >= earliest) &&
        (swingFilter == "All" || ShotAnalytics.NormalizeSwing(shot.SwingType) == swingFilter);

    private static string Confidence(int shotCount) => shotCount switch
    {
        < 5 => "Low",
        < 10 => "Building",
        < 20 => "Moderate",
        _ => "Strong"
    };
}
