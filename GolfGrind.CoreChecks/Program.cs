using GolfGrind.Core.Models;
using GolfGrind.Core.Services;

var checks = new (string Name, Action Run)[]
{
    ("connection state transitions", CheckConnectionState),
    ("shot validation", CheckShotValidation),
    ("duplicate protection", CheckDuplicates),
    ("calibration separation", CheckCalibration),
    ("practice game completion", CheckTargetGame),
    ("pressure streak completion", CheckPressureStreak),
    ("practice recommendations use bag mapping carries", CheckBagMappingRecommendations),
    ("calibration datasets stay separate", CheckCalibrationDataSeparation),
    ("performance distance metrics", CheckPerformanceDistanceMetrics),
    ("analytics dashboard projections", CheckAnalyticsDashboard),
    ("guided activity state", CheckGuidedActivityState),
    ("practice recommends wedge swing", CheckWedgeSwingRecommendation),
    ("approach practice carry and total modes", CheckApproachPractice),
    ("practice mode list", CheckSelectablePracticeModes),
    ("combine formats and target sequence", CheckCombineFormats),
    ("combine scoring", CheckCombineScoring),
    ("skills challenge configuration and scoring", CheckSkillsChallenge),
    ("mapped clubs are optional for practice", CheckOptionalPracticeMapping),
    ("fairway finder club selection and statistics", CheckFairwayFinder),
    ("flight path remains calculated", CheckFlightPath),
    ("flight path reflects launch measurements", CheckFlightPathInputs)
};

foreach (var check in checks)
{
    check.Run();
    Console.WriteLine($"PASS {check.Name}");
}

static void CheckConnectionState()
{
    var machine = new ConnectionStateMachine();
    machine.TransitionTo(LaunchMonitorConnectionState.Discovering);
    machine.TransitionTo(LaunchMonitorConnectionState.Connecting);
    machine.TransitionTo(LaunchMonitorConnectionState.Initializing);
    machine.TransitionTo(LaunchMonitorConnectionState.Connected);
    machine.TransitionTo(LaunchMonitorConnectionState.BallReady);
    machine.TransitionTo(LaunchMonitorConnectionState.Reconnecting);
    Equal(LaunchMonitorConnectionState.Reconnecting, machine.State);
}

static void CheckShotValidation()
{
    True(ShotValidator.Validate(ValidShot()).Accepted);
    Equal(ShotRejectionReason.InvalidBallSpeed, ShotValidator.Validate(ValidShot() with { BallSpeedMph = 400 }).Reason);
}

static void CheckDuplicates()
{
    var detector = new ShotDuplicateDetector(TimeSpan.FromSeconds(3));
    var when = DateTimeOffset.UtcNow;
    True(!detector.IsDuplicate(ValidShot(), when));
    True(detector.IsDuplicate(ValidShot(), when.AddSeconds(1)));
    True(!detector.IsDuplicate(ValidShot() with { BallSpeedMph = 121 }, when.AddSeconds(2)));
}

static void CheckCalibration()
{
    var environment = new EnvironmentalCalibration(AltitudeFeet: 1000, ReferenceCarryScale: 1.02);
    var personal = new PersonalShotAdjustment(CarryScale: 0.98, OfflineBiasYards: 1.5);
    var adjusted = CalibrationMath.AdjustCarry(100, environment, personal);
    True(adjusted > 99 && adjusted < 103);
    var metadata = CalibrationMath.AttachMetadata(ValidShot(), environment, personal).Calculation!;
    Equal(1.02, metadata.Environment.ReferenceCarryScale);
    Equal(0.98, metadata.PersonalAdjustment.CarryScale);
}

