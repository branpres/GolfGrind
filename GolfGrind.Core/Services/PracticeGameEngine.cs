using GolfGrind.Core.Models;

namespace GolfGrind.Core.Services;

public sealed class PracticeGameEngine
{
    public PracticeGameState Start(
        PracticeGameKind game,
        PracticeGameOptions options,
        IReadOnlyList<GolfClub> clubs,
        IReadOnlyList<PracticeSession> sessions,
        Random? random = null)
    {
        options.Clamp();
        if (game == PracticeGameKind.FairwayFinder &&
            options.FairwayClubId is { } selectedClubId &&
            clubs.All(club => club.Id != selectedClubId || club.Kind == GolfClubKind.Putter))
        {
            options.FairwayClubId = null;
        }

        var state = new PracticeGameState
        {
            Game = game,
            Settings = options.Snapshot(),
            IsActive = true
        };

        state.TargetPool = BuildTargetPool(game, clubs, sessions, options.FairwayClubId);

        SelectNextTarget(state, random ?? Random.Shared);
        return state;
    }

    public PracticeGameEvaluation Evaluate(
        PracticeGameState state,
        ShotData shot,
        Random? random = null)
    {
        if (!state.IsActive)
            throw new InvalidOperationException("The practice game is not active.");

        var settings = state.Settings;
        var target = state.CurrentTargetYards;
        var useTotalDistance = (state.Game == PracticeGameKind.ApproachPractice &&
                                settings.ApproachDistanceMode == ScoringDistanceMode.Total) ||
                               (state.Game == PracticeGameKind.SkillsAssessment &&
                                settings.SkillsDistanceMode == ScoringDistanceMode.Total);
        var measuredDistance = useTotalDistance
            ? shot.TotalYards
            : shot.CarryYards;
        var proximity = target is not null && measuredDistance is not null && shot.OfflineYards is not null
            ? Math.Sqrt(Math.Pow(measuredDistance.Value - target.Value, 2) + Math.Pow(shot.OfflineYards.Value, 2))
            : (double?)null;

        var hit = IsHit(state, shot, proximity);
        var score = Score(state, shot, proximity, hit);
        if (state.Game is PracticeGameKind.GolfCombine or PracticeGameKind.SkillsAssessment)
            hit = score > 0;
        var category = CurrentCategory(state);
        var shotNumber = state.ShotsTaken + 1;
        var resultLabel = state.Game switch
        {
            PracticeGameKind.GolfCombine => $"{score}/100",
            PracticeGameKind.SkillsAssessment => $"{score}/100",
            PracticeGameKind.ApproachPractice when hit => "Green",
            PracticeGameKind.ApproachPractice => "Missed green",
            _ => hit ? "Hit" : "Miss"
        };
        var snapshot = new PracticeGameShotSnapshot(
            state.Game,
            settings,
            shotNumber,
            target,
            state.RecommendedClubId,
            state.RecommendedClub,
            state.RecommendedSwing,
            state.IsDriverTarget,
            hit,
            resultLabel);

        state.ShotsTaken = shotNumber;
        state.TotalScore += score;
        state.LastProximityYards = proximity;
        state.LastResultLabel = resultLabel;
        if (hit)
        {
            state.TargetsHit++;
            state.CurrentStreak++;
            state.BestStreak = Math.Max(state.BestStreak, state.CurrentStreak);
        }
        else
        {
            state.Misses++;
            state.CurrentStreak = 0;
        }

        state.Attempts.Add(new(
            shotNumber,
            target,
            proximity is null ? null : Math.Round(proximity.Value, 1),
            score,
            hit,
            category,
            shot.CarryYards,
            shot.OfflineYards));

        Advance(state, shot, hit, random ?? Random.Shared);
        var enriched = shot with
        {
            PracticeMode = state.Game.StorageName(),
            TargetYards = target,
            ProximityYards = proximity is null ? null : Math.Round(proximity.Value, 1),
            PracticeScore = score,
            PracticeGame = snapshot
        };

        var summary = state.IsComplete ? BuildSummary(state) : null;
        if (summary is not null)
            state.Message = summary.Message;
        return new(enriched, snapshot, state.IsComplete, summary);
    }

    public PracticeGameSummary EndEarly(PracticeGameState state)
    {
        state.IsActive = false;
        state.IsComplete = false;
        var summary = BuildSummary(state, completed: false);
        state.Message = summary.Message;
        return summary;
    }

