using GolfGrind.App.Components.Shared;
using GolfGrind.App.Services;
using GolfGrind.Core.Abstractions;
using GolfGrind.Core.Models;
using GolfGrind.Core.Services;
using Microsoft.AspNetCore.Components;

namespace GolfGrind.App.Components.Pages;

public partial class Home
{
    private static readonly string[] AppViews = ["Range", "Practice", "Bag Mapping", "Wedge Matrix", "Analytics", "Golf Bag", "Sessions", "Golfers"];
    private List<GolfClub> clubs = [];
    private List<GolferProfile> profiles = [];
    private string activeView = "Range";
    private Guid selectedClubId;
    private Guid selectedProfileId;
    private Guid loadedProfileId;
    private Guid? pendingDeleteProfileId;
    private string newProfileName = "";
    private string? profileMessage;
    private Guid analyticsClubId;
    private Guid? selectedHistorySessionId;
    private Guid? pendingDeleteWholeSessionId;
    private Guid? pendingDeleteSessionId;
    private DateTimeOffset? pendingDeleteShotAt;
    private string newClubName = "Wedge";
    private GolfClubKind newClubKind = GolfClubKind.Wedge;
    private double? newClubLoft = 56;
    private double? newClubStockCarry;
    private string selectedSwingType = "Half";
    private string? bagMessage;
    private readonly BagMappingGuideState bagGuide = new();
    private double suggestedDistanceYards = 150;
    private readonly PracticeGameOptions practiceGameOptions = new();
    private PracticeGameState practiceGameState = new();
    private PracticeGameSummary? pendingPracticeSummary;
    private readonly WedgeMatrixGuideState wedgeGuide = new();
    private GolfGrindBackup? pendingBackup;
    private string? backupMessage;
    private string? sessionToast;
    private int sessionToastVersion;
    private string monitorMode => MonitorWorkspace.MonitorMode;
    private bool keepScreenAwake => MonitorWorkspace.KeepScreenAwake;
    private bool audibleReadyNotification => MonitorWorkspace.AudibleReadyNotification;
    private LaunchMonitorStatus status => MonitorWorkspace.Status;
    private int acceptedShotCount => MonitorWorkspace.AcceptedShotCount;
    private int rejectedShotCount => MonitorWorkspace.RejectedShotCount;
    private int duplicateShotCount => MonitorWorkspace.DuplicateShotCount;
    private string? shotDiagnosticMessage => MonitorWorkspace.ShotDiagnosticMessage;
    private MockLaunchMonitor MockMonitor => MonitorWorkspace.MockMonitor;
    private PracticeSession currentSession => ActivitySessions.Current;
    private ShotData? lastShot => ActivitySessions.LastShot;
    private bool ProfileChangeDisabled => practiceGameState.IsActive || bagGuide.IsActive || wedgeGuide.IsActive;
    private string ActiveProfileName => profiles.FirstOrDefault(profile => profile.Id == selectedProfileId)?.Name ?? "Golfer";
    private ILaunchMonitor ActiveMonitor => MonitorWorkspace.ActiveMonitor;
    private GolfClub? SelectedClub => clubs.FirstOrDefault(club => club.Id == selectedClubId);
    private GolfClub? SelectedAnalyticsClub => clubs.FirstOrDefault(club => club.Id == analyticsClubId);
    private IReadOnlyList<ClubStatistics> BagStatistics =>
        ShotAnalytics.CalculateBag(clubs, SessionStorage.GetSessions());
    private double? SelectedAnalyticsStockCarry =>
        BagStatistics.FirstOrDefault(stat => stat.Club.Id == analyticsClubId)?.StockCarryYards;
    private IReadOnlyList<StoredShot> DispersionShots
    {
        get
        {
            if (SelectedAnalyticsClub is null)
                return [];
            return ShotAnalytics.DispersionShots(SelectedAnalyticsClub, SessionStorage.GetSessions());
        }
    }
    private IReadOnlyList<GolfClub> WedgeClubs => clubs.Where(club => club.Kind == GolfClubKind.Wedge).ToList();
    private string BagGuidePrompt => bagGuide.CurrentClubId is { } clubId
        ? clubs.FirstOrDefault(club => club.Id == clubId)?.DisplayName ?? "Unknown club"
        : "Complete";
    private string? BagGuideMessage => bagGuide.Status switch
    {
        BagMappingGuideStatus.NeedsClubSelection => "Select at least one club to map.",
        BagMappingGuideStatus.Stopped => "Bag mapping stopped. Recorded shots remain saved.",
        BagMappingGuideStatus.Completed => "Guided bag mapping complete. Review the results below, then enter any preferred stock carries and save the bag.",
        BagMappingGuideStatus.LeftView => "Bag mapping stopped because you left the Bag Mapping section.",
        _ => null
    };
    private string SuggestedClub
    {
        get
        {
            var match = BagInsights.FindNearestClub(BagStatistics, suggestedDistanceYards);
            return match is null
                ? "Record some shots first"
                : $"{match.Club.DisplayName} · {match.CarryYards:0.0} yd";
        }
    }
    private string WedgeGuidePrompt => wedgeGuide.Current(WedgeClubs) is { } selection
        ? $"{WedgeClubs.First(wedge => wedge.Id == selection.ClubId).DisplayName} · {SwingLabel(selection.SwingType)} swing"
        : "Complete";
    private string? WedgeGuideMessage => wedgeGuide.Status switch
    {
        WedgeMatrixGuideStatus.NeedsWedge => "Add at least one wedge to your bag first.",
        WedgeMatrixGuideStatus.Stopped => "Guided matrix stopped. Recorded shots remain saved.",
        WedgeMatrixGuideStatus.Completed => "Guided wedge matrix complete.",
        WedgeMatrixGuideStatus.LeftView => "Guided matrix stopped because you left the Wedge Matrix section.",
        _ => null
    };
    protected override void OnInitialized()
    {
        profiles = ProfileService.GetProfiles().ToList();
        selectedProfileId = ProfileService.ActiveProfileId;
        loadedProfileId = selectedProfileId;
        clubs = BagService.Load(selectedProfileId);
        SessionStorage.SwitchProfile(selectedProfileId);
        MonitorWorkspace.Initialize();
        ActivitySessions.Initialize();
        selectedClubId = clubs.FirstOrDefault(club => club.Name.Equals("7 Iron", StringComparison.OrdinalIgnoreCase))?.Id
            ?? clubs[0].Id;
        analyticsClubId = selectedClubId;
        bagGuide.ResetSelection(clubs);
        MonitorWorkspace.SquareMonitor.ShotReceived += OnShotReceived;
        MonitorWorkspace.SquareMonitor.StatusChanged += OnStatusChanged;
        MockMonitor.ShotReceived += OnShotReceived;
        MockMonitor.StatusChanged += OnStatusChanged;
    }

