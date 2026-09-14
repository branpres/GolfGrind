namespace GolfGrind.Core.Services;

public static class SkillsChallengeRules
{
    public static double TargetRingRadius(double targetYards, double marginMultiplier)
    {
        var baseRadius = Math.Clamp(targetYards * 0.08, 5, 25);
        return Math.Round(baseRadius * Math.Clamp(marginMultiplier, 0.5, 2), 1);
    }

    public static int ScoreProximity(double targetYards, double? proximityYards, double marginMultiplier)
    {
        if (proximityYards is null)
            return 0;

        var ring = TargetRingRadius(targetYards, marginMultiplier);
        var ratio = proximityYards.Value / ring;
        return ratio switch
        {
            <= 0.25 => 100,
            <= 0.50 => 75,
            <= 0.75 => 50,
            <= 1.00 => 25,
            _ => 0
        };
    }
}
