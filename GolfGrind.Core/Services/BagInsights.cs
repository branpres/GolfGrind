using GolfGrind.Core.Models;

namespace GolfGrind.Core.Services;

public sealed record ClubCarryRecommendation(GolfClub Club, double CarryYards);

public static class BagInsights
{
    public static ClubCarryRecommendation? FindNearestClub(
        IReadOnlyList<ClubStatistics> statistics,
        double targetYards)
    {
        var match = statistics
            .Where(item => item.StockCarryYards is not null)
            .MinBy(item => Math.Abs(item.StockCarryYards!.Value - targetYards));
        return match is null
            ? null
            : new ClubCarryRecommendation(match.Club, match.StockCarryYards!.Value);
    }

    public static double? GapToNext(
        ClubStatistics current,
        IReadOnlyList<ClubStatistics> statistics)
    {
        var index = statistics.ToList().FindIndex(item => item.Club.Id == current.Club.Id);
        if (index < 0 || current.StockCarryYards is null)
            return null;
        var next = statistics.Skip(index + 1).FirstOrDefault(item => item.StockCarryYards is not null);
        return next?.StockCarryYards is null
            ? null
            : current.StockCarryYards.Value - next.StockCarryYards.Value;
    }

    public static void ApplyMedianCarries(
        IEnumerable<GolfClub> clubs,
        IReadOnlyList<ClubStatistics> statistics)
    {
        var byClub = statistics.ToDictionary(item => item.Club.Id);
        foreach (var club in clubs)
        {
            if (byClub.TryGetValue(club.Id, out var clubStatistics) && clubStatistics.MedianCarryYards is not null)
                club.StockCarryYards = Math.Round(clubStatistics.MedianCarryYards.Value, 1);
        }
    }
}

public static class PracticeHistory
{
    public static IReadOnlyList<PracticeGameSummary> CompletedSummaries(
        IEnumerable<PracticeSession> sessions,
        PracticeGameKind game) =>
        sessions
            .Where(session => session.GameSummary is { Completed: true } summary && summary.Game == game)
            .Select(session => session.GameSummary!)
            .ToList();
}