static void CheckTargetGame()
{
    var engine = new PracticeGameEngine();
    var options = new PracticeGameOptions { FixedTargetYards = 100, ShotGoal = 2, ToleranceYards = 5 };
    var state = engine.Start(PracticeGameKind.TargetPractice, options, [], [], new Random(1));
    engine.Evaluate(state, ValidShot() with { CarryYards = 100, OfflineYards = 0 }, new Random(1));
    var final = engine.Evaluate(state, ValidShot() with { CarryYards = 102, OfflineYards = 1 }, new Random(1));
    True(final.Completed);
    Equal(20, final.Summary!.Score);
}

static void CheckPressureStreak()
{
    var engine = new PracticeGameEngine();
    var options = new PracticeGameOptions { MinimumTargetYards = 100, MaximumTargetYards = 100, MissesAllowed = 2, ToleranceYards = 5 };
    var state = engine.Start(PracticeGameKind.PressureStreak, options, [], [], new Random(1));
    engine.Evaluate(state, ValidShot() with { CarryYards = 100, OfflineYards = 0 }, new Random(1));
    engine.Evaluate(state, ValidShot() with { CarryYards = 150, OfflineYards = 0 }, new Random(1));
    var final = engine.Evaluate(state, ValidShot() with { CarryYards = 150, OfflineYards = 0 }, new Random(1));
    True(final.Completed);
    Equal(1, final.Summary!.BestStreak);
}

static void CheckBagMappingRecommendations()
{
    var wedge = new GolfClub { Name = "GW", Kind = GolfClubKind.Wedge, LoftDegrees = 48 };
    var session = new PracticeSession
    {
        SessionType = ShotAnalytics.BagMappingSessionType,
        Shots =
        [
            StoredShot.FromShot(ValidShot() with { Club = wedge.DisplayName, CarryYards = 55, SwingType = "Half" }, wedge.Id),
            StoredShot.FromShot(ValidShot() with { Club = wedge.DisplayName, CarryYards = 105, SwingType = "Full" }, wedge.Id)
        ]
    };
    var options = new PracticeGameOptions { FixedTargetYards = 105 };
    var state = new PracticeGameEngine().Start(PracticeGameKind.ApproachPractice, options, [wedge], [session], new Random(1));
    Equal(105d, state.TargetPool.Single().Yards);
    Equal(wedge.Id, state.RecommendedClubId!.Value);
}

static void CheckCalibrationDataSeparation()
{
    var wedge = new GolfClub { Name = "SW", Kind = GolfClubKind.Wedge, LoftDegrees = 56 };
    var bagSession = new PracticeSession
    {
        SessionType = ShotAnalytics.BagMappingSessionType,
        Shots = [StoredShot.FromShot(ValidShot() with { Club = wedge.DisplayName, CarryYards = 100, SwingType = "Full" }, wedge.Id)]
    };
    var matrixSession = new PracticeSession
    {
        SessionType = ShotAnalytics.WedgeMatrixSessionType,
        Shots = [StoredShot.FromShot(ValidShot() with { Club = wedge.DisplayName, CarryYards = 65, SwingType = "Half" }, wedge.Id)]
    };
    var rangeSession = new PracticeSession
    {
        SessionType = "Range",
        Shots = [StoredShot.FromShot(ValidShot() with { Club = wedge.DisplayName, CarryYards = 180, SwingType = "Full" }, wedge.Id)]
    };

    var bag = ShotAnalytics.CalculateBag([wedge], [bagSession, matrixSession, rangeSession]).Single();
    Equal(1, bag.ShotCount);
    Equal(100d, bag.MedianCarryYards!.Value);
    Equal(1, ShotAnalytics.WedgeMatrixShotsForClub(wedge, [bagSession, matrixSession, rangeSession]).Count);
    Equal(1, ShotAnalytics.PerformanceSessions([bagSession, matrixSession, rangeSession]).Count);
}

static void CheckPerformanceDistanceMetrics()
{
    var shots = new[]
    {
        StoredShot.FromShot(ValidShot() with { CarryYards = 150, TotalYards = 160, OfflineYards = -4 }),
        StoredShot.FromShot(ValidShot() with { CarryYards = 160, TotalYards = 174, OfflineYards = 8 })
    };
    var metrics = ShotAnalytics.CalculatePerformanceDistanceMetrics(shots);
    Equal(167d, metrics.AverageTotalYards!.Value);
    Equal(12d, metrics.AverageRolloutYards!.Value);
    Equal(2d, metrics.AverageOfflineYards!.Value);
}

