namespace GolfGrind.Core.Models;

public enum PracticeGameKind
{
    TargetPractice = 0,
    DistanceLadder = 1,
    GolfCombine = 2,
    ApproachPractice = 3,
    FairwayFinder = 4,
    SkillsAssessment = 5,
    PressureStreak = 6
}

public enum CombineFormat
{
    Quick,
    Full
}

public enum ScoringDistanceMode
{
    Carry,
    Total
}

public sealed class PracticeGameOptions
{
    public double FixedTargetYards { get; set; } = 100;
    public int ShotGoal { get; set; } = 10;
    public double ToleranceYards { get; set; } = 5;
    public int MinimumTargetYards { get; set; } = 50;
    public int MaximumTargetYards { get; set; } = 180;
    public int LadderStartYards { get; set; } = 50;
    public int LadderEndYards { get; set; } = 100;
    public int LadderStepYards { get; set; } = 10;
    public double FairwayWidthYards { get; set; } = 30;
    public double MinimumCarryYards { get; set; } = 150;
    public Guid? FairwayClubId { get; set; }
    public int MissesAllowed { get; set; } = 3;
    public CombineFormat CombineFormat { get; set; } = CombineFormat.Quick;
    public double CombineDriverTargetYards { get; set; } = 250;
    public ScoringDistanceMode ApproachDistanceMode { get; set; } = ScoringDistanceMode.Carry;
    public int SkillsStationCount { get; set; } = 10;
    public double SkillsMinimumDistanceYards { get; set; } = 50;
    public double SkillsMaximumDistanceYards { get; set; } = 180;
    public double SkillsMarginMultiplier { get; set; } = 1;
    public ScoringDistanceMode SkillsDistanceMode { get; set; } = ScoringDistanceMode.Carry;

    public PracticeGameSettingsSnapshot Snapshot() => new(
        FixedTargetYards,
        ShotGoal,
        ToleranceYards,
        MinimumTargetYards,
        MaximumTargetYards,
        LadderStartYards,
        LadderEndYards,
        LadderStepYards,
        FairwayWidthYards,
        MinimumCarryYards,
        FairwayClubId,
        MissesAllowed,
        CombineFormat,
        CombineDriverTargetYards,
        ApproachDistanceMode,
        SkillsStationCount,
        SkillsMinimumDistanceYards,
        SkillsMaximumDistanceYards,
        SkillsMarginMultiplier,
        SkillsDistanceMode);

    public void Clamp()
    {
        FixedTargetYards = Math.Clamp(FixedTargetYards, 10, 350);
        ShotGoal = Math.Clamp(ShotGoal, 1, 100);
        ToleranceYards = Math.Clamp(ToleranceYards, 1, 50);
        MinimumTargetYards = Math.Clamp(MinimumTargetYards, 10, 350);
        MaximumTargetYards = Math.Clamp(MaximumTargetYards, MinimumTargetYards, 350);
        LadderStartYards = Math.Clamp(LadderStartYards, 10, 350);
        LadderEndYards = Math.Clamp(LadderEndYards, LadderStartYards, 350);
        LadderStepYards = Math.Clamp(LadderStepYards, 1, 100);
        FairwayWidthYards = Math.Clamp(FairwayWidthYards, 5, 100);
        MinimumCarryYards = Math.Clamp(MinimumCarryYards, 0, 350);
        MissesAllowed = Math.Clamp(MissesAllowed, 1, 10);
        CombineDriverTargetYards = Math.Clamp(CombineDriverTargetYards, 100, 350);
        SkillsStationCount = Math.Clamp(SkillsStationCount, 5, 20);
        SkillsMinimumDistanceYards = Math.Clamp(SkillsMinimumDistanceYards, 20, 280);
        SkillsMaximumDistanceYards = Math.Clamp(SkillsMaximumDistanceYards, Math.Max(50, SkillsMinimumDistanceYards), 350);
        SkillsMarginMultiplier = Math.Clamp(SkillsMarginMultiplier, 0.5, 2);
    }
}

