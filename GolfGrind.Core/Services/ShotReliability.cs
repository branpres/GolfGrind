using GolfGrind.Core.Models;

namespace GolfGrind.Core.Services;

public static class ShotValidator
{
    public static ShotValidationResult Validate(ShotData shot)
    {
        if (!double.IsFinite(shot.BallSpeedMph) || !double.IsFinite(shot.LaunchAngleDeg) ||
            !double.IsFinite(shot.LaunchDirectionDeg) || !double.IsFinite(shot.BackSpinRpm) ||
            !double.IsFinite(shot.SideSpinRpm))
            return ShotValidationResult.Reject(ShotRejectionReason.MissingRequiredMeasurement, "A required measurement was missing or non-finite.");
        if (shot.BallSpeedMph is < 1 or > 250)
            return ShotValidationResult.Reject(ShotRejectionReason.InvalidBallSpeed, $"Ball speed {shot.BallSpeedMph:0.0} mph is outside the accepted range.");
        if (shot.LaunchAngleDeg is < -20 or > 80)
            return ShotValidationResult.Reject(ShotRejectionReason.InvalidLaunchAngle, $"Launch angle {shot.LaunchAngleDeg:0.0}° is outside the accepted range.");
        if (shot.LaunchDirectionDeg is < -60 or > 60)
            return ShotValidationResult.Reject(ShotRejectionReason.InvalidLaunchDirection, $"Launch direction {shot.LaunchDirectionDeg:0.0}° is outside the accepted range.");
        if (Math.Abs(shot.BackSpinRpm) > 20000 || Math.Abs(shot.SideSpinRpm) > 15000)
            return ShotValidationResult.Reject(ShotRejectionReason.InvalidSpin, "Spin is outside the accepted range.");
        if (shot.CarryYards is not { } carry || !double.IsFinite(carry) || carry is < 0 or > 450 ||
            shot.OfflineYards is not { } offline || !double.IsFinite(offline) || Math.Abs(offline) > 250)
            return ShotValidationResult.Reject(ShotRejectionReason.InvalidFlightResult, "The calculated flight result is invalid.");
        return ShotValidationResult.Accept();
    }
}

public sealed class ShotDuplicateDetector
{
    private readonly TimeSpan _window;
    private string? _lastFingerprint;
    private DateTimeOffset _lastAcceptedAt;

    public ShotDuplicateDetector(TimeSpan? window = null) => _window = window ?? TimeSpan.FromSeconds(3);

    public bool IsDuplicate(ShotData shot, DateTimeOffset? observedAt = null)
    {
        var now = observedAt ?? DateTimeOffset.UtcNow;
        var fingerprint = string.Join('|',
            Math.Round(shot.BallSpeedMph, 1),
            Math.Round(shot.LaunchAngleDeg, 1),
            Math.Round(shot.LaunchDirectionDeg, 1),
            Math.Round(shot.BackSpinRpm, 0),
            Math.Round(shot.SideSpinRpm, 0));
        if (fingerprint == _lastFingerprint && now - _lastAcceptedAt <= _window)
            return true;
        _lastFingerprint = fingerprint;
        _lastAcceptedAt = now;
        return false;
    }

    public void Reset()
    {
        _lastFingerprint = null;
        _lastAcceptedAt = default;
    }
}

public static class CalibrationMath
{
    public static double AdjustCarry(
        double rawCarryYards,
        EnvironmentalCalibration environment,
        PersonalShotAdjustment personal)
    {
        var altitudeScale = 1 + Math.Clamp(environment.AltitudeFeet, -1000, 12000) / 1000d * 0.01;
        var temperatureScale = 1 + (Math.Clamp(environment.TemperatureFahrenheit, 20, 120) - 70) * 0.001;
        var humidityScale = 1 + (Math.Clamp(environment.RelativeHumidityPercent, 0, 100) - 50) * 0.0001;
        return rawCarryYards * altitudeScale * temperatureScale * humidityScale *
               Math.Clamp(environment.ReferenceCarryScale, 0.8, 1.2) *
               Math.Clamp(personal.CarryScale, 0.8, 1.2);
    }

    public static ShotData AttachMetadata(
        ShotData shot,
        EnvironmentalCalibration environment,
        PersonalShotAdjustment personal,
        string flightModel = "BallFlightCalculator/3",
        string calculationProfile = "Standard") => shot with
        {
            Calculation = new(flightModel, calculationProfile, environment, personal, DateTimeOffset.UtcNow)
        };
}