    private static List<PracticeGameTarget> BuildTargetPool(
        PracticeGameKind game,
        IReadOnlyList<GolfClub> clubs,
        IReadOnlyList<PracticeSession> sessions,
        Guid? fairwayClubId)
    {
        var mapped = ShotAnalytics.CalculateBag(clubs, sessions)
            .Where(statistics => statistics.Club.Kind != GolfClubKind.Putter)
            .Where(statistics => statistics.StockCarryYards is >= 10 and <= 350)
            .Select(statistics => new PracticeGameTarget(
                statistics.StockCarryYards!.Value,
                statistics.Club.Id,
                statistics.Club.DisplayName,
                "Full",
                Band(statistics.StockCarryYards.Value),
                statistics.Club.Kind))
            .OrderBy(item => item.Yards)
            .ToList();

        if (game == PracticeGameKind.FairwayFinder)
        {
            if (fairwayClubId is { } selectedId && clubs.FirstOrDefault(club => club.Id == selectedId) is { } selectedClub)
            {
                var mappedSelection = mapped.FirstOrDefault(target => target.ClubId == selectedId);
                return mappedSelection is not null
                    ? [mappedSelection]
                    : [new(
                        selectedClub.StockCarryYards ?? 0,
                        selectedClub.Id,
                        selectedClub.DisplayName,
                        "Full",
                        "Long game",
                        selectedClub.Kind)];
            }

            return mapped
                .Where(target => target.ClubKind is GolfClubKind.Driver or GolfClubKind.Wood or GolfClubKind.Hybrid)
                .ToList();
        }

        var matrixTargets = clubs
            .Where(club => club.Kind == GolfClubKind.Wedge)
            .SelectMany(club => new[] { "Half", "ThreeQuarter", "Full" }
                .Select(swing => new
                {
                    Club = club,
                    Swing = swing,
                    Cell = ShotAnalytics.CalculateWedgeCell(
                        ShotAnalytics.WedgeMatrixShotsForClub(club, sessions),
                        swing)
                }))
            .Where(item => item.Cell.MedianCarryYards is >= 10 and <= 350)
            .Select(item => new PracticeGameTarget(
                item.Cell.MedianCarryYards!.Value,
                item.Club.Id,
                item.Club.DisplayName,
                item.Swing,
                Band(item.Cell.MedianCarryYards.Value),
                item.Club.Kind))
            .ToList();

        var wedgeFullIds = matrixTargets
            .Where(target => target.SwingType == "Full" && target.ClubId is not null)
            .Select(target => target.ClubId!.Value)
            .ToHashSet();
        mapped.RemoveAll(target => target.ClubKind == GolfClubKind.Wedge &&
                                   target.ClubId is { } clubId &&
                                   wedgeFullIds.Contains(clubId));
        mapped.AddRange(matrixTargets);
        return mapped.OrderBy(target => target.Yards).ToList();
    }

    private static void SelectNextTarget(PracticeGameState state, Random random)
    {
        PracticeGameTarget? selected = state.Game switch
        {
            PracticeGameKind.TargetPractice => DistanceTarget(state, state.Settings.FixedTargetYards),
            PracticeGameKind.DistanceLadder => DistanceTarget(
                state,
                state.ShotsTaken == 0 ? state.Settings.LadderStartYards : state.CurrentTargetYards ?? state.Settings.LadderStartYards,
                "Ladder"),
            PracticeGameKind.GolfCombine => CombineTarget(state),
            PracticeGameKind.ApproachPractice => ApproachTarget(state),
            PracticeGameKind.PressureStreak => RandomDistanceTarget(state, random),
            PracticeGameKind.SkillsAssessment => SkillsTarget(state, random),
            PracticeGameKind.FairwayFinder => FairwayTarget(state),
            _ => RandomFromPool(state, random)
        };

        state.CurrentTargetYards = state.Game == PracticeGameKind.FairwayFinder ? null : selected?.Yards;
        state.RecommendedClubId = selected?.ClubId;
        state.RecommendedClub = selected?.ClubName;
        state.RecommendedSwing = selected?.SwingType;
        state.IsDriverTarget = selected?.IsDriverTarget ?? false;
    }

    private static PracticeGameTarget? CombineTarget(PracticeGameState state)
    {
        var targetIndex = CombineRules.TargetIndex(state.ShotsTaken);
        if (targetIndex >= CombineRules.DistanceTargets.Count)
        {
            var driver = state.TargetPool.FirstOrDefault(target => target.ClubKind == GolfClubKind.Driver);
            return new(
                state.Settings.CombineDriverTargetYards,
                driver?.ClubId,
                driver?.ClubName,
                driver?.SwingType,
                "Long game",
                GolfClubKind.Driver,
                true);
        }

        var yards = CombineRules.DistanceTargets[targetIndex];
        var recommendation = state.TargetPool.MinBy(target => Math.Abs(target.Yards - yards));
        return new(
            yards,
            recommendation?.ClubId,
            recommendation?.ClubName,
            recommendation?.SwingType,
            Band(yards),
            recommendation?.ClubKind);
    }

    private static PracticeGameTarget ApproachTarget(PracticeGameState state)
    {
        return DistanceTarget(state, state.Settings.FixedTargetYards);
    }