static void CheckAnalyticsDashboard()
{
    var iron = new GolfClub { Name = "7 Iron", Kind = GolfClubKind.Iron };
    var mapping = new PracticeSession
    {
        SessionType = ShotAnalytics.BagMappingSessionType,
        Shots = [StoredShot.FromShot(ValidShot() with { CarryYards = 100 }, iron.Id)]
    };
    var range = new PracticeSession
    {
        Name = "Range one",
        SessionType = "Range",
        Shots =
        [
            StoredShot.FromShot(ValidShot() with { CarryYards = 150, TotalYards = 160, OfflineYards = -4 }, iron.Id),
            StoredShot.FromShot(ValidShot() with { CarryYards = 160, TotalYards = 172, OfflineYards = 6 }, iron.Id)
        ]
    };
    var dashboard = AnalyticsDashboardBuilder.Build([iron], [mapping, range], iron.Id, null, "All");
    Equal(2, dashboard.IncludedShots.Count);
    Equal(1, dashboard.SessionRows.Count);
    Equal("Range one", dashboard.SessionRows[0].Name);
    Equal(10d, dashboard.SessionRows[0].DispersionWidth!.Value);
    Equal("Low", dashboard.Confidence);
    Equal(0, dashboard.ExcludedCount);
}

static void CheckGuidedActivityState()
{
    var iron = new GolfClub { Name = "7 Iron", Kind = GolfClubKind.Iron };
    var wedge = new GolfClub { Name = "SW", Kind = GolfClubKind.Wedge };
    var bagGuide = new BagMappingGuideState { ShotsPerClub = 1 };
    bagGuide.ResetSelection([iron, wedge]);
    var startedAt = DateTimeOffset.UtcNow;
    True(bagGuide.Start([iron, wedge], startedAt));
    Equal(iron.Id, bagGuide.CurrentClubId!.Value);
    True(!bagGuide.Advance([StoredShot.FromShot(ValidShot() with { HitAt = startedAt.AddSeconds(1) }, iron.Id)]));
    Equal(wedge.Id, bagGuide.CurrentClubId!.Value);
    True(bagGuide.Advance(
    [
        StoredShot.FromShot(ValidShot() with { HitAt = startedAt.AddSeconds(1) }, iron.Id),
        StoredShot.FromShot(ValidShot() with { HitAt = startedAt.AddSeconds(2) }, wedge.Id)
    ]));
    True(!bagGuide.IsActive);

    var wedgeGuide = new WedgeMatrixGuideState { ShotsPerCell = 1 };
    True(wedgeGuide.Start([wedge], wedge.Id, "Half"));
    Equal("Half", wedgeGuide.Current([wedge])!.SwingType);
    True(!wedgeGuide.Advance(1));
    Equal("ThreeQuarter", wedgeGuide.Current([wedge])!.SwingType);
    True(!wedgeGuide.Advance(1));
    Equal("Full", wedgeGuide.Current([wedge])!.SwingType);
    True(wedgeGuide.Advance(1));
    True(!wedgeGuide.IsActive);
}

