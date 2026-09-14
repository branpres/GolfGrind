using System.Text.Json;

namespace GolfGrind.App.Services;

public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private readonly object _gate = new();
    private readonly GolfGrindSettings _settings;

    public AppSettingsService()
    {
        _settings = AtomicJsonFileStore.TryRead<GolfGrindSettings>(GolfGrindDataPaths.SettingsFile, JsonOptions, out var stored) && stored is not null
            ? stored
            : new GolfGrindSettings();
    }

    public Guid? ActiveProfileId
    {
        get { lock (_gate) return _settings.ActiveProfileId; }
    }

    public bool KeepScreenAwake
    {
        get { lock (_gate) return _settings.KeepScreenAwake; }
    }

    public string? SquareDeviceId
    {
        get { lock (_gate) return _settings.SquareDeviceId; }
    }

    public string SquareDeviceName
    {
        get { lock (_gate) return string.IsNullOrWhiteSpace(_settings.SquareDeviceName) ? "Square Golf" : _settings.SquareDeviceName; }
    }

    public void SetActiveProfile(Guid profileId) => Update(settings => settings.ActiveProfileId = profileId);
    public void SetKeepScreenAwake(bool enabled) => Update(settings => settings.KeepScreenAwake = enabled);

    public void RememberSquareDevice(string deviceId, string deviceName) => Update(settings =>
    {
        settings.SquareDeviceId = deviceId;
        settings.SquareDeviceName = deviceName;
    });

    public void SetSquareLastConnected(DateTimeOffset connectedAt) =>
        Update(settings => settings.SquareLastConnected = connectedAt);

    private void Update(Action<GolfGrindSettings> update)
    {
        lock (_gate)
        {
            update(_settings);
            AtomicJsonFileStore.Write(GolfGrindDataPaths.SettingsFile, _settings, JsonOptions);
        }
    }
}

public sealed class GolfGrindSettings
{
    public Guid? ActiveProfileId { get; set; }
    public bool KeepScreenAwake { get; set; } = true;
    public string? SquareDeviceId { get; set; }
    public string? SquareDeviceName { get; set; }
    public DateTimeOffset? SquareLastConnected { get; set; }
}
