using GolfGrind.Core.Models;

namespace GolfGrind.App.Components.Shared;

public sealed record NewGolfClubRequest(string Name, GolfClubKind Kind, double? LoftDegrees, double? StockCarryYards);