static void CheckWedgeSwingRecommendation()
{
    var wedge = new GolfClub { Name = "SW", Kind = GolfClubKind.Wedge, LoftDegrees = 56 };
    var matrixSession = new PracticeSession
    {
        SessionType = ShotAnalytics.WedgeMatrixSessionType,
        Shots =
        [
            StoredShot.FromShot(ValidShot() with { Club = wedge.DisplayName, CarryYards = 65, SwingType = "Half" }, wedge.Id),
            StoredShot.FromShot(ValidShot() with { Club = wedge.DisplayName, CarryYards = 85, SwingType = "ThreeQuarter" }, wedge.Id),
            StoredShot.FromShot(ValidShot() with { Club = wedge.DisplayName, CarryYards = 105, SwingType = "Full" }, wedge.Id)
        ]
    };
    var options = new PracticeGameOptions { FixedTargetYards = 66 };
    var state = new PracticeGameEngine().Start(
        PracticeGameKind.ApproachPractice,
        options,
        [wedge],
        [matrixSession],
        new Random(1));
    Equal(wedge.Id, state.RecommendedClubId!.Value);
    Equal("Half", state.RecommendedSwing!);
}

static void CheckApproachPractice()
{
    var engine = new PracticeGameEngine();
    var options = new PracticeGameOptions
    {
        FixedTargetYards = 150,
        ShotGoal = 2,
        ToleranceYards = 10,
        ApproachDistanceMode = ScoringDistanceMode.Carry
    };
    var state = engine.Start(PracticeGameKind.ApproachPractice, options, [], [], new Random(1));
    var green = engine.Evaluate(
        state,
        ValidShot() with { CarryYards = 156, OfflineYards = 8 },
        new Random(1));
    True(green.Snapshot.HitTarget == true);
    Equal(1, green.Shot.PracticeScore!.Value);

    var final = engine.Evaluate(
        state,
        ValidShot() with { CarryYards = 165, OfflineYards = 0 },
        new Random(1));
    True(final.Completed);
    Equal(1, final.Summary!.Score);
    Equal(1, final.Summary.TargetsHit);

    var totalOptions = new PracticeGameOptions
    {
        FixedTargetYards = 165,
        ShotGoal = 1,
        ToleranceYards = 5,
        ApproachDistanceMode = ScoringDistanceMode.Total
    };
    var totalState = engine.Start(PracticeGameKind.ApproachPractice, totalOptions, [], [], new Random(1));
    var rolloutGreen = engine.Evaluate(
        totalState,
        ValidShot() with { CarryYards = 150, TotalYards = 165, OfflineYards = 0 },
        new Random(1));
    True(rolloutGreen.Completed);
    True(rolloutGreen.Snapshot.HitTarget == true);
    Equal(ScoringDistanceMode.Total, rolloutGreen.Snapshot.Settings.ApproachDistanceMode);
    Equal(1, rolloutGreen.Shot.PracticeScore!.Value);
    Equal(0d, rolloutGreen.Shot.ProximityYards!.Value);
}

static void CheckSelectablePracticeModes()
{
    Equal(7, PracticeGameNames.SelectableGames.Count);
    True(PracticeGameNames.SelectableGames.Contains(PracticeGameKind.ApproachPractice));
    True(PracticeGameNames.SelectableGames.Contains(PracticeGameKind.GolfCombine));
}

static void CheckCombineFormats()
{
    Equal(12, CombineRules.TargetCount);
    Equal(36, CombineRules.TotalShots(CombineFormat.Quick));
    Equal(72, CombineRules.TotalShots(CombineFormat.Full));
    Equal(1, CombineRules.PassNumber(35));
    Equal(2, CombineRules.PassNumber(36));
    Equal(0, CombineRules.TargetIndex(0));
    Equal(1, CombineRules.TargetIndex(3));

    var wedge = new GolfClub { Name = "SW", Kind = GolfClubKind.Wedge, LoftDegrees = 56, StockCarryYards = 55 };
    var iron = new GolfClub { Name = "9 Iron", Kind = GolfClubKind.Iron, StockCarryYards = 105 };
    var driver = new GolfClub { Name = "Driver", Kind = GolfClubKind.Driver, StockCarryYards = 245 };
    var engine = new PracticeGameEngine();
    var state = engine.Start(PracticeGameKind.GolfCombine, new() { CombineFormat = CombineFormat.Quick }, [wedge, iron, driver], [], new Random(1));
    True(state.IsActive);
    Equal(55d, state.CurrentTargetYards!.Value);
    Equal(wedge.Id, state.RecommendedClubId!.Value);

    PracticeGameEvaluation? final = null;
    for (var index = 0; index < CombineRules.TotalShots(CombineFormat.Quick); index++)
    {
        var carry = state.CurrentTargetYards!.Value;
        final = engine.Evaluate(state, ValidShot() with { CarryYards = carry, OfflineYards = 0 }, new Random(1));
    }
    True(final!.Completed);
    Equal(100, final.Summary!.Score);
    Equal(36, final.Summary.Shots);
}