public sealed record PracticeGameSettingsSnapshot(
    double FixedTargetYards,
    int ShotGoal,
    double ToleranceYards,
    int MinimumTargetYards,
    int MaximumTargetYards,
    int LadderStartYards,
    int LadderEndYards,
    int LadderStepYards,
    double FairwayWidthYards,
    double MinimumCarryYards,
    Guid? FairwayClubId,
    int MissesAllowed,
    CombineFormat CombineFormat,
    double CombineDriverTargetYards,
    ScoringDistanceMode ApproachDistanceMode,
    int SkillsStationCount,
    double SkillsMinimumDistanceYards,
    double SkillsMaximumDistanceYards,
    double SkillsMarginMultiplier,
    ScoringDistanceMode SkillsDistanceMode);

public sealed record PracticeGameShotSnapshot(
    PracticeGameKind Game,
    PracticeGameSettingsSnapshot Settings,
    int ShotNumber,
    double? TargetYards,
    Guid? RecommendedClubId,
    string? RecommendedClub,
    string? RecommendedSwing,
    bool IsDriverTarget = false,
    bool? HitTarget = null,
    string? ResultLabel = null);

public sealed record PracticeGameSummary(
    PracticeGameKind Game,
    bool Completed,
    int Shots,
    int Score,
    double? AverageProximityYards,
    int BestStreak,
    int TargetsHit,
    DateTimeOffset FinishedAt,
    string Message);

public sealed record PracticeGameAttempt(
    int ShotNumber,
    double? TargetYards,
    double? ProximityYards,
    int Score,
    bool HitTarget,
    string Category,
    double? CarryYards = null,
    double? OfflineYards = null);

public sealed class PracticeGameState
{
    public PracticeGameKind Game { get; set; } = PracticeGameKind.TargetPractice;
    public PracticeGameSettingsSnapshot Settings { get; set; } = new PracticeGameOptions().Snapshot();
    public bool IsActive { get; set; }
    public bool IsComplete { get; set; }
    public int ShotsTaken { get; set; }
    public int TotalScore { get; set; }
    public int Misses { get; set; }
    public int CurrentStreak { get; set; }
    public int BestStreak { get; set; }
    public int TargetsHit { get; set; }
    public double? CurrentTargetYards { get; set; }
    public Guid? RecommendedClubId { get; set; }
    public string? RecommendedClub { get; set; }
    public string? RecommendedSwing { get; set; }
    public bool IsDriverTarget { get; set; }
    public double? LastProximityYards { get; set; }
    public string? LastResultLabel { get; set; }
    public string Message { get; set; } = "";
    public List<PracticeGameAttempt> Attempts { get; set; } = [];
    public List<PracticeGameTarget> TargetPool { get; set; } = [];
    public int SequenceIndex { get; set; }
}

public sealed record PracticeGameTarget(
    double Yards,
    Guid? ClubId,
    string? ClubName,
    string? SwingType,
    string Category,
    GolfClubKind? ClubKind = null,
    bool IsDriverTarget = false);

public sealed record PracticeGameEvaluation(
    ShotData Shot,
    PracticeGameShotSnapshot Snapshot,
    bool Completed,
    PracticeGameSummary? Summary);

public static class PracticeGameNames
{
    public static IReadOnlyList<PracticeGameKind> SelectableGames { get; } =
    [
        PracticeGameKind.TargetPractice,
        PracticeGameKind.DistanceLadder,
        PracticeGameKind.GolfCombine,
        PracticeGameKind.ApproachPractice,
        PracticeGameKind.FairwayFinder,
        PracticeGameKind.SkillsAssessment,
        PracticeGameKind.PressureStreak
    ];

    public static string DisplayName(this PracticeGameKind game) => game switch
    {
        PracticeGameKind.TargetPractice => "Target Practice",
        PracticeGameKind.DistanceLadder => "Distance Ladder",
        PracticeGameKind.GolfCombine => "Golf Combine",
        PracticeGameKind.ApproachPractice => "Approach Practice",
        PracticeGameKind.FairwayFinder => "Fairway Finder",
        PracticeGameKind.SkillsAssessment => "Skills Challenge",
        PracticeGameKind.PressureStreak => "Pressure Streak",
        _ => game.ToString()
    };

    public static string StorageName(this PracticeGameKind game) => game.ToString();
}
