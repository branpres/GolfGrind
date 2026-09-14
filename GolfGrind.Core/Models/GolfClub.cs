using System.Text.Json.Serialization;

namespace GolfGrind.Core.Models;

public enum GolfClubKind
{
    Driver,
    Wood,
    Hybrid,
    Iron,
    Wedge,
    Putter
}

public sealed class GolfClub
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public GolfClubKind Kind { get; set; }
    public double? LoftDegrees { get; set; }
    public double? StockCarryYards { get; set; }

    [JsonIgnore]
    public string DisplayName => LoftDegrees is null
        ? Name
        : $"{Name} · {LoftDegrees:0.#}°";
}
