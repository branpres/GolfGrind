using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GolfGrind.Core.Models;

namespace GolfGrind.App.Services;

public sealed class BackupService
{
    private const int CurrentFormatVersion = 3;
    private const string SessionCsvHeader = "Golfer,Session,Session Type,Game,Game Completed,Started,Ended,Hit At,Club,Swing,Practice Mode,Shot Number,Recommended Club,Recommended Swing,Target Yards,Proximity Yards,Score,Result,Included In Analytics,Carry Yards,Total Yards,Offline Yards,Apex Yards,Flight Time Seconds,Ball Speed MPH,Launch Angle Degrees,Launch Direction Degrees,Backspin RPM,Sidespin RPM,Total Spin RPM,Spin Axis Degrees,Flight Model,Calculation Profile,Altitude Feet,Temperature F,Humidity Percent,Reference Carry Scale,Personal Carry Scale,Personal Offline Bias Yards";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ExportJson(IEnumerable<GolferBackup> golfers, Guid activeGolferId)
    {
        var backup = new GolfGrindBackup
        {
            FormatVersion = CurrentFormatVersion,
            ExportedAt = DateTimeOffset.Now,
            ActiveGolferId = activeGolferId,
            Golfers = golfers.ToList()
        };
        var path = CreateExportPath("backup", "json");
        File.WriteAllText(path, JsonSerializer.Serialize(backup, JsonOptions));
        return path;
    }

    public string ExportCsv(IEnumerable<GolferBackup> golfers)
    {
        var path = CreateExportPath("shots", "csv");
        var builder = new StringBuilder();
        builder.AppendLine("Golfer,Session,Session Type,Game,Game Completed,Started,Hit At,Club,Swing,Practice Mode,Shot Number,Recommended Club,Recommended Swing,Target Yards,Proximity Yards,Score,Result,Included In Analytics,Carry Yards,Total Yards,Offline Yards,Ball Speed MPH,Launch Angle,Launch Direction,Total Spin RPM,Spin Axis,Flight Model,Calculation Profile,Altitude Feet,Temperature F,Humidity Percent,Reference Carry Scale,Personal Carry Scale,Personal Offline Bias");
        foreach (var golfer in golfers)
        {
            foreach (var session in golfer.Sessions.OrderBy(item => item.StartedAt))
            {
                foreach (var shot in session.Shots.OrderBy(item => item.HitAt))
                {
                    builder.AppendLine(string.Join(",",
                        Csv(golfer.Profile.Name), Csv(session.Name), Csv(session.SessionType), Csv(session.PracticeGame?.DisplayName()), session.GameSummary?.Completed.ToString() ?? "", Csv(session.StartedAt.ToString("O")), Csv(shot.HitAt.ToString("O")),
                        Csv(shot.Club), Csv(shot.SwingType), Csv(shot.PracticeMode), shot.PracticeGame?.ShotNumber.ToString(CultureInfo.InvariantCulture) ?? "", Csv(shot.PracticeGame?.RecommendedClub), Csv(shot.PracticeGame?.RecommendedSwing), Number(shot.TargetYards), Number(shot.ProximityYards), shot.PracticeScore?.ToString(CultureInfo.InvariantCulture) ?? "", Csv(shot.PracticeGame?.ResultLabel), shot.ExcludedFromAnalytics ? "No" : "Yes",
                        Number(shot.CarryYards), Number(shot.TotalYards), Number(shot.OfflineYards), Number(shot.BallSpeedMph), Number(shot.LaunchAngleDeg),
                        Number(shot.LaunchDirectionDeg), Number(shot.TotalSpinRpm), Number(shot.SpinAxisDeg), Csv(shot.Calculation?.FlightModel), Csv(shot.Calculation?.CalculationProfile),
                        Number(shot.Calculation?.Environment.AltitudeFeet), Number(shot.Calculation?.Environment.TemperatureFahrenheit), Number(shot.Calculation?.Environment.RelativeHumidityPercent),
                        Number(shot.Calculation?.Environment.ReferenceCarryScale), Number(shot.Calculation?.PersonalAdjustment.CarryScale), Number(shot.Calculation?.PersonalAdjustment.OfflineBiasYards)));
                }
            }
        }
        File.WriteAllText(path, builder.ToString());
        return path;
    }

    public string ExportSessionCsv(string golferName, PracticeSession session)
    {
        var path = CreateExportPath($"session-{SafeFilePart(session.Name)}", "csv");
        var builder = new StringBuilder();
        builder.AppendLine(SessionCsvHeader);
        foreach (var shot in session.Shots.OrderBy(item => item.HitAt))
            AppendSessionShot(builder, golferName, session, shot);
        File.WriteAllText(path, builder.ToString());
        return path;
    }

    public GolfGrindBackup ReadJson(string path)
    {
        var backup = JsonSerializer.Deserialize<GolfGrindBackup>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidOperationException("The selected file is not a valid Golf Grind backup.");
        if (backup.FormatVersion is < 1 or > CurrentFormatVersion)
            throw new InvalidOperationException($"Backup format {backup.FormatVersion} is not supported by this app version.");

        if (backup.FormatVersion == 1)
        {
            var legacyProfile = new GolferProfile { Name = "Imported golfer" };
            backup.Golfers = [new GolferBackup
            {
                Profile = legacyProfile,
                Bag = backup.Bag ?? [],
                Sessions = backup.Sessions ?? []
            }];
            backup.ActiveGolferId = legacyProfile.Id;
        }

        ValidateGolfers(backup.Golfers);
        backup.FormatVersion = CurrentFormatVersion;
        backup.Bag = null;
        backup.Sessions = null;
        return backup;
    }

