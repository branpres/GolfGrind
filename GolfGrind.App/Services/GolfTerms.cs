namespace GolfGrind.App.Services;

public static class GolfTerms
{
    public const string Carry = "How far the ball travels through the air before first touching the ground.";
    public const string Total = "How far the ball finishes from the golfer after carry, bounce, and rollout.";
    public const string Rollout = "How far the ball travels after its first landing, calculated as total distance minus carry distance.";
    public const string Offline = "How far left or right the ball finishes from the intended target line.";
    public const string OfflineTendency = "The average left-or-right finish relative to the target line. This helps show a repeated directional miss.";
    public const string Apex = "The highest point of the ball's flight.";
    public const string FlightTime = "The estimated time the ball remains in the air.";
    public const string BallSpeed = "The speed of the golf ball immediately after impact.";
    public const string Launch = "The vertical angle at which the ball leaves the clubface.";
    public const string LaunchDirection = "The horizontal direction the ball starts relative to the target line.";
    public const string TotalSpin = "The ball's overall spin rate immediately after impact.";
    public const string SpinAxis = "The tilt of the ball's spin, which helps determine whether it curves left or right.";
    public const string Backspin = "The backward rotation that helps create lift and affects carry and stopping power.";
    public const string Sidespin = "A familiar way of expressing the sideways part of spin that contributes to curve.";
    public const string Loft = "The angle of the clubface relative to vertical. More loft generally launches the ball higher and shorter.";
    public const string StockCarry = "The golfer's expected full-swing carry for this club. A manually entered value overrides the calculated median.";
    public const string Gap = "The carry-distance difference between this club and the next longer club.";
    public const string Average = "The arithmetic mean: all retained values added together and divided by the number of values.";
    public const string Median = "The middle retained value after the results are sorted, which reduces the influence of unusually long or short shots.";
    public const string TypicalRange = "The middle 60% of retained carry distances, showing the range where most normal shots finish.";
    public const string Consistency = "The standard deviation of retained carry distances. A lower number means the carries are grouped more tightly and are more repeatable.";
    public const string Dispersion = "The overall left-to-right spread of retained shots.";
    public const string Confidence = "An estimate of how dependable the calculated club numbers are, based on the amount of retained data.";
    public const string Swing = "The recorded swing length used for the shot, such as half, three-quarter, or full.";
    public const string Target = "The distance the golfer was asked to hit for this practice shot.";
    public const string Result = "Whether the shot met the active practice mode's goal.";
    public const string Score = "Points awarded by the scoring rules saved with that practice shot.";
    public const string ScoringRadius = "The maximum distance from the center of the target that still counts as a hit.";
}
