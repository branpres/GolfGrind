using GolfGrind.App.Components.Shared;
using GolfGrind.Core.Models;
using GolfGrind.Core.Services;

namespace GolfGrind.App.Components.Pages;

public partial class Home
{
    private void ToggleSessionDetails(Guid sessionId) =>
        selectedHistorySessionId = selectedHistorySessionId == sessionId ? null : sessionId;

    private void HandleToggleExclusion(ShotSelection shot) =>
        ToggleAnalyticsExclusion(shot.SessionId, shot.HitAt);

    private Task HandleDeleteShot(ShotSelection shot) =>
        RequestDeleteShot(shot.SessionId, shot.HitAt);

    private async Task ExportSessionAsync(PracticeSession session)
    {
        var toastVersion = ++sessionToastVersion;
        try
        {
            var path = BackupService.ExportSessionCsv(ActiveProfileName, session);
            sessionToast = $"{SessionTitle(session)} exported to {path}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            sessionToast = $"Session CSV could not be written: {exception.Message}";
        }

        await InvokeAsync(StateHasChanged);
        await Task.Delay(TimeSpan.FromSeconds(3));
        if (sessionToastVersion == toastVersion)
        {
            sessionToast = null;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task RequestDeleteSession(Guid sessionId)
    {
        var session = SessionStorage.GetSessions().FirstOrDefault(item => item.Id == sessionId);
        if (session is null)
            return;
        if (pendingDeleteWholeSessionId != sessionId)
        {
            pendingDeleteWholeSessionId = sessionId;
            return;
        }

        var wasCurrent = currentSession.Id == sessionId;
        var replacementType = SessionType(session);
        var deletedTitle = SessionTitle(session);
        if (SessionStorage.DeleteSession(sessionId))
        {
            if (wasCurrent)
                BeginActivitySession(replacementType, DefaultSessionName(replacementType));
            if (selectedHistorySessionId == sessionId)
                selectedHistorySessionId = null;
            pendingDeleteWholeSessionId = null;
            var toastVersion = ++sessionToastVersion;
            sessionToast = $"{deletedTitle} was deleted. {DeleteImpact(session)}";
            await InvokeAsync(StateHasChanged);
            await Task.Delay(TimeSpan.FromSeconds(3));
            if (sessionToastVersion == toastVersion)
            {
                sessionToast = null;
                await InvokeAsync(StateHasChanged);
            }
            return;
        }
        pendingDeleteWholeSessionId = null;
    }

    private static string DefaultSessionName(string sessionType) => sessionType switch
    {
        "Range" => "Range session",
        "Bag Mapping" => "Bag mapping",
        "Wedge Matrix" => "Wedge matrix",
        "Distance Ladder" => "Distance ladder",
        "Combine" => "Golf combine",
        "Target Practice" => "Target practice",
        "Approach Practice" => "Approach practice",
        _ => sessionType
    };

    private void StartNewSession()
    {
        if (practiceGameState.IsActive)
            EndPracticeGame();
        if (wedgeGuide.IsActive) wedgeGuide.Stop();
        if (bagGuide.IsActive) bagGuide.Stop();
        ActivitySessions.StartNewForView(activeView, practiceGameState.Game);
        ResetSessionUiState();
    }

    private void BeginActivitySession(string sessionType, string sessionName)
    {
        ActivitySessions.Begin(sessionType, sessionName);
        ResetSessionUiState();
    }

    private void ResetSessionUiState()
    {
        selectedHistorySessionId = null;
        pendingDeleteWholeSessionId = null;
        pendingDeleteSessionId = null;
        pendingDeleteShotAt = null;
    }

    private void EnsureSessionForCurrentActivity()
    {
        if (activeView is "Analytics" or "Golf Bag" or "Sessions" or "Golfers")
            return;
        if (ActivitySessions.EnsureForView(activeView, practiceGameState.Game))
            ResetSessionUiState();
    }

    private void RenameSession(PracticeSession session)
    {
        session.Name = string.IsNullOrWhiteSpace(session.Name)
            ? "Untitled session"
            : session.Name.Trim();
        SessionStorage.SaveSession(session);
    }

    private bool IsDeletePending(Guid sessionId, DateTimeOffset hitAt) =>
        pendingDeleteSessionId == sessionId && pendingDeleteShotAt == hitAt;

    private void ToggleAnalyticsExclusion(Guid sessionId, DateTimeOffset hitAt)
    {
        SessionStorage.ToggleAnalyticsExclusion(sessionId, hitAt);
        pendingDeleteSessionId = null;
        pendingDeleteShotAt = null;
    }

    private async Task RequestDeleteShot(Guid sessionId, DateTimeOffset hitAt)
    {
        if (!IsDeletePending(sessionId, hitAt))
        {
            pendingDeleteSessionId = sessionId;
            pendingDeleteShotAt = hitAt;
            return;
        }

        if (SessionStorage.DeleteShot(sessionId, hitAt) && sessionId == currentSession.Id)
        {
            ActivitySessions.RemoveLiveShot(hitAt);
            await AdvanceBagGuideAsync();
        }

        pendingDeleteSessionId = null;
        pendingDeleteShotAt = null;
    }

    private static string SessionTitle(PracticeSession session) =>
        string.IsNullOrWhiteSpace(session.Name) ? "Untitled session" : session.Name;
    private static string SessionType(PracticeSession session) =>
        string.IsNullOrWhiteSpace(session.SessionType) ? "Range" : session.SessionType;
    private static string DeleteImpact(PracticeSession session) => SessionType(session) switch
    {
        ShotAnalytics.BagMappingSessionType => "Its shots were removed from Bag Mapping and club recommendations.",
        ShotAnalytics.WedgeMatrixSessionType => "Its shots were removed from Wedge Matrix and wedge-swing recommendations.",
        _ => "Its shots were removed from performance analytics and any practice results in that session."
    };
    private static string SessionDate(PracticeSession session) =>
        session.StartedAt.LocalDateTime.ToString("MMM d, yyyy · h:mm tt");

    private static string SessionClubs(PracticeSession session)
    {
        var names = session.Shots.Select(shot => shot.Club).Distinct().ToList();
        return names.Count == 0 ? "No shots yet" : string.Join(", ", names);
    }

    private static string AverageCarry(PracticeSession session)
    {
        var carries = session.Shots
            .Where(shot => !shot.ExcludedFromAnalytics && shot.CarryYards is not null)
            .Select(shot => shot.CarryYards!.Value)
            .ToList();
        return carries.Count == 0 ? "—" : $"{carries.Average():0.0} yd";
    }
}
