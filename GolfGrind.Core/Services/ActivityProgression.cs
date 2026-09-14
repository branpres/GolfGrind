using GolfGrind.Core.Models;

namespace GolfGrind.Core.Services;

public static class BagMappingProgression
{
    public static BagMappingProgress Calculate(
        IReadOnlyList<Guid> clubSequence,
        IReadOnlyList<StoredShot> shots,
        DateTimeOffset startedAt,
        int shotsPerClub)
    {
        for (var index = 0; index < clubSequence.Count; index++)
        {
            var retained = shots.Count(shot =>
                !shot.ExcludedFromAnalytics && shot.HitAt >= startedAt && shot.ClubId == clubSequence[index]);
            if (retained < shotsPerClub)
                return new(false, index, retained, clubSequence[index]);
        }
        return new(true, clubSequence.Count, shotsPerClub, null);
    }
}

public sealed record BagMappingProgress(bool Complete, int ClubIndex, int ShotsForClub, Guid? ClubId);

public static class WedgeMatrixProgression
{
    public static WedgeMatrixProgress Advance(
        int clubIndex,
        int swingIndex,
        int shotInCell,
        int clubCount,
        int swingCount,
        int shotsPerCell)
    {
        shotInCell++;
        if (shotInCell < shotsPerCell)
            return new(false, clubIndex, swingIndex, shotInCell);

        shotInCell = 0;
        swingIndex++;
        if (swingIndex >= swingCount)
        {
            swingIndex = 0;
            clubIndex++;
        }
        return new(clubIndex >= clubCount, clubIndex, swingIndex, shotInCell);
    }
}

public sealed record WedgeMatrixProgress(bool Complete, int ClubIndex, int SwingIndex, int ShotInCell);

public static class ShotQualityRules
{
    public static bool IsPotentialOutlier(
        string clubName,
        string? swingType,
        double? carryYards,
        IReadOnlyList<ClubStatistics> bagStatistics)
    {
        if (carryYards is null || ShotAnalytics.NormalizeSwing(swingType) != "Full")
            return false;
        var stat = bagStatistics.FirstOrDefault(item =>
            string.Equals(item.Club.DisplayName, clubName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Club.Name, clubName, StringComparison.OrdinalIgnoreCase));
        if (stat is null || stat.ShotCount < 5 || stat.MedianCarryYards is null || stat.CarryStandardDeviation is null)
            return false;
        var threshold = Math.Max(15, stat.CarryStandardDeviation.Value * 2.5);
        return Math.Abs(carryYards.Value - stat.MedianCarryYards.Value) > threshold;
    }
}
