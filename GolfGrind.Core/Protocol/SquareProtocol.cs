using System.Buffers.Binary;
using GolfGrind.Core.Models;
using GolfGrind.Core.Services;

namespace GolfGrind.Core.Protocol;

public static class SquareProtocol
{
    public static readonly Guid CommandCharacteristic = new("86602101-6b7e-439a-bdd1-489a3213e9bb");
    public static readonly Guid NotificationCharacteristic = new("86602102-6b7e-439a-bdd1-489a3213e9bb");
    public static readonly Guid FirmwareCharacteristic = new("86602003-6b7e-439a-bdd1-489a3213e9bb");
    public static readonly Guid BatteryCharacteristic = new("00002a19-0000-1000-8000-00805f9b34fb");

    public const double MetersPerSecondToMph = 2.2369362920544;

    public static byte[] Heartbeat(byte sequence) => [0x11, 0x83, sequence, 0, 0, 0, 0, 0];

    public static byte[] EnableBallDetection(byte sequence, bool advancedSpin = true) =>
        [0x11, 0x81, sequence, 0x01, advancedSpin ? (byte)0x11 : (byte)0x10, 0, 0, 0, 0];

    public static byte[] RequestClubMetrics(byte sequence) => [0x11, 0x87, sequence, 0, 0, 0, 0, 0, 0];

    public static byte[] SelectClub(byte sequence, string club, bool leftHanded = false)
    {
        var (clubNumber, clubType) = ClubCode(club);
        return [0x11, 0x82, sequence, clubNumber, clubType, leftHanded ? (byte)1 : (byte)0, 0, 0, 0];
    }

    public static bool TryParseBallReady(ReadOnlySpan<byte> packet, out bool ready)
    {
        ready = false;
        if (packet.Length < 5 || packet[0] != 0x11 || packet[1] != 0x01)
            return false;

        ready = (packet[3] is 0x01 or 0x02) && packet[4] == 0x01;
        return true;
    }

    public static bool TryParseBallMetrics(ReadOnlySpan<byte> packet, out SquareBallMetrics metrics)
    {
        metrics = default;
        if (packet.Length < 17 || packet[0] != 0x11 || packet[1] != 0x02)
            return false;

        var speedMps = ReadInt16(packet, 3) / 100d;
        var launch = ReadInt16(packet, 5) / 100d;
        var direction = ReadInt16(packet, 7) / 100d;
        var spin = ReadInt16(packet, 9);
        var spinAxis = ReadInt16(packet, 11) / -100d;
        var backSpin = ReadInt16(packet, 13);
        var sideSpin = ReadInt16(packet, 15);

        if (speedMps <= 0 || speedMps >= 120 || launch < -10 || launch > 90 || spin < 0 || spin >= 30_000)
            return false;

        metrics = new(speedMps * MetersPerSecondToMph, launch, direction, spin, spinAxis, backSpin, sideSpin);
        return true;
    }

    public static bool TryParseClubMetrics(ReadOnlySpan<byte> packet, out SquareClubMetrics metrics)
    {
        metrics = default;
        if (packet.Length < 11 || packet[0] != 0x11 || packet[1] != 0x07 || packet[2] != 0x0f)
            return false;

        metrics = new(
            ReadInt16(packet, 3) / 100d,
            ReadInt16(packet, 5) / 100d,
            ReadInt16(packet, 7) / 100d,
            ReadInt16(packet, 9) / 100d);
        return true;
    }

    public static ShotData ToShot(string club, SquareBallMetrics ball, SquareClubMetrics? clubMetrics = null) =>
        BallFlightCalculator.Calculate(new(
            DateTimeOffset.Now,
            club,
            CarryYards: null,
            TotalYards: null,
            OfflineYards: null,
            ApexYards: null,
            FlightTimeSeconds: null,
            BallSpeedMph: ball.BallSpeedMph,
            LaunchAngleDeg: ball.LaunchAngleDeg,
            LaunchDirectionDeg: ball.LaunchDirectionDeg,
            BackSpinRpm: ball.BackSpinRpm,
            SideSpinRpm: ball.SideSpinRpm,
            ClubSpeedMph: null,
            ClubPathDeg: clubMetrics?.ClubPathDeg,
            FaceAngleDeg: clubMetrics?.FaceAngleDeg,
            AttackAngleDeg: clubMetrics?.AttackAngleDeg,
            DynamicLoftDeg: clubMetrics?.DynamicLoftDeg,
            TotalSpinRpm: ball.TotalSpinRpm,
            SpinAxisDeg: ball.SpinAxisDeg));

    private static short ReadInt16(ReadOnlySpan<byte> packet, int offset) =>
        BinaryPrimitives.ReadInt16LittleEndian(packet.Slice(offset, 2));

    private static (byte Number, byte Type) ClubCode(string club)
    {
        var normalized = club.Trim();
        if (normalized.Contains("Driver", StringComparison.OrdinalIgnoreCase)) return (0x02, 0x04);
        if (normalized.Contains("Putter", StringComparison.OrdinalIgnoreCase)) return (0x01, 0x07);
        if (normalized.StartsWith("PW", StringComparison.OrdinalIgnoreCase)) return (0x0a, 0x06);
        if (normalized.StartsWith("GW", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("AW", StringComparison.OrdinalIgnoreCase)) return (0x0b, 0x06);
        if (normalized.Contains("Wedge", StringComparison.OrdinalIgnoreCase) || normalized.Contains('°')) return (0x0c, 0x06);

        var number = LeadingClubNumber(normalized);
        if (normalized.Contains("Wood", StringComparison.OrdinalIgnoreCase))
            return ((byte)Math.Clamp(number ?? 5, 1, 15), 0x05);
        if (normalized.Contains("Iron", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("Hybrid", StringComparison.OrdinalIgnoreCase))
            return ((byte)Math.Clamp(number ?? 7, 1, 15), 0x06);

        return (0x07, 0x06);
    }

    private static int? LeadingClubNumber(string club)
    {
        var digits = new string(club.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var number) ? number : null;
    }
}

public readonly record struct SquareBallMetrics(
    double BallSpeedMph,
    double LaunchAngleDeg,
    double LaunchDirectionDeg,
    double TotalSpinRpm,
    double SpinAxisDeg,
    double BackSpinRpm,
    double SideSpinRpm);

public readonly record struct SquareClubMetrics(
    double ClubPathDeg,
    double FaceAngleDeg,
    double AttackAngleDeg,
    double DynamicLoftDeg);
