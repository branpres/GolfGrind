using GolfGrind.Core.Abstractions;
using GolfGrind.Core.Models;
using GolfGrind.Core.Protocol;
using GolfGrind.Core.Services;
using GolfGrind.App.Services;
using Microsoft.Extensions.Logging;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;

namespace GolfGrind.App.Platforms.Windows;

public sealed class SquareLaunchMonitor(ILogger<SquareLaunchMonitor> logger, AppSettingsService settings) : ILaunchMonitor
{
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly SemaphoreSlim _writes = new(1, 1);
    private readonly SemaphoreSlim _diagnosticWrites = new(1, 1);
    private readonly List<GattDeviceService> _services = [];
    private readonly ConnectionStateMachine _stateMachine = new();
    private static readonly TimeSpan[] ReconnectDelays =
        [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(15)];

    private BluetoothLEDevice? _device;
    private GattCharacteristic? _command;
    private GattCharacteristic? _notifications;
    private GattCharacteristic? _battery;
    private CancellationTokenSource? _connectionCts;
    private CancellationTokenSource? _reconnectCts;
    private bool _connectionRequested;
    private int _reconnectLoopActive;
    private int _reconnectAttempts;
    private int _receivedShots;
    private int _duplicatePackets;
    private DateTimeOffset? _lastPacketAt;
    private DateTimeOffset? _lastHeartbeatAt;
    private DateTimeOffset? _connectedAt;
    private string? _lastError;
    private byte _sequence;
    private int? _batteryPercent;
    private bool _ballReady;
    private string _club = "7 Iron";
    private string? _lastBallPacket;
    private DateTimeOffset _lastBallPacketAt;

    public event EventHandler<ShotData>? ShotReceived;
    public event EventHandler<LaunchMonitorStatus>? StatusChanged;

