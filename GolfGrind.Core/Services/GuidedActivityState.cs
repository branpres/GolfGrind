using GolfGrind.Core.Models;

namespace GolfGrind.Core.Services;

public enum BagMappingGuideStatus { Ready, NeedsClubSelection, Active, Stopped, Completed, LeftView }
public enum WedgeMatrixGuideStatus { Ready, NeedsWedge, Active, Stopped, Completed, LeftView }

public sealed class BagMappingGuideState
{
    public HashSet<Guid> SelectedClubIds { get; } = [];
    public IReadOnlyList<Guid> Sequence { get; private set; } = [];
    public bool IsActive { get; private set; }
    public int ShotsPerClub { get; set; } = 5;
    public int ClubIndex { get; private set; }
    public int ShotInClub { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public BagMappingGuideStatus Status { get; private set; }
    public Guid? CurrentClubId => ClubIndex < Sequence.Count ? Sequence[ClubIndex] : null;

    public void ResetSelection(IEnumerable<GolfClub> clubs)
    {
        SelectedClubIds.Clear();
        foreach (var club in clubs.Where(club => club.Kind != GolfClubKind.Putter))
            SelectedClubIds.Add(club.Id);
        ResetRun();
    }

    public void Toggle(Guid clubId, bool selected)
    {
        if (selected) SelectedClubIds.Add(clubId);
        else SelectedClubIds.Remove(clubId);
    }

    public void SelectAll(IEnumerable<GolfClub> clubs) => ResetSelection(clubs);
    public void DeselectAll()
    {
        SelectedClubIds.Clear();
        Status = BagMappingGuideStatus.Ready;
    }

    public bool Start(IEnumerable<GolfClub> clubs, DateTimeOffset startedAt)
    {
        Sequence = clubs.Where(club => club.Kind != GolfClubKind.Putter && SelectedClubIds.Contains(club.Id)).Select(club => club.Id).ToList();
        if (Sequence.Count == 0)
        {
            IsActive = false;
            Status = BagMappingGuideStatus.NeedsClubSelection;
            return false;
        }
        ShotsPerClub = Math.Clamp(ShotsPerClub, 1, 30);
        ClubIndex = 0;
        ShotInClub = 0;
        StartedAt = startedAt;
        Status = BagMappingGuideStatus.Active;
        IsActive = true;
        return true;
    }

    public bool Advance(IReadOnlyList<StoredShot> shots)
    {
        if (!IsActive)
            return false;
        var progress = BagMappingProgression.Calculate(Sequence, shots, StartedAt, ShotsPerClub);
        ClubIndex = progress.ClubIndex;
        ShotInClub = progress.ShotsForClub;
        if (!progress.Complete)
            return false;
        IsActive = false;
        Status = BagMappingGuideStatus.Completed;
        return true;
    }

    public void Stop(BagMappingGuideStatus status = BagMappingGuideStatus.Stopped)
    {
        IsActive = false;
        Status = status;
    }

    public void ClearMessage() => Status = BagMappingGuideStatus.Ready;
    private void ResetRun()
    {
        Sequence = [];
        IsActive = false;
        ClubIndex = 0;
        ShotInClub = 0;
        Status = BagMappingGuideStatus.Ready;
    }
}

public sealed record WedgeGuideSelection(Guid ClubId, string SwingType);

public sealed class WedgeMatrixGuideState
{
    public static readonly IReadOnlyList<string> SwingTypes = ["Half", "ThreeQuarter", "Full"];
    public HashSet<Guid> SelectedClubIds { get; } = [];
    public IReadOnlyList<Guid> Sequence { get; private set; } = [];
    public bool IsActive { get; private set; }
    public int ShotsPerCell { get; set; } = 5;
    public int ClubIndex { get; private set; }
    public int SwingIndex { get; private set; }
    public int ShotInCell { get; private set; }
    public WedgeMatrixGuideStatus Status { get; private set; }

    public void ResetSelection(IEnumerable<GolfClub> wedges)
    {
        SelectedClubIds.Clear();
        foreach (var wedge in wedges.Where(club => club.Kind == GolfClubKind.Wedge))
            SelectedClubIds.Add(wedge.Id);
        ResetRun();
    }

    public void Toggle(Guid clubId, bool selected)
    {
        if (selected) SelectedClubIds.Add(clubId);
        else SelectedClubIds.Remove(clubId);
    }

    public void SelectAll(IEnumerable<GolfClub> wedges) => ResetSelection(wedges);

    public void DeselectAll()
    {
        SelectedClubIds.Clear();
        ResetRun();
    }

    public bool Start(IReadOnlyList<GolfClub> wedges)
    {
        Sequence = wedges
            .Where(wedge => SelectedClubIds.Contains(wedge.Id))
            .Select(wedge => wedge.Id)
            .ToList();
        if (Sequence.Count == 0)
        {
            IsActive = false;
            Status = WedgeMatrixGuideStatus.NeedsWedge;
            return false;
        }
        ShotsPerCell = Math.Clamp(ShotsPerCell, 1, 20);
        ClubIndex = 0;
        SwingIndex = 0;
        ShotInCell = 0;
        Status = WedgeMatrixGuideStatus.Active;
        IsActive = true;
        return true;
    }

    public bool Advance()
    {
        if (!IsActive)
            return false;
        var progress = WedgeMatrixProgression.Advance(ClubIndex, SwingIndex, ShotInCell, Sequence.Count, SwingTypes.Count, ShotsPerCell);
        ClubIndex = progress.ClubIndex;
        SwingIndex = progress.SwingIndex;
        ShotInCell = progress.ShotInCell;
        if (!progress.Complete)
            return false;
        IsActive = false;
        Status = WedgeMatrixGuideStatus.Completed;
        return true;
    }

    public WedgeGuideSelection? Current(IReadOnlyList<GolfClub> wedges) =>
        ClubIndex < Sequence.Count && SwingIndex < SwingTypes.Count &&
        wedges.FirstOrDefault(wedge => wedge.Id == Sequence[ClubIndex]) is { } wedge
            ? new(wedge.Id, SwingTypes[SwingIndex])
            : null;

    public void Stop(WedgeMatrixGuideStatus status = WedgeMatrixGuideStatus.Stopped)
    {
        IsActive = false;
        Status = status;
    }

    public void Reset()
    {
        SelectedClubIds.Clear();
        ResetRun();
    }

    private void ResetRun()
    {
        Sequence = [];
        IsActive = false;
        ClubIndex = 0;
        SwingIndex = 0;
        ShotInCell = 0;
        Status = WedgeMatrixGuideStatus.Ready;
    }
}
