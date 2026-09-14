using GolfGrind.Core.Models;

namespace GolfGrind.Core.Services;

public sealed record FairwayFinderStatistics(
    int Shots,
    int FairwaysHit,
    double HitPercentage,
    double? AverageOfflineYards,
    double? LeftEdgeYards,
    double? RightEdgeYards,
    double? DispersionWidthYards);

public static class FairwayFinderRules
{
    public static FairwayFinderStatistics Calculate(IEnumerable<PracticeGameAttempt> attempts)
    {
        var results = attempts.ToList();
        var offline = results
            .Where(attempt => attempt.OfflineYards is not null)
            .Select(attempt => attempt.OfflineYards!.Value)
            .ToList();
        var left = offline.Count == 0 ? (double?)null : offline.Min();
        var right = offline.Count == 0 ? (double?)null : offline.Max();
        var hits = results.Count(attempt => attempt.HitTarget);
        return new(
            results.Count,
            hits,
            results.Count == 0 ? 0 : hits * 100d / results.Count,
            offline.Count == 0 ? null : offline.Average(),
            left,
            right,
            left is null || right is null ? null : right - left);
    }
}
