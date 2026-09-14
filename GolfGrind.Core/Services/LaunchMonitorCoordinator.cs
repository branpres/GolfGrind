using GolfGrind.Core.Models;

namespace GolfGrind.Core.Services;

public sealed class LaunchMonitorCoordinator
{
    private readonly ShotDuplicateDetector _duplicates = new();

    public int AcceptedShots { get; private set; }
    public int RejectedShots { get; private set; }
    public int DuplicateShots { get; private set; }
    public string? LastDiagnostic { get; private set; }

    public ShotProcessingResult Process(
        ShotData shot,
        EnvironmentalCalibration environment,
        PersonalShotAdjustment personal)
    {
        var validation = ShotValidator.Validate(shot);
        if (!validation.Accepted)
        {
            RejectedShots++;
            LastDiagnostic = $"Rejected shot: {validation.Message}";
            return new(false, null, validation);
        }

        if (_duplicates.IsDuplicate(shot))
        {
            DuplicateShots++;
            var duplicate = ShotValidationResult.Reject(ShotRejectionReason.Duplicate, "Ignored duplicate shot packet.");
            LastDiagnostic = duplicate.Message;
            return new(false, null, duplicate);
        }

        AcceptedShots++;
        LastDiagnostic = $"Accepted shot {AcceptedShots}.";
        return new(true, CalibrationMath.AttachMetadata(shot, environment, personal), validation);
    }

    public void Reset()
    {
        _duplicates.Reset();
        AcceptedShots = 0;
        RejectedShots = 0;
        DuplicateShots = 0;
        LastDiagnostic = null;
    }
}

public sealed record ShotProcessingResult(
    bool Accepted,
    ShotData? Shot,
    ShotValidationResult Validation);