static void CheckCombineScoring()
{
    Equal(100, CombineRules.ScoreShot(100, 100, 0, false));
    True(CombineRules.ScoreShot(100, 100, 5, false) < CombineRules.ScoreShot(100, 105, 0, false));
    Equal(0, CombineRules.ScoreShot(100, 150, 0, false));
    Equal(100, CombineRules.ScoreShot(250, 225, 0, true));
    Equal(100, CombineRules.ScoreShot(250, 250, 0, true));
    Equal(100, CombineRules.ScoreShot(250, 275, 0, true));
    True(CombineRules.ScoreShot(250, 200, 0, true) < 100);
    True(CombineRules.ScoreShot(250, 250, 10, true) < 100);
}

static void CheckSkillsChallenge()
{
    var options = new PracticeGameOptions
    {
        SkillsStationCount = 5,
        SkillsMinimumDistanceYards = 20,
        SkillsMaximumDistanceYards = 50,
        SkillsMarginMultiplier = 1,
        SkillsDistanceMode = ScoringDistanceMode.Total
    };
    var engine = new PracticeGameEngine();
    var state = engine.Start(PracticeGameKind.SkillsAssessment, options, [], [], new Random(4));
    True(state.CurrentTargetYards is >= 20 and <= 50);

    PracticeGameEvaluation? final = null;
    for (var station = 0; station < 5; station++)
    {
        var target = state.CurrentTargetYards!.Value;
        final = engine.Evaluate(
            state,
            ValidShot() with { CarryYards = target - 15, TotalYards = target, OfflineYards = 0 },
            new Random(station + 10));
    }

    True(final!.Completed);
    Equal(5, final.Summary!.Shots);
    Equal(100, final.Summary.Score);
    Equal(5, final.Summary.TargetsHit);
    Equal(ScoringDistanceMode.Total, final!.Snapshot.Settings.SkillsDistanceMode);
    Equal(100, SkillsChallengeRules.ScoreProximity(100, 0, 1));
    Equal(25, SkillsChallengeRules.ScoreProximity(100, 8, 1));
    Equal(0, SkillsChallengeRules.ScoreProximity(100, 8.1, 1));
    True(SkillsChallengeRules.TargetRingRadius(100, 0.5) < SkillsChallengeRules.TargetRingRadius(100, 2));
}

static void CheckOptionalPracticeMapping()
{
    var engine = new PracticeGameEngine();

    var combine = engine.Start(
        PracticeGameKind.GolfCombine,
        new() { CombineDriverTargetYards = 215 },
        [],
        [],
        new Random(1));
    True(combine.IsActive);
    Equal(55d, combine.CurrentTargetYards!.Value);
    True(combine.RecommendedClubId is null);

    for (var index = 0; index < CombineRules.DistanceTargets.Count * CombineRules.ShotsPerTargetPerPass; index++)
        engine.Evaluate(combine, ValidShot() with { CarryYards = combine.CurrentTargetYards, OfflineYards = 0 }, new Random(1));
    True(combine.IsDriverTarget);
    Equal(215d, combine.CurrentTargetYards!.Value);
    True(combine.RecommendedClubId is null);

    var fairway = engine.Start(PracticeGameKind.FairwayFinder, new(), [], [], new Random(1));
    True(fairway.IsActive);
    True(fairway.RecommendedClubId is null);

    var skills = engine.Start(PracticeGameKind.SkillsAssessment, new(), [], [], new Random(1));
    True(skills.IsActive);
    True(skills.CurrentTargetYards is >= 50 and <= 180);
    True(skills.RecommendedClubId is null);
}