    private static PracticeGameTarget? FairwayTarget(PracticeGameState state)
    {
        if (state.Settings.FairwayClubId is { } selectedId)
            return state.TargetPool.FirstOrDefault(target => target.ClubId == selectedId);
        return state.TargetPool.FirstOrDefault(target => target.ClubKind == GolfClubKind.Driver)
               ?? state.TargetPool.MaxBy(target => target.Yards);
    }

    private static PracticeGameTarget RandomDistanceTarget(PracticeGameState state, Random random)
    {
        var yards = random.Next(state.Settings.MinimumTargetYards, state.Settings.MaximumTargetYards + 1);
        return DistanceTarget(state, yards);
    }

    private static PracticeGameTarget DistanceTarget(PracticeGameState state, double yards, string? category = null)
    {
        var recommendation = state.TargetPool.MinBy(target => Math.Abs(target.Yards - yards));
        return new(
            yards,
            recommendation?.ClubId,
            recommendation?.ClubName,
            recommendation?.SwingType,
            category ?? Band(yards),
            recommendation?.ClubKind);
    }

    private static PracticeGameTarget? RandomFromPool(PracticeGameState state, Random random)
    {
        var filtered = state.TargetPool.Count > 1 && state.RecommendedClubId is not null
            ? state.TargetPool.Where(target => target.ClubId != state.RecommendedClubId).ToList()
            : state.TargetPool;
        var choices = filtered.Count == 0 ? state.TargetPool : filtered;
        return choices.Count == 0 ? null : choices[random.Next(choices.Count)];
    }

    private static PracticeGameTarget SkillsTarget(PracticeGameState state, Random random)
    {
        var minimum = (int)Math.Ceiling(state.Settings.SkillsMinimumDistanceYards);
        var maximum = (int)Math.Floor(state.Settings.SkillsMaximumDistanceYards);
        var yards = random.Next(minimum, maximum + 1);
        return DistanceTarget(state, yards);
    }

    private static bool IsHit(PracticeGameState state, ShotData shot, double? proximity)
    {
        if (state.Game == PracticeGameKind.FairwayFinder)
            return shot.OfflineYards is { } offline && shot.CarryYards is { } carry &&
                   Math.Abs(offline) <= state.Settings.FairwayWidthYards / 2 &&
                   carry >= state.Settings.MinimumCarryYards;
        if (state.Game == PracticeGameKind.DistanceLadder)
            return shot.CarryYards is { } ladderCarry && state.CurrentTargetYards is { } ladderTarget &&
                   Math.Abs(ladderCarry - ladderTarget) <= state.Settings.ToleranceYards;
        if (state.Game == PracticeGameKind.SkillsAssessment && state.CurrentTargetYards is { } skillsTarget)
            return proximity is not null &&
                   proximity <= SkillsChallengeRules.TargetRingRadius(skillsTarget, state.Settings.SkillsMarginMultiplier);
        return proximity is not null && proximity <= state.Settings.ToleranceYards;
    }

    private static int Score(PracticeGameState state, ShotData shot, double? proximity, bool hit)
    {
        if (state.Game == PracticeGameKind.GolfCombine && state.CurrentTargetYards is { } combineTarget)
            return CombineRules.ScoreShot(combineTarget, shot.CarryYards, shot.OfflineYards, state.IsDriverTarget);
        if (state.Game == PracticeGameKind.SkillsAssessment && state.CurrentTargetYards is { } skillsTarget)
            return SkillsChallengeRules.ScoreProximity(skillsTarget, proximity, state.Settings.SkillsMarginMultiplier);
        if (state.Game == PracticeGameKind.FairwayFinder)
            return hit ? 10 : 0;
        if (state.Game == PracticeGameKind.ApproachPractice)
            return hit ? 1 : 0;
        if (proximity is null)
            return 0;
        return proximity.Value switch
        {
            var value when value <= state.Settings.ToleranceYards => 10,
            var value when value <= state.Settings.ToleranceYards * 2 => 7,
            var value when value <= state.Settings.ToleranceYards * 3 => 4,
            var value when value <= state.Settings.ToleranceYards * 4 => 1,
            _ => 0
        };
    }