    private static void ValidateGolfers(List<GolferBackup>? golfers)
    {
        if (golfers is not { Count: > 0 })
            throw new InvalidOperationException("The backup does not contain any golfer profiles.");
        if (golfers.Any(golfer => golfer.Profile is null || golfer.Profile.Id == Guid.Empty || string.IsNullOrWhiteSpace(golfer.Profile.Name)))
            throw new InvalidOperationException("The backup contains an invalid golfer profile.");
        if (golfers.Select(golfer => golfer.Profile.Id).Distinct().Count() != golfers.Count)
            throw new InvalidOperationException("The backup contains duplicate golfer identifiers.");

        foreach (var golfer in golfers)
        {
            if (golfer.Bag is not { Count: > 0 } || golfer.Bag.Any(club => string.IsNullOrWhiteSpace(club.Name)))
                throw new InvalidOperationException($"The backup does not contain a valid golf bag for {golfer.Profile.Name}.");
            if (golfer.Bag.Select(club => club.Id).Distinct().Count() != golfer.Bag.Count)
                throw new InvalidOperationException($"The backup contains duplicate club identifiers for {golfer.Profile.Name}.");
            golfer.Sessions ??= [];
            if (golfer.Sessions.Select(session => session.Id).Distinct().Count() != golfer.Sessions.Count)
                throw new InvalidOperationException($"The backup contains duplicate session identifiers for {golfer.Profile.Name}.");
            foreach (var session in golfer.Sessions)
                session.Shots ??= [];
        }
    }

    private static string CreateExportPath(string suffix, string extension)
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Golf Grind");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"golf-grind-{suffix}-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}");
    }

    private static void AppendSessionShot(StringBuilder builder, string golferName, PracticeSession session, StoredShot shot)
    {
        builder.AppendLine(string.Join(",",
            Csv(golferName), Csv(session.Name), Csv(session.SessionType), Csv(session.PracticeGame?.DisplayName()), session.GameSummary?.Completed.ToString() ?? "",
            Csv(session.StartedAt.ToString("O")), Csv(session.EndedAt?.ToString("O")), Csv(shot.HitAt.ToString("O")), Csv(shot.Club), Csv(shot.SwingType), Csv(shot.PracticeMode),
            shot.PracticeGame?.ShotNumber.ToString(CultureInfo.InvariantCulture) ?? "", Csv(shot.PracticeGame?.RecommendedClub), Csv(shot.PracticeGame?.RecommendedSwing),
            Number(shot.TargetYards), Number(shot.ProximityYards), shot.PracticeScore?.ToString(CultureInfo.InvariantCulture) ?? "", Csv(shot.PracticeGame?.ResultLabel), shot.ExcludedFromAnalytics ? "No" : "Yes",
            Number(shot.CarryYards), Number(shot.TotalYards), Number(shot.OfflineYards), Number(shot.ApexYards), Number(shot.FlightTimeSeconds), Number(shot.BallSpeedMph),
            Number(shot.LaunchAngleDeg), Number(shot.LaunchDirectionDeg), Number(shot.BackSpinRpm), Number(shot.SideSpinRpm), Number(shot.TotalSpinRpm), Number(shot.SpinAxisDeg),
            Csv(shot.Calculation?.FlightModel), Csv(shot.Calculation?.CalculationProfile), Number(shot.Calculation?.Environment.AltitudeFeet),
            Number(shot.Calculation?.Environment.TemperatureFahrenheit), Number(shot.Calculation?.Environment.RelativeHumidityPercent), Number(shot.Calculation?.Environment.ReferenceCarryScale),
            Number(shot.Calculation?.PersonalAdjustment.CarryScale), Number(shot.Calculation?.PersonalAdjustment.OfflineBiasYards)));
    }

    private static string SafeFilePart(string? value)
    {
        var source = string.IsNullOrWhiteSpace(value) ? "untitled" : value.Trim();
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(source.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
        var compact = string.Join("-", safe.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(compact) ? "untitled" : compact;
    }

    private static string Csv(string? value) => $"\"{(value ?? "").Replace("\"", "\"\"")}\"";
    private static string Number(double? value) => value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "";
    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}

public sealed class GolfGrindBackup
{
    public int FormatVersion { get; set; }
    public DateTimeOffset ExportedAt { get; set; }
    public Guid? ActiveGolferId { get; set; }
    public List<GolferBackup>? Golfers { get; set; }

    // Retained only so version 1 single-golfer backups remain importable.
    public List<GolfClub>? Bag { get; set; }
    public List<PracticeSession>? Sessions { get; set; }
}

public sealed class GolferBackup
{
    public GolferProfile Profile { get; set; } = new();
    public List<GolfClub> Bag { get; set; } = [];
    public List<PracticeSession> Sessions { get; set; } = [];
}
