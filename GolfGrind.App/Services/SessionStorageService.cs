using System.Text.Json;
using GolfGrind.Core.Models;

namespace GolfGrind.App.Services;

public sealed class SessionStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private string _sessionsDirectory = "";
    private readonly List<PracticeSession> _sessions = [];
    private PracticeSession? _current;

    public SessionStorageService()
    {
    }

    public void SwitchProfile(Guid profileId)
    {
        _sessions.Clear();
        _current = null;
        _sessionsDirectory = DirectoryFor(profileId);
        Directory.CreateDirectory(_sessionsDirectory);

        LoadExistingSessions();
    }

    public PracticeSession Current => _current ?? StartNewSession();

    public IReadOnlyList<PracticeSession> GetSessions() =>
        _sessions
            .Where(session => session.Shots.Count > 0)
            .OrderByDescending(session => session.StartedAt)
            .ToList();

    public PracticeSession StartNewSession(string? name = null, string? sessionType = null)
    {
        var now = DateTimeOffset.Now;
        foreach (var openSession in _sessions.Where(session => session.EndedAt is null && session.Shots.Count > 0))
        {
            openSession.EndedAt = now;
            Save(openSession);
        }

        foreach (var emptySession in _sessions.Where(session => session.Shots.Count == 0).ToList())
            DeleteSession(emptySession.Id);

        _current = new PracticeSession
        {
            StartedAt = now,
            Name = string.IsNullOrWhiteSpace(name) ? "Range session" : name.Trim(),
            SessionType = string.IsNullOrWhiteSpace(sessionType) ? "Range" : sessionType.Trim()
        };
        _sessions.Add(_current);
        return _current;
    }

    public void SaveSession(PracticeSession session)
    {
        if (session.Shots.Count > 0)
            Save(session);
    }

    public bool DiscardIfEmpty(Guid sessionId)
    {
        var session = _sessions.FirstOrDefault(item => item.Id == sessionId);
        return session is not null && session.Shots.Count == 0 && DeleteSession(sessionId);
    }

    public PracticeSession ReplaceAll(IEnumerable<PracticeSession> sessions)
    {
        foreach (var path in Directory.EnumerateFiles(_sessionsDirectory, "*.json*"))
            File.Delete(path);

        _sessions.Clear();
        foreach (var session in sessions)
        {
            session.Shots ??= [];
            if (session.Shots.Count == 0)
                continue;
            _sessions.Add(session);
            Save(session);
        }

        _current = null;
        return StartNewSession("Restored session", "Range");
    }

    public void ReplaceProfile(Guid profileId, IEnumerable<PracticeSession> sessions)
    {
        var directory = DirectoryFor(profileId);
        Directory.CreateDirectory(directory);
        foreach (var path in Directory.EnumerateFiles(directory, "*.json*"))
            File.Delete(path);
        foreach (var session in sessions)
        {
            session.Shots ??= [];
            if (session.Shots.Count == 0)
                continue;
            SaveToDirectory(directory, session);
        }
    }

    public IReadOnlyList<PracticeSession> ReadProfile(Guid profileId)
    {
        var sessions = new List<PracticeSession>();
        var directory = DirectoryFor(profileId);
        if (!Directory.Exists(directory))
            return sessions;
        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            if (AtomicJsonFileStore.TryRead<PracticeSession>(path, JsonOptions, out var session) && session is not null)
            {
                session.Shots ??= [];
                sessions.Add(session);
            }
        }
        return sessions.OrderByDescending(session => session.StartedAt).ToList();
    }

    public void DeleteProfile(Guid profileId)
    {
        var directory = DirectoryFor(profileId);
        if (Directory.Exists(directory))
            Directory.Delete(directory, true);
    }

    public void RecordShot(ShotData shot, Guid? clubId)
    {
        var session = Current;
        session.Shots.Add(StoredShot.FromShot(shot, clubId));
        Save(session);
    }

    public void SaveGameSummary(Guid sessionId, PracticeGameSummary summary)
    {
        var session = _sessions.FirstOrDefault(item => item.Id == sessionId);
        if (session is null)
            return;
        if (session.Shots.Count == 0)
            return;
        session.GameSummary = summary;
        session.EndedAt = summary.FinishedAt;
        Save(session);
    }

    public bool DeleteShot(Guid sessionId, DateTimeOffset hitAt)
    {
        var session = _sessions.FirstOrDefault(item => item.Id == sessionId);
        if (session is null)
            return false;

        var removed = session.Shots.RemoveAll(shot => shot.HitAt == hitAt) > 0;
        if (removed)
        {
            if (session.Shots.Count == 0)
                DeleteSession(sessionId);
            else
                Save(session);
        }
        return removed;
    }

    public bool? ToggleAnalyticsExclusion(Guid sessionId, DateTimeOffset hitAt)
    {
        var session = _sessions.FirstOrDefault(item => item.Id == sessionId);
        if (session is null)
            return null;
        var index = session.Shots.FindIndex(shot => shot.HitAt == hitAt);
        if (index < 0)
            return null;

        var excluded = !session.Shots[index].ExcludedFromAnalytics;
        session.Shots[index] = session.Shots[index] with { ExcludedFromAnalytics = excluded };
        Save(session);
        return excluded;
    }

    public bool DeleteSession(Guid sessionId)
    {
        var session = _sessions.FirstOrDefault(item => item.Id == sessionId);
        if (session is null)
            return false;

        _sessions.Remove(session);
        if (_current?.Id == sessionId)
            _current = null;
        var path = Path.Combine(_sessionsDirectory, $"{sessionId:N}.json");
        if (File.Exists(path))
            File.Delete(path);
        if (File.Exists(path + ".good"))
            File.Delete(path + ".good");
        return true;
    }

    private void LoadExistingSessions()
    {
        foreach (var path in Directory.EnumerateFiles(_sessionsDirectory, "*.json"))
        {
            if (AtomicJsonFileStore.TryRead<PracticeSession>(path, JsonOptions, out var session) && session is not null)
            {
                session.Shots ??= [];
                if (session.Shots.Count == 0)
                {
                    File.Delete(path);
                    if (File.Exists(path + ".good"))
                        File.Delete(path + ".good");
                }
                else
                {
                    _sessions.Add(session);
                }
            }
        }
    }

    private void Save(PracticeSession session)
    {
        if (string.IsNullOrWhiteSpace(_sessionsDirectory))
            throw new InvalidOperationException("Select a golfer profile before saving a session.");
        SaveToDirectory(_sessionsDirectory, session);
    }

    private static void SaveToDirectory(string directory, PracticeSession session)
    {
        var path = Path.Combine(directory, $"{session.Id:N}.json");
        AtomicJsonFileStore.Write(path, session, JsonOptions, ValidateSession);
    }

    private static void ValidateSession(PracticeSession session)
    {
        if (session.Id == Guid.Empty)
            throw new InvalidDataException("A session must have an identifier.");
        session.Shots ??= [];
    }

    private static string DirectoryFor(Guid profileId) =>
        GolfGrindDataPaths.SessionsDirectory(profileId);
}