    public bool IsConnected => _device?.ConnectionStatus == BluetoothConnectionStatus.Connected && _command is not null;
    public string? DiagnosticsPath { get; private set; }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _connectionRequested = true;
        _reconnectCts?.Cancel();
        await ConnectAttemptAsync(cancellationToken);
    }

    private async Task ConnectAttemptAsync(CancellationToken cancellationToken)
    {
        await _lifecycle.WaitAsync(cancellationToken);
        try
        {
            if (IsConnected)
                return;

            InitializeDiagnosticsLog();
            SetState(LaunchMonitorConnectionState.Discovering);
            PublishStatus("Looking for a paired Square monitor…");
            _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var device = await FindDeviceAsync(_connectionCts.Token);
            if (device is null)
            {
                await CleanupAsync();
                SetState(LaunchMonitorConnectionState.Faulted);
                PublishStatus("Square not found. Pair it in Windows Bluetooth settings, then try again.");
                return;
            }

            _device = device;
            settings.RememberSquareDevice(device.DeviceId, DisplayName(device));
            _device.ConnectionStatusChanged += OnConnectionStatusChanged;
            SetState(LaunchMonitorConnectionState.Connecting);
            PublishStatus($"Connecting to {DisplayName(device)}…");

            await DiscoverCharacteristicsAsync(device, _connectionCts.Token);
            SetState(LaunchMonitorConnectionState.Initializing);
            await SubscribeAsync(_connectionCts.Token);
            await ReadBatteryAsync(_connectionCts.Token);

            SetState(LaunchMonitorConnectionState.Connected);
            _reconnectAttempts = 0;
            _lastError = null;
            _connectedAt = DateTimeOffset.UtcNow;
            settings.SetSquareLastConnected(DateTimeOffset.UtcNow);
            PublishStatus($"Connected to {DisplayName(device)}");
            await WriteAsync(SquareProtocol.Heartbeat(NextSequence()), _connectionCts.Token);
            _lastHeartbeatAt = DateTimeOffset.UtcNow;
            await WriteAsync(SquareProtocol.SelectClub(NextSequence(), _club), _connectionCts.Token);
            await WriteAsync(SquareProtocol.EnableBallDetection(NextSequence()), _connectionCts.Token);

            _ = HeartbeatLoopAsync(_connectionCts.Token);
            _ = WatchdogLoopAsync(_connectionCts.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await CleanupAsync();
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not connect to the Square launch monitor");
            _lastError = ex.Message;
            await CleanupAsync();
            SetState(LaunchMonitorConnectionState.Faulted);
            PublishStatus($"Connection failed: {ex.Message}");
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _connectionRequested = false;
        _reconnectCts?.Cancel();
        _connectionCts?.Cancel();
        await _lifecycle.WaitAsync(cancellationToken);
        try
        {
            await CleanupAsync();
            SetState(LaunchMonitorConnectionState.Disconnected);
            PublishStatus("Disconnected");
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task SelectClubAsync(string club, CancellationToken cancellationToken = default)
    {
        _club = club;
        if (IsConnected)
            await WriteAsync(SquareProtocol.SelectClub(NextSequence(), club), cancellationToken);
    }

    private async Task<BluetoothLEDevice?> FindDeviceAsync(CancellationToken cancellationToken)
    {
        var rememberedId = settings.SquareDeviceId;
        if (!string.IsNullOrWhiteSpace(rememberedId))
        {
            try
            {
                var remembered = await BluetoothLEDevice.FromIdAsync(rememberedId).AsTask(cancellationToken);
                if (remembered is not null)
                {
                    PublishStatus($"Reconnecting to remembered {settings.SquareDeviceName}…");
                    return remembered;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(ex, "Remembered Square device was unavailable; falling back to discovery");
            }
        }

        var paired = await DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true));
        var match = paired.FirstOrDefault(info => IsSquareName(info.Name));
        if (match is not null)
            return await BluetoothLEDevice.FromIdAsync(match.Id);

        PublishStatus("Scanning for Square Bluetooth advertisements…");
        var completion = new TaskCompletionSource<ulong>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        var watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };

        void Received(BluetoothLEAdvertisementWatcher _, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            if (IsSquareName(args.Advertisement.LocalName))
                completion.TrySetResult(args.BluetoothAddress);
        }

        watcher.Received += Received;
        watcher.Start();
        try
        {
            var timeout = Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
            if (await Task.WhenAny(completion.Task, timeout) != completion.Task)
                return null;

            return await BluetoothLEDevice.FromBluetoothAddressAsync(await completion.Task);
        }
        finally
        {
            watcher.Stop();
            watcher.Received -= Received;
        }
    }

    private async Task DiscoverCharacteristicsAsync(BluetoothLEDevice device, CancellationToken cancellationToken)
    {
        var serviceResult = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached).AsTask(cancellationToken);
        if (serviceResult.Status != GattCommunicationStatus.Success)
            throw new InvalidOperationException($"Bluetooth service discovery returned {serviceResult.Status}.");

        _services.AddRange(serviceResult.Services);
        foreach (var service in _services)
        {
            var characteristicResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached).AsTask(cancellationToken);
            if (characteristicResult.Status != GattCommunicationStatus.Success)
                continue;

            foreach (var characteristic in characteristicResult.Characteristics)
            {
                if (characteristic.Uuid == SquareProtocol.CommandCharacteristic) _command = characteristic;
                else if (characteristic.Uuid == SquareProtocol.NotificationCharacteristic) _notifications = characteristic;
                else if (characteristic.Uuid == SquareProtocol.BatteryCharacteristic) _battery = characteristic;
            }
        }

        if (_command is null || _notifications is null)
            throw new InvalidOperationException("The Square command/notification characteristics were not found. Close other Square software and confirm the monitor is paired.");
    }

    private async Task SubscribeAsync(CancellationToken cancellationToken)
    {
        _notifications!.ValueChanged += OnNotificationReceived;
        var status = await _notifications.WriteClientCharacteristicConfigurationDescriptorAsync(
            GattClientCharacteristicConfigurationDescriptorValue.Notify).AsTask(cancellationToken);

        if (status != GattCommunicationStatus.Success)
            throw new InvalidOperationException($"Subscribing to Square notifications returned {status}.");

        if (_battery is not null && _battery.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
        {
            _battery.ValueChanged += OnBatteryChanged;
            await _battery.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify).AsTask(cancellationToken);
        }
    }

    private async Task ReadBatteryAsync(CancellationToken cancellationToken)
    {
        if (_battery is null)
            return;

        var result = await _battery.ReadValueAsync(BluetoothCacheMode.Uncached).AsTask(cancellationToken);
        if (result.Status == GattCommunicationStatus.Success)
        {
            CryptographicBuffer.CopyToByteArray(result.Value, out var bytes);
            if (bytes.Length > 0)
                _batteryPercent = bytes[0];
        }
    }

    private void OnNotificationReceived(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        try
        {
            if (args.CharacteristicValue is null)
                return;

            CryptographicBuffer.CopyToByteArray(args.CharacteristicValue, out var packet);
            if (packet is { Length: > 0 })
                _ = HandleNotificationAsync(packet);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Ignored an invalid Square notification during disconnect");
        }
    }

    private async Task HandleNotificationAsync(byte[]? packet)
    {
        if (packet is not { Length: > 0 })
            return;

        try
        {
            _lastPacketAt = DateTimeOffset.UtcNow;
            await LogPacketAsync("RX", packet);

            if (SquareProtocol.TryParseBallReady(packet, out var ready))
            {
                _ballReady = ready;
                SetState(ready ? LaunchMonitorConnectionState.BallReady : LaunchMonitorConnectionState.Connected);
                PublishStatus(ready ? "Connected — ball ready" : "Connected — place ball in the hitting zone");
                return;
            }

            if (SquareProtocol.TryParseBallMetrics(packet, out var ball))
            {
                var fingerprint = Convert.ToHexString(packet);
                var now = DateTimeOffset.UtcNow;
                if (fingerprint == _lastBallPacket && now - _lastBallPacketAt < TimeSpan.FromSeconds(2))
                {
                    _duplicatePackets++;
                    PublishStatus("Ignored duplicate Square shot packet");
                    return;
                }

                _lastBallPacket = fingerprint;
                _lastBallPacketAt = now;
                _ballReady = false;
                _receivedShots++;
                SetState(LaunchMonitorConnectionState.Connected);
                PublishStatus("Shot received");

                ShotReceived?.Invoke(this, SquareProtocol.ToShot(_club, ball));
                if (_connectionCts is not null)
                    _ = ReArmAfterShotAsync(_connectionCts.Token);
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not process a Square notification: {Packet}",
                packet is null ? "<null>" : Convert.ToHexString(packet));
        }
    }

    private async Task ReArmAfterShotAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            await WriteAsync(SquareProtocol.EnableBallDetection(NextSequence()), cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not re-arm Square ball detection");
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await WriteAsync(SquareProtocol.Heartbeat(NextSequence()), cancellationToken);
                _lastHeartbeatAt = DateTimeOffset.UtcNow;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Square heartbeat stopped");
            _lastError = ex.Message;
            _ = HandleConnectionFaultAsync("Square heartbeat stopped; reconnecting…");
        }
    }

    private async Task WatchdogLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (!_connectionRequested)
                    return;
                if (_device?.ConnectionStatus != BluetoothConnectionStatus.Connected || _command is null || _notifications is null)
                {
                    await HandleConnectionFaultAsync("Square connection watchdog detected a stale connection; reconnecting…");
                    return;
                }
                if (_lastHeartbeatAt is { } heartbeat && DateTimeOffset.UtcNow - heartbeat > TimeSpan.FromSeconds(15))
                {
                    await HandleConnectionFaultAsync("Square heartbeat is stale; reconnecting…");
                    return;
                }
                var lastTraffic = _lastPacketAt ?? _connectedAt;
                if (lastTraffic is { } traffic && DateTimeOffset.UtcNow - traffic > TimeSpan.FromSeconds(30))
                {
                    await HandleConnectionFaultAsync("Square notifications are stale; reconnecting…");
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task WriteAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        if (_command is null)
            throw new InvalidOperationException("The Square command channel is not connected.");

        await _writes.WaitAsync(cancellationToken);
        try
        {
            using var writer = new DataWriter();
            writer.WriteBytes(bytes);
            var buffer = writer.DetachBuffer();
            var option = _command.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write)
                ? GattWriteOption.WriteWithResponse
                : GattWriteOption.WriteWithoutResponse;
            var result = await _command.WriteValueWithResultAsync(buffer, option).AsTask(cancellationToken);
            if (result.Status != GattCommunicationStatus.Success)
                throw new InvalidOperationException($"Square Bluetooth write returned {result.Status}.");

            await LogPacketAsync("TX", bytes);
        }
        finally
        {
            _writes.Release();
        }
    }

    private void OnBatteryChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        if (args.CharacteristicValue is null)
            return;

        CryptographicBuffer.CopyToByteArray(args.CharacteristicValue, out var bytes);
        if (bytes is { Length: > 0 })
        {
            _batteryPercent = bytes[0];
            PublishStatus(_ballReady ? "Connected — ball ready" : "Connected");
        }
    }

    private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args)
    {
        if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            _ = HandleUnexpectedDisconnectAsync(sender);
    }

    private async Task HandleUnexpectedDisconnectAsync(BluetoothLEDevice disconnectedDevice)
    {
        var lockTaken = false;
        try
        {
            await _lifecycle.WaitAsync();
            lockTaken = true;
            if (!ReferenceEquals(disconnectedDevice, _device))
                return;

            await CleanupAsync();
            if (_connectionRequested)
            {
                SetState(LaunchMonitorConnectionState.Reconnecting);
                PublishStatus("Square disconnected — reconnecting automatically…");
            }
            else
            {
                SetState(LaunchMonitorConnectionState.Disconnected);
                PublishStatus("Square disconnected");
            }
        }
        catch (ObjectDisposedException)
        {
            // The app is already shutting down.
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not finish Square disconnect cleanup");
            PublishStatus("Square disconnected");
        }
        finally
        {
            if (lockTaken)
                _lifecycle.Release();
        }

        if (_connectionRequested)
            _ = ReconnectLoopAsync();
    }

    private async Task HandleConnectionFaultAsync(string message)
    {
        var lockTaken = false;
        try
        {
            await _lifecycle.WaitAsync();
            lockTaken = true;
            if (!_connectionRequested)
                return;
            await CleanupAsync();
            SetState(LaunchMonitorConnectionState.Reconnecting);
            PublishStatus(message);
        }
        catch (ObjectDisposedException)
        {
            return;
        }
        finally
        {
            if (lockTaken)
                _lifecycle.Release();
        }
        _ = ReconnectLoopAsync();
    }

    private async Task ReconnectLoopAsync()
    {
        if (Interlocked.Exchange(ref _reconnectLoopActive, 1) == 1)
            return;

        _reconnectCts?.Dispose();
        _reconnectCts = new CancellationTokenSource();
        var token = _reconnectCts.Token;
        try
        {
            for (var index = 0; index < ReconnectDelays.Length && _connectionRequested; index++)
            {
                _reconnectAttempts = index + 1;
                SetState(LaunchMonitorConnectionState.Reconnecting);
                PublishStatus($"Reconnect attempt {_reconnectAttempts} in {ReconnectDelays[index].TotalSeconds:0} seconds…");
                await Task.Delay(ReconnectDelays[index], token);
                await ConnectAttemptAsync(token);
                if (IsConnected)
                {
                    PublishStatus(_ballReady ? "Connected — ball ready" : "Connected — activity preserved");
                    return;
                }
            }

            if (_connectionRequested)
            {
                SetState(LaunchMonitorConnectionState.Faulted);
                PublishStatus("Automatic reconnect paused after four attempts. Click Connect to try again.");
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            Interlocked.Exchange(ref _reconnectLoopActive, 0);
        }
    }

    private void SetState(LaunchMonitorConnectionState state)
    {
        try
        {
            _stateMachine.TransitionTo(state);
        }
        catch (InvalidOperationException)
        {
            _stateMachine.RecoverTo(state);
        }
    }

    private void PublishStatus(string message)
    {
        var health = new LaunchMonitorHealth(
            _stateMachine.State,
            _lastPacketAt,
            _lastHeartbeatAt,
            _reconnectAttempts,
            _receivedShots,
            0,
            _duplicatePackets,
            null,
            _lastError);
        StatusChanged?.Invoke(this, new(IsConnected, _ballReady, _batteryPercent, message, _stateMachine.State, health));
    }

    private async Task CleanupAsync()
    {
        _connectionCts?.Cancel();
        if (_notifications is not null)
        {
            _notifications.ValueChanged -= OnNotificationReceived;
            try
            {
                await _notifications.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.None);
            }
            catch
            {
            }
        }

        if (_battery is not null)
            _battery.ValueChanged -= OnBatteryChanged;

        if (_device is not null)
        {
            _device.ConnectionStatusChanged -= OnConnectionStatusChanged;
            _device.Dispose();
        }

        foreach (var service in _services)
            service.Dispose();

        _services.Clear();
        _device = null;
        _command = null;
        _notifications = null;
        _battery = null;
        _connectionCts?.Dispose();
        _connectionCts = null;
        _batteryPercent = null;
        _ballReady = false;
    }

    private byte NextSequence() => _sequence++;

    private void InitializeDiagnosticsLog()
    {
        if (DiagnosticsPath is not null)
        {
            File.AppendAllText(DiagnosticsPath, $"Square Golf BLE connection attempt {DateTimeOffset.Now:O}{Environment.NewLine}");
            return;
        }
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Golf Grind");
        Directory.CreateDirectory(directory);
        DiagnosticsPath = Path.Combine(directory, $"square-packets-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        File.WriteAllText(DiagnosticsPath, $"Square Golf BLE packet capture started {DateTimeOffset.Now:O}{Environment.NewLine}");
    }

    private async Task LogPacketAsync(string direction, byte[]? packet)
    {
        if (DiagnosticsPath is null || packet is null)
            return;

        var lockTaken = false;
        try
        {
            await _diagnosticWrites.WaitAsync();
            lockTaken = true;
            var line = $"{DateTimeOffset.Now:O} {direction} {packet.Length,3} {Convert.ToHexString(packet)}{Environment.NewLine}";
            await File.AppendAllTextAsync(DiagnosticsPath, line);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not write Square diagnostic packet");
        }
        finally
        {
            if (lockTaken)
                _diagnosticWrites.Release();
        }
    }

    private static bool IsSquareName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && (name.Contains("Square", StringComparison.OrdinalIgnoreCase) || name.Contains("BlueZ", StringComparison.OrdinalIgnoreCase));
    private static string DisplayName(BluetoothLEDevice device) => string.IsNullOrWhiteSpace(device.Name) ? "Square Golf" : device.Name;

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _reconnectCts?.Dispose();
        _lifecycle.Dispose();
        _writes.Dispose();
        _diagnosticWrites.Dispose();
    }
}