    private static void Advance(PracticeGameState state, ShotData shot, bool hit, Random random)
    {
        if (state.Game == PracticeGameKind.DistanceLadder && hit)
        {
            var next = (state.CurrentTargetYards ?? state.Settings.LadderStartYards) + state.Settings.LadderStepYards;
            if (next > state.Settings.LadderEndYards)
            {
                Complete(state);
                return;
            }
            state.CurrentTargetYards = next;
            var recommendation = state.TargetPool.MinBy(target => Math.Abs(target.Yards - next));
            state.RecommendedClubId = recommendation?.ClubId;
            state.RecommendedClub = recommendation?.ClubName;
            state.RecommendedSwing = recommendation?.SwingType;
            return;
        }

        if (state.Game == PracticeGameKind.PressureStreak && state.Misses >= state.Settings.MissesAllowed)
        {
            Complete(state);
            return;
        }

        var goal = state.Game switch
        {
            PracticeGameKind.SkillsAssessment => state.Settings.SkillsStationCount,
            PracticeGameKind.GolfCombine => CombineRules.TotalShots(state.Settings.CombineFormat),
            _ => state.Settings.ShotGoal
        };
        if (state.Game != PracticeGameKind.DistanceLadder && state.Game != PracticeGameKind.PressureStreak && state.ShotsTaken >= goal)
        {
            Complete(state);
            return;
        }

        state.SequenceIndex++;
        SelectNextTarget(state, random);
    }

    private static void Complete(PracticeGameState state)
    {
        state.IsActive = false;
        state.IsComplete = true;
    }

    private static PracticeGameSummary BuildSummary(PracticeGameState state, bool? completed = null)
    {
        var average = state.Attempts.Where(attempt => attempt.ProximityYards is not null)
            .Select(attempt => attempt.ProximityYards!.Value)
            .DefaultIfEmpty()
            .Average();
        var hasProximity = state.Attempts.Any(attempt => attempt.ProximityYards is not null);
        var finalCompleted = completed ?? state.IsComplete;
        var lead = finalCompleted ? $"{state.Game.DisplayName()} complete" : $"{state.Game.DisplayName()} ended";
        var isHundredPointMode = state.Game is PracticeGameKind.GolfCombine or PracticeGameKind.SkillsAssessment;
        var fairwayStatistics = FairwayFinderRules.Calculate(state.Attempts);
        var isPercentageMode = state.Game == PracticeGameKind.FairwayFinder;
        var summaryScore = isPercentageMode
            ? (int)Math.Round(fairwayStatistics.HitPercentage, MidpointRounding.AwayFromZero)
            : isHundredPointMode && state.ShotsTaken > 0
            ? (int)Math.Round(state.Attempts.Average(attempt => attempt.Score), MidpointRounding.AwayFromZero)
            : state.TotalScore;
        var detail = state.Game == PracticeGameKind.GolfCombine
            ? CombineDetail(state)
            : state.Game == PracticeGameKind.SkillsAssessment
            ? $"{state.TargetsHit} of {state.ShotsTaken} stations scored"
            : state.Game == PracticeGameKind.ApproachPractice
            ? $"{state.TargetsHit} of {state.ShotsTaken} greens hit"
            : state.Game == PracticeGameKind.PressureStreak
            ? $"best streak {state.BestStreak}; {state.Misses} misses"
            : state.Game == PracticeGameKind.FairwayFinder
                ? $"{state.TargetsHit} of {state.ShotsTaken} fairways; {fairwayStatistics.DispersionWidthYards?.ToString("0.0") ?? "—"} yd lateral spread"
                : hasProximity
                    ? $"{average:0.0} yd average proximity"
                    : $"{state.TargetsHit} targets hit";
        var message = isPercentageMode
            ? $"{lead}: {summaryScore}% fairways hit over {state.ShotsTaken} shots · {detail}."
            : isHundredPointMode
            ? $"{lead}: {summaryScore}/100 over {state.ShotsTaken} {(state.Game == PracticeGameKind.SkillsAssessment ? "stations" : "shots")} · {detail}."
            : $"{lead}: {summaryScore} {(summaryScore == 1 ? "point" : "points")} over {state.ShotsTaken} shots · {detail}.";
        return new(
            state.Game,
            finalCompleted,
            state.ShotsTaken,
            summaryScore,
            hasProximity ? Math.Round(average, 1) : null,
            state.BestStreak,
            state.TargetsHit,
            DateTimeOffset.Now,
            message);
    }

    private static string CombineDetail(PracticeGameState state)
    {
        var bands = state.Attempts
            .GroupBy(attempt => attempt.Category)
            .Select(group => new { Name = group.Key, Average = group.Average(item => item.Score) })
            .OrderByDescending(group => group.Average)
            .ToList();
        var strongest = bands.Count == 0 ? "—" : bands[0].Name;
        var weakest = bands.Count < 2 ? "—" : bands[^1].Name;
        var average = state.Attempts.Count == 0 ? 0 : state.Attempts.Average(attempt => attempt.Score);
        return $"{average:0.0}/100 average; strongest {strongest}; work on {weakest}";
    }

    private static string CurrentCategory(PracticeGameState state) => state.Game == PracticeGameKind.FairwayFinder
        ? "Long game"
        : state.CurrentTargetYards is { } target ? Band(target) : "Fairway";

    private static string Band(double target) => target <= 100 ? "Wedges" : target <= 175 ? "Approach" : "Long game";
}
