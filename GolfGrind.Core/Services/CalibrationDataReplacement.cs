using GolfGrind.Core.Models;

namespace GolfGrind.Core.Services;

public sealed record CalibrationReplacementResult(
    int RemovedShots,
    IReadOnlyList<PracticeSession> ChangedSessions);

public static class CalibrationDataReplacement
{
    public static CalibrationReplacementResult RemoveForClubs(
        IEnumerable<PracticeSession> sessions,
        string sessionType,
        IEnumerable<GolfClub> clubs)
    {
        var selectedClubs = clubs.ToList();
        if (selectedClubs.Count == 0)
            return new(0, []);

        var removed = 0;
        var changed = new List<PracticeSession>();
        foreach (var session in sessions.Where(session =>
                     string.Equals(session.SessionType, sessionType, StringComparison.OrdinalIgnoreCase)))
        {
            var removedFromSession = session.Shots.RemoveAll(shot =>
                selectedClubs.Any(club => ShotAnalytics.MatchesClub(shot, club)));
            if (removedFromSession == 0)
                continue;
            removed += removedFromSession;
            changed.Add(session);
        }

        return new(removed, changed);
    }
}