static void CheckFairwayFinder()
{
    var driver = new GolfClub { Name = "Driver", Kind = GolfClubKind.Driver, StockCarryYards = 240 };
    var iron = new GolfClub { Name = "5 Iron", Kind = GolfClubKind.Iron, StockCarryYards = 185 };
    var engine = new PracticeGameEngine();
    var options = new PracticeGameOptions
    {
        FairwayClubId = iron.Id,
        ShotGoal = 3,
        FairwayWidthYards = 20,
        MinimumCarryYards = 150
    };
    var state = engine.Start(PracticeGameKind.FairwayFinder, options, [driver, iron], [], new Random(1));
    Equal(iron.Id, state.RecommendedClubId!.Value);
    Equal("5 Iron", state.RecommendedClub!);

    engine.Evaluate(state, ValidShot() with { CarryYards = 160, OfflineYards = 5 }, new Random(1));
    Equal(iron.Id, state.RecommendedClubId!.Value);
    engine.Evaluate(state, ValidShot() with { CarryYards = 140, OfflineYards = 0 }, new Random(1));
    var final = engine.Evaluate(state, ValidShot() with { CarryYards = 170, OfflineYards = 15 }, new Random(1));

    True(final.Completed);
    Equal(33, final.Summary!.Score);
    Equal(1, final.Summary.TargetsHit);
    Equal(160d, state.Attempts[0].CarryYards!.Value);
    Equal(5d, state.Attempts[0].OfflineYards!.Value);
    var statistics = FairwayFinderRules.Calculate(state.Attempts);
    Equal(1, statistics.FairwaysHit);
    Equal(15d, statistics.DispersionWidthYards!.Value);

    var mapping = new PracticeSession
    {
        SessionType = ShotAnalytics.BagMappingSessionType,
        Shots = [StoredShot.FromShot(ValidShot() with { Club = driver.DisplayName, CarryYards = 245 }, driver.Id)]
    };
    var automatic = engine.Start(PracticeGameKind.FairwayFinder, new(), [driver, iron], [mapping], new Random(1));
    Equal(driver.Id, automatic.RecommendedClubId!.Value);
}

static void CheckFlightPath()
{
    var calculated = BallFlightCalculator.Calculate(ValidShot() with
    {
        CarryYards = null,
        TotalYards = null,
        OfflineYards = null,
        ApexYards = null,
        FlightTimeSeconds = null
    });
    True(calculated.CarryYards is > 0);
    True(calculated.FlightPath is { Count: > 2 });
}

static void CheckFlightPathInputs()
{
    var baseline = ValidShot() with
    {
        CarryYards = null,
        TotalYards = null,
        OfflineYards = null,
        ApexYards = null,
        FlightTimeSeconds = null
    };
    var lowLaunch = BallFlightCalculator.Calculate(baseline with { LaunchAngleDeg = 10 });
    var highLaunch = BallFlightCalculator.Calculate(baseline with { LaunchAngleDeg = 22 });
    var rightLaunch = BallFlightCalculator.Calculate(baseline with { LaunchDirectionDeg = 5 });

    True(highLaunch.ApexYards > lowLaunch.ApexYards);
    True(rightLaunch.OfflineYards > lowLaunch.OfflineYards);
}

static ShotData ValidShot() => new(
    DateTimeOffset.UtcNow,
    "7 Iron",
    155,
    162,
    2,
    25,
    5.2,
    118,
    17,
    0.5,
    5200,
    150,
    TotalSpinRpm: 5202,
    SpinAxisDeg: 1.6);

static void True(bool condition)
{
    if (!condition)
        throw new InvalidOperationException("Expected condition to be true.");
}

static void Equal<T>(T expected, T actual) where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Expected {expected}; got {actual}.");
}
