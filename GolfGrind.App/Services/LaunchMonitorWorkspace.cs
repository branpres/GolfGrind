using GolfGrind.Core.Abstractions;
using GolfGrind.Core.Models;
using GolfGrind.Core.Services;

namespace GolfGrind.App.Services;

public sealed class LaunchMonitorWorkspace(
    ILaunchMonitor squareMonitor,
    MockLaunchMonitor mockMonitor,
    LaunchMonitorCoordinator coordinator,
    IDisplayWakeService displayWakeService,
    AppSettingsService settings)
{
    private readonly EnvironmentalCalibration _environment = new();
    private readonly PersonalShotAdjustment _personalAdjustment = new();

    public ILaunchMonitor SquareMonitor => squareMonitor;
    public MockLaunchMonitor MockMonitor => mockMonitor;
    public string MonitorMode { get; private set; } = "square";
    public bool KeepScreenAwake { get; private set; } = true;
    public bool AudibleReadyNotification { get; private set; } = true;
    public LaunchMonitorStatus Status { get; private set; } = new(false, false, null, "Disconnected");
    public int AcceptedShotCount => coordinator.AcceptedShots;
    public int RejectedShotCount => coordinator.RejectedShots;
    public int DuplicateShotCount => coordinator.DuplicateShots;
    public string? ShotDiagnosticMessage => coordinator.LastDiagnostic;
    public ILaunchMonitor ActiveMonitor => MonitorMode == "mock" ? mockMonitor : squareMonitor;

    public void Initialize()
    {
        KeepScreenAwake = settings.KeepScreenAwake;
        AudibleReadyNotification = settings.AudibleReadyNotification;
        UpdateDisplayWakeLock();
    }

    public async Task SetMonitorModeAsync(string mode)
    {
        MonitorMode = mode;
        displayWakeService.SetActive(false);
        Status = new(false, false, null, mode == "mock" ? "Demo mode selected" : "Square Bluetooth selected");
        await Task.CompletedTask;
    }

    public void SetKeepScreenAwake(bool enabled)
    {
        KeepScreenAwake = enabled;
        settings.SetKeepScreenAwake(enabled);
        UpdateDisplayWakeLock();
    }

    public void SetAudibleReadyNotification(bool enabled)
    {
        AudibleReadyNotification = enabled;
        settings.SetAudibleReadyNotification(enabled);
    }

    public async Task ToggleConnectionAsync(string selectedClub)
    {
        if (ActiveMonitor.IsConnected)
        {
            await ActiveMonitor.DisconnectAsync();
            return;
        }
        await ActiveMonitor.ConnectAsync();
        await ActiveMonitor.SelectClubAsync(selectedClub);
    }

    public Task SelectClubAsync(string club) => ActiveMonitor.SelectClubAsync(club);
    public void SimulateShot() => mockMonitor.SimulateShot();

    public ShotProcessingResult Process(object? sender, ShotData shot, string swingType)
    {
        if (!ReferenceEquals(sender, ActiveMonitor))
            return new(false, null, ShotValidationResult.Reject(ShotRejectionReason.None, "Shot ignored because it came from the inactive monitor."));
        return coordinator.Process(shot with { SwingType = swingType }, _environment, _personalAdjustment);
    }

    public bool UpdateStatus(object? sender, LaunchMonitorStatus status)
    {
        if (!ReferenceEquals(sender, ActiveMonitor))
            return false;
        Status = status;
        UpdateDisplayWakeLock();
        return true;
    }

    public void ResetProcessing() => coordinator.Reset();
    public void DeactivateDisplay() => displayWakeService.SetActive(false);
    private void UpdateDisplayWakeLock() => displayWakeService.SetActive(KeepScreenAwake && Status.IsConnected);
}
