using GolfGrind.Core.Models;

namespace GolfGrind.App.Services;

public sealed record ActivityDescriptor(string SessionType, string SessionName);

public sealed class ActivitySessionCoordinator(SessionStorageService storage)
{
    private readonly List<ShotData> _liveShots = [];

    public PracticeSession Current { get; private set; } = new();
    public IReadOnlyList<ShotData> LiveShots => _liveShots;
    public ShotData? LastShot { get; private set; }

    public void Initialize() => Begin("Range", "Range session");

    public void Begin(string sessionType, string sessionName)
    {
        Current = storage.StartNewSession(sessionName, sessionType);
        _liveShots.Clear();
        LastShot = null;
    }

    public bool EnsureForView(string view, PracticeGameKind practiceGame)
    {
        if (view is "Analytics" or "Golf Bag" or "Sessions" or "Golfers")
            return false;
        var activity = Describe(view, practiceGame);
        if (string.Equals(Current.SessionType, activity.SessionType, StringComparison.OrdinalIgnoreCase))
            return false;
        Begin(activity.SessionType, activity.SessionName);
        return true;
    }

    public void StartNewForView(string view, PracticeGameKind practiceGame)
    {
        var activity = Describe(view, practiceGame);
        Begin(activity.SessionType, activity.SessionName);
    }

    public void ConfigurePractice(PracticeGameState state)
    {
        Current.PracticeGame = state.Game;
        Current.GameSettings = state.Settings;
    }

    public void Record(ShotData shot, Guid? clubId)
    {
        LastShot = shot;
        _liveShots.Add(shot);
        storage.RecordShot(shot, clubId);
    }

    public void CompletePractice(PracticeGameSummary summary)
    {
        if (Current.Shots.Count == 0)
            storage.DiscardIfEmpty(Current.Id);
        else
            storage.SaveGameSummary(Current.Id, summary);
    }

    public void SaveCompletedPractice(PracticeGameSummary summary) => storage.SaveGameSummary(Current.Id, summary);

    public void RemoveLiveShot(DateTimeOffset hitAt)
    {
        _liveShots.RemoveAll(shot => shot.HitAt == hitAt);
        if (LastShot?.HitAt == hitAt)
            LastShot = _liveShots.LastOrDefault();
    }

    public static ActivityDescriptor Describe(string view, PracticeGameKind practiceGame) => view switch
    {
        "Bag Mapping" => new("Bag Mapping", "Bag mapping"),
        "Wedge Matrix" => new("Wedge Matrix", "Wedge matrix"),
        "Practice" => new(practiceGame.DisplayName(), practiceGame.DisplayName()),
        _ => new("Range", "Range session")
    };
}
