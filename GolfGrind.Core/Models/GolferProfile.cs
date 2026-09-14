namespace GolfGrind.Core.Models;

public sealed class GolferProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Golfer";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}