    private async Task SetClubAsync(Guid clubId)
    {
        selectedClubId = clubId;
        await ClubChangedAsync();
    }

    private void SetSwingType(string swingType) => selectedSwingType = swingType;
    private void SetWedgeGuideShotsPerCell(int value) => wedgeGuide.ShotsPerCell = value;
    private void SetBagGuideShotsPerClub(int value) => bagGuide.ShotsPerClub = value;
    private void SetSuggestedDistance(double value) => suggestedDistanceYards = value;
    private void SetAnalyticsClub(Guid value) => analyticsClubId = value;

    private async Task SetMonitorModeAsync(string mode)
    {
        await MonitorWorkspace.SetMonitorModeAsync(mode);
    }

    private void SetKeepScreenAwake(bool enabled)
    {
        MonitorWorkspace.SetKeepScreenAwake(enabled);
    }

    private void SetAudibleReadyNotification(bool enabled) =>
        MonitorWorkspace.SetAudibleReadyNotification(enabled);

    private async Task ToggleConnectionAsync()
    {
        await MonitorWorkspace.ToggleConnectionAsync(SelectedClub?.DisplayName ?? "7 Iron");
    }

    private Task ClubChangedAsync() => MonitorWorkspace.SelectClubAsync(SelectedClub?.DisplayName ?? "7 Iron");
    private void SimulateShot() => MonitorWorkspace.SimulateShot();
    private async Task SelectViewAsync(string view)
    {
        if (view != "Wedge Matrix" && wedgeGuide.IsActive)
            wedgeGuide.Stop(WedgeMatrixGuideStatus.LeftView);
        if (view != "Bag Mapping" && bagGuide.IsActive)
            bagGuide.Stop(BagMappingGuideStatus.LeftView);
        if (view != "Practice" && practiceGameState.IsActive)
            EndPracticeGame();
        activeView = view;
        EnsureSessionForCurrentActivity();
        if (view == "Wedge Matrix" && SelectedClub?.Kind != GolfClubKind.Wedge && WedgeClubs.FirstOrDefault() is { } wedge)
        {
            selectedClubId = wedge.Id;
            await ClubChangedAsync();
        }
    }
    private void OnShotReceived(object? sender, ShotData shot)
    {
        _ = InvokeAsync(async () =>
        {
            var club = SelectedClub;
            var swingType = activeView == "Wedge Matrix" && club?.Kind == GolfClubKind.Wedge
                ? selectedSwingType
                : "Full";
            var processed = MonitorWorkspace.Process(sender, shot, swingType);
            if (!processed.Accepted || processed.Shot is null)
            {
                StateHasChanged();
                return;
            }

            var retainedShot = processed.Shot;
            retainedShot = ApplyPracticeGameResult(retainedShot);
            ActivitySessions.Record(retainedShot, club?.Id);
            if (pendingPracticeSummary is not null)
            {
                ActivitySessions.SaveCompletedPractice(pendingPracticeSummary);
                pendingPracticeSummary = null;
            }
            await AdvanceWedgeGuideAsync();
            await AdvanceBagGuideAsync();
            StateHasChanged();
        });
    }

    private void OnStatusChanged(object? sender, LaunchMonitorStatus newStatus)
    {
        _ = InvokeAsync(() =>
        {
            if (!MonitorWorkspace.UpdateStatus(sender, newStatus))
                return;
            StateHasChanged();
        });
    }

    public void Dispose()
    {
        MonitorWorkspace.DeactivateDisplay();
        MonitorWorkspace.SquareMonitor.ShotReceived -= OnShotReceived;
        MonitorWorkspace.SquareMonitor.StatusChanged -= OnStatusChanged;
        MockMonitor.ShotReceived -= OnShotReceived;
        MockMonitor.StatusChanged -= OnStatusChanged;
    }
}
