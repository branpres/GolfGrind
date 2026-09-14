using GolfGrind.Core.Models;

namespace GolfGrind.Core.Services;

public static class CombineRules
{
    public const int ShotsPerTargetPerPass = 3;
    public const double LateralPenalty = 1.25;
    public const double DriverCarryGraceYards = 25;
    public static IReadOnlyList<double> DistanceTargets { get; } =
        [55, 70, 85, 95, 105, 115, 130, 145, 160, 170, 180];
    public static int TargetCount => DistanceTargets.Count + 1;

    public static int Passes(CombineFormat format) => format == CombineFormat.Full ? 2 : 1;
    public static int ShotsPerDistance(CombineFormat format) => ShotsPerTargetPerPass * Passes(format);
    public static int TotalShots(CombineFormat format) => TargetCount * ShotsPerDistance(format);

    public static int TargetIndex(int shotsTaken) =>
        Math.Max(0, shotsTaken) / ShotsPerTargetPerPass % TargetCount;

    public static int ShotInTarget(int shotsTaken) =>
        Math.Max(0, shotsTaken) % ShotsPerTargetPerPass + 1;

    public static int PassNumber(int shotsTaken) =>
        Math.Max(0, shotsTaken) / (TargetCount * ShotsPerTargetPerPass) + 1;

    public static int ScoreShot(
        double targetCarryYards,
        double? actualCarryYards,
        double? offlineYards,
        bool driverTarget)
    {
        if (actualCarryYards is null || offlineYards is null)
            return 0;

        var carryError = driverTarget
            ? Math.Max(0, targetCarryYards - DriverCarryGraceYards - actualCarryYards.Value)
            : Math.Abs(actualCarryYards.Value - targetCarryYards);
        var weightedLateralError = Math.Abs(offlineYards.Value) * LateralPenalty;
        var adjustedError = Math.Sqrt(carryError * carryError + weightedLateralError * weightedLateralError);
        var zeroScoreRadius = driverTarget
            ? Math.Max(35, targetCarryYards * 0.18)
            : Math.Max(12, targetCarryYards * 0.12);
        var score = 100 * (1 - adjustedError / zeroScoreRadius);
        return (int)Math.Round(Math.Clamp(score, 0, 100), MidpointRounding.AwayFromZero);
    }
}
