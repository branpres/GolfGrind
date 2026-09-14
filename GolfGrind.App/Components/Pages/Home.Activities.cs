using GolfGrind.App.Components.Shared;
using GolfGrind.Core.Models;
using GolfGrind.Core.Services;

namespace GolfGrind.App.Components.Pages;

public partial class Home
{
    private void AddClub()
    {
        var name = string.IsNullOrWhiteSpace(newClubName) ? newClubKind.ToString() : newClubName.Trim();
        var club = new GolfClub
        {
            Name = name,
            Kind = newClubKind,
            LoftDegrees = newClubLoft,
            StockCarryYards = newClubStockCarry
        };
        clubs.Add(club);
        if (club.Kind != GolfClubKind.Putter)
            bagGuide.SelectedClubIds.Add(club.Id);
        selectedClubId = club.Id;
        if (analyticsClubId == Guid.Empty)
            analyticsClubId = club.Id;
        bagMessage = "Club added. Save the bag to keep your changes.";
    }

    private void AddClubFromPanel(NewGolfClubRequest request)
    {
        newClubName = request.Name;
        newClubKind = request.Kind;
        newClubLoft = request.LoftDegrees;
        newClubStockCarry = request.StockCarryYards;
        AddClub();
    }

    private void RemoveClub(Guid clubId)
    {
        if (clubs.Count <= 1)
            return;

        clubs.RemoveAll(club => club.Id == clubId);
        bagGuide.SelectedClubIds.Remove(clubId);
        if (clubs.All(club => club.Id != selectedClubId))
            selectedClubId = clubs[0].Id;
        if (clubs.All(club => club.Id != analyticsClubId))
            analyticsClubId = clubs[0].Id;
        bagMessage = "Club removed. Save the bag to keep your changes.";
    }

    private async Task SaveBagAsync()
    {
        try
        {
            BagService.Save(selectedProfileId, clubs);
            bagMessage = "Bag saved.";
            await ClubChangedAsync();
        }
        catch (InvalidOperationException exception)
        {
            bagMessage = exception.Message;
        }
    }

    private void ApplyCalculatedCarries()
    {
        BagInsights.ApplyMedianCarries(clubs, BagStatistics);
        BagService.Save(selectedProfileId, clubs);
        bagMessage = "Calculated medians were saved as stock carries.";
    }

    private void SelectPracticeGame(PracticeGameKind game)
    {
        if (practiceGameState.IsActive)
            return;
        practiceGameState = new PracticeGameState { Game = game };
    }

    private void StartPracticeGame()
    {
        if (bagGuide.IsActive) bagGuide.Stop();
        if (wedgeGuide.IsActive) wedgeGuide.Stop();
        practiceGameState = PracticeGameEngine.Start(
            practiceGameState.Game,
            practiceGameOptions,
            clubs,
            SessionStorage.GetSessions());
        if (!practiceGameState.IsActive)
            return;

        var gameName = practiceGameState.Game.DisplayName();
        BeginActivitySession(gameName, gameName);
        ActivitySessions.ConfigurePractice(practiceGameState);
        pendingPracticeSummary = null;
    }

    private void EndPracticeGame()
    {
        if (!practiceGameState.IsActive)
            return;
        var summary = PracticeGameEngine.EndEarly(practiceGameState);
        ActivitySessions.CompletePractice(summary);
        pendingPracticeSummary = null;
    }

    private string PracticePersonalBest(PracticeGameKind game)
    {
        var completed = PracticeHistory.CompletedSummaries(SessionStorage.GetSessions(), game);
        if (completed.Count == 0)
            return "No completed score yet";
        return game switch
        {
            PracticeGameKind.PressureStreak => $"{completed.Max(summary => summary.BestStreak)} shots",
            PracticeGameKind.FairwayFinder => $"{completed.Max(summary => summary.Score)}%",
            PracticeGameKind.GolfCombine or PracticeGameKind.SkillsAssessment => $"{completed.Max(summary => summary.Score)}/100",
            _ => $"{completed.Max(summary => summary.Score)} points"
        };
    }

    private ShotData ApplyPracticeGameResult(ShotData shot)
    {
        if (!practiceGameState.IsActive)
            return shot;

        var evaluation = PracticeGameEngine.Evaluate(practiceGameState, shot);
        if (evaluation.Summary is not null)
            pendingPracticeSummary = evaluation.Summary;

        return evaluation.Shot;
    }

    private void ToggleBagGuideClub(BagClubToggle toggle)
    {
        bagGuide.Toggle(toggle.ClubId, toggle.Selected);
    }

    private void SelectAllBagGuideClubs() => bagGuide.SelectAll(clubs);

    private void DeselectAllBagGuideClubs() => bagGuide.DeselectAll();

    private async Task StartBagGuideAsync()
    {
        if (!bagGuide.Start(clubs, DateTimeOffset.Now))
            return;
        if (wedgeGuide.IsActive) wedgeGuide.Stop();
        BeginActivitySession("Bag Mapping", "Bag mapping");
        await ApplyBagGuideSelectionAsync();
    }

    private void StopBagGuide() => bagGuide.Stop();

    private async Task AdvanceBagGuideAsync()
    {
        if (!bagGuide.IsActive)
            return;
        if (!bagGuide.Advance(currentSession.Shots))
        {
            await ApplyBagGuideSelectionAsync();
            return;
        }
    }

    private Task ApplyBagGuideSelectionAsync()
    {
        var clubId = bagGuide.CurrentClubId ?? throw new InvalidOperationException("Bag Mapping has no current club.");
        selectedClubId = clubId;
        return ClubChangedAsync();
    }

    private async Task StartWedgeGuideAsync()
    {
        if (!wedgeGuide.Start(WedgeClubs, selectedClubId, selectedSwingType))
            return;
        if (bagGuide.IsActive) bagGuide.Stop();
        BeginActivitySession("Wedge Matrix", "Wedge matrix");
        await ApplyWedgeGuideSelectionAsync();
    }

    private void StopWedgeGuide() => wedgeGuide.Stop();

    private async Task AdvanceWedgeGuideAsync()
    {
        if (!wedgeGuide.IsActive)
            return;
        if (wedgeGuide.Advance(WedgeClubs.Count))
            return;
        await ApplyWedgeGuideSelectionAsync();
    }

    private async Task ApplyWedgeGuideSelectionAsync()
    {
        var selection = wedgeGuide.Current(WedgeClubs) ?? throw new InvalidOperationException("Wedge Matrix has no current cell.");
        selectedClubId = selection.ClubId;
        selectedSwingType = selection.SwingType;
        await ClubChangedAsync();
    }

    private static string SwingLabel(string? swingType) => ShotAnalytics.NormalizeSwing(swingType) switch
    {
        "Quarter" => "¼",
        "Half" => "½",
        "ThreeQuarter" => "¾",
        _ => "Full"
    };

    private double? GapValue(ClubStatistics stat) => BagInsights.GapToNext(stat, BagStatistics);
    private string GapToNext(ClubStatistics stat) => GapValue(stat) is { } gap ? $"{gap:0.0} yd" : "—";
    private string GapClass(ClubStatistics stat) => GapValue(stat) switch
    {
        < 5 => "gap-overlap",
        > 20 => "gap-large",
        _ => ""
    };

    private bool IsPotentialOutlier(string clubName, string? swingType, double? carryYards) =>
        ShotQualityRules.IsPotentialOutlier(clubName, swingType, carryYards, BagStatistics);
}
