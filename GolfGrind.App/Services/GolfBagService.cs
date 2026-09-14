using System.Text.Json;
using GolfGrind.Core.Models;

namespace GolfGrind.App.Services;

public sealed class GolfBagService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public List<GolfClub> Load(Guid profileId)
    {
        if (AtomicJsonFileStore.TryRead<List<GolfClub>>(GolfGrindDataPaths.BagFile(profileId), JsonOptions, out var clubs) &&
            clubs is { Count: > 0 })
        {
            var validClubs = clubs.Where(club => !string.IsNullOrWhiteSpace(club.Name)).ToList();
            if (validClubs.Count > 0)
                return validClubs;
        }

        return CreateDefaultBag();
    }

    public void Save(Guid profileId, IEnumerable<GolfClub> clubs)
    {
        var validClubs = clubs
            .Where(club => !string.IsNullOrWhiteSpace(club.Name))
            .Select(club =>
            {
                club.Name = club.Name.Trim();
                club.LoftDegrees = club.LoftDegrees is > 0 and <= 90
                    ? Math.Round(club.LoftDegrees.Value, 1)
                    : null;
                club.StockCarryYards = club.StockCarryYards is > 0 and <= 500
                    ? Math.Round(club.StockCarryYards.Value, 1)
                    : null;
                return club;
            })
            .ToList();

        if (validClubs.Count == 0)
            throw new InvalidOperationException("Your bag must contain at least one club.");

        AtomicJsonFileStore.Write(
            GolfGrindDataPaths.BagFile(profileId),
            validClubs,
            JsonOptions,
            storedClubs =>
            {
                if (storedClubs.Count == 0 || storedClubs.Any(club => club.Id == Guid.Empty || string.IsNullOrWhiteSpace(club.Name)))
                    throw new InvalidDataException("A golf bag must contain valid clubs.");
            });
    }

    public void Delete(Guid profileId)
    {
        var path = GolfGrindDataPaths.BagFile(profileId);
        File.Delete(path);
        File.Delete(path + ".good");
    }

    public static List<GolfClub> CreateDefaultBag() =>
    [
        New("Driver", GolfClubKind.Driver),
        New("5 Wood", GolfClubKind.Wood),
        New("5 Hybrid", GolfClubKind.Hybrid),
        New("5 Iron", GolfClubKind.Iron),
        New("6 Iron", GolfClubKind.Iron),
        New("7 Iron", GolfClubKind.Iron),
        New("8 Iron", GolfClubKind.Iron),
        New("9 Iron", GolfClubKind.Iron),
        New("PW", GolfClubKind.Wedge, 43),
        New("GW", GolfClubKind.Wedge, 48),
        New("Wedge", GolfClubKind.Wedge, 54),
        New("Wedge", GolfClubKind.Wedge, 58),
        New("Putter", GolfClubKind.Putter)
    ];

    private static GolfClub New(string name, GolfClubKind kind, double? loft = null) =>
        new() { Name = name, Kind = kind, LoftDegrees = loft };
}
