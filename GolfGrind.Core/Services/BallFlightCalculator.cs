using GolfGrind.Core.Models;

namespace GolfGrind.Core.Services;

/// <summary>
/// Dependency-free, deterministic golf-ball flight estimate. Coordinates are
/// right-handed: X is right of target, Y is down range, and Z is up.
/// </summary>
public static class BallFlightCalculator
{
    private const double BallMassKg = 0.04593;
    private const double BallRadiusMeters = 0.021335;
    private const double BallAreaSquareMeters = Math.PI * BallRadiusMeters * BallRadiusMeters;
    private const double AirDensityKgPerCubicMeter = 1.225;
    private const double GravityMetersPerSecondSquared = 9.80665;
    private const double MetersPerYard = 0.9144;
    private const double MphToMetersPerSecond = 0.44704;
    private const double RpmToRadiansPerSecond = 2 * Math.PI / 60;
    private const double TimeStepSeconds = 0.005;

    public static ShotData Calculate(ShotData measured)
    {
        if (measured.BallSpeedMph <= 0)
            return measured;

        var speed = measured.BallSpeedMph * MphToMetersPerSecond;
        var elevation = DegreesToRadians(measured.LaunchAngleDeg);
        var azimuth = DegreesToRadians(measured.LaunchDirectionDeg);
        var totalSpin = Math.Max(0, measured.TotalSpinRpm ??
            Math.Sqrt(measured.BackSpinRpm * measured.BackSpinRpm + measured.SideSpinRpm * measured.SideSpinRpm));
        var spinAxis = DegreesToRadians(measured.SpinAxisDeg ??
            RadiansToDegrees(Math.Atan2(measured.SideSpinRpm, Math.Max(1, measured.BackSpinRpm))));

        var velocity = new Vector3(
            speed * Math.Cos(elevation) * Math.Sin(azimuth),
            speed * Math.Cos(elevation) * Math.Cos(azimuth),
            speed * Math.Sin(elevation));
        var position = new Vector3(0, 0, 0.02);

        // Backspin points along +X. Rotating that axis toward -Z models the
        // measured spin-axis tilt and produces the corresponding side force.
        var spinDirection = Normalize(new Vector3(Math.Cos(spinAxis), 0, -Math.Sin(spinAxis)));
        var spinRadiansPerSecond = totalSpin * RpmToRadiansPerSecond;
        var spinRatio = Math.Clamp(BallRadiusMeters * spinRadiansPerSecond / speed, 0, 0.4);

        // Empirical dimpled-ball coefficients. These are deliberately isolated
        // so recorded Square/software comparisons can tune the model later.
        var dragCoefficient = Math.Clamp(0.19 + 0.55 * spinRatio, 0.20, 0.38);
        var liftCoefficient = spinRatio < 0.001 ? 0 : Math.Clamp(0.04 + 1.60 * spinRatio, 0, 0.32);
        var aerodynamicFactor = 0.5 * AirDensityKgPerCubicMeter * BallAreaSquareMeters / BallMassKg;

        var path = new List<FlightPoint>(80) { ToFlightPoint(position) };
        var nextSampleAt = 0.08;
        var time = 0d;
        var apexMeters = position.Z;
        var previousPosition = position;

        while (time < 15)
        {
            var velocityMagnitude = Magnitude(velocity);
            if (velocityMagnitude < 0.1)
                break;

            previousPosition = position;
            var velocityDirection = velocity / velocityMagnitude;
            var dynamicAcceleration = aerodynamicFactor * velocityMagnitude * velocityMagnitude;
            var drag = velocityDirection * (-dynamicAcceleration * dragCoefficient);
            var liftDirection = Normalize(Cross(spinDirection, velocityDirection));
            var lift = liftDirection * (dynamicAcceleration * liftCoefficient);
            var acceleration = drag + lift + new Vector3(0, 0, -GravityMetersPerSecondSquared);

            velocity += acceleration * TimeStepSeconds;
            position += velocity * TimeStepSeconds;
            time += TimeStepSeconds;
            apexMeters = Math.Max(apexMeters, position.Z);

            if (time >= nextSampleAt)
            {
                path.Add(ToFlightPoint(position));
                nextSampleAt += 0.08;
            }

            if (position.Z <= 0 && time > 0.05)
            {
                var fraction = previousPosition.Z / (previousPosition.Z - position.Z);
                position = previousPosition + (position - previousPosition) * fraction;
                time -= TimeStepSeconds * (1 - fraction);
                break;
            }
        }

        path.Add(ToFlightPoint(position));
        var carryYards = position.Y / MetersPerYard;
        var offlineYards = position.X / MetersPerYard;
        var rolloutYards = EstimateRolloutYards(velocity, totalSpin, carryYards);

        return measured with
        {
            CarryYards = Math.Round(Math.Max(0, carryYards), 1),
            TotalYards = Math.Round(Math.Max(0, carryYards + rolloutYards), 1),
            OfflineYards = Math.Round(offlineYards, 1),
            ApexYards = Math.Round(apexMeters / MetersPerYard, 1),
            FlightTimeSeconds = Math.Round(time, 2),
            FlightPath = path
        };
    }

    private static double EstimateRolloutYards(Vector3 landingVelocity, double spinRpm, double carryYards)
    {
        var horizontalSpeed = Math.Sqrt(landingVelocity.X * landingVelocity.X + landingVelocity.Y * landingVelocity.Y);
        var descentAngle = Math.Atan2(Math.Abs(landingVelocity.Z), Math.Max(0.1, horizontalSpeed));
        var landingFactor = Math.Clamp(1 - descentAngle / DegreesToRadians(55), 0.08, 0.85);
        var spinFactor = Math.Clamp(1 - spinRpm / 10_000d, 0.12, 0.82);
        var rollMeters = horizontalSpeed * horizontalSpeed / (2 * GravityMetersPerSecondSquared * 1.7)
            * landingFactor * spinFactor;
        return Math.Clamp(rollMeters / MetersPerYard, 0, Math.Max(3, carryYards * 0.18));
    }

    private static FlightPoint ToFlightPoint(Vector3 value) => new(
        value.Y / MetersPerYard,
        value.X / MetersPerYard,
        Math.Max(0, value.Z / MetersPerYard));

    private static Vector3 Cross(Vector3 left, Vector3 right) => new(
        left.Y * right.Z - left.Z * right.Y,
        left.Z * right.X - left.X * right.Z,
        left.X * right.Y - left.Y * right.X);

    private static Vector3 Normalize(Vector3 value)
    {
        var magnitude = Magnitude(value);
        return magnitude < 0.000001 ? new Vector3(0, 0, 0) : value / magnitude;
    }

    private static double Magnitude(Vector3 value) =>
        Math.Sqrt(value.X * value.X + value.Y * value.Y + value.Z * value.Z);
    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
    private static double RadiansToDegrees(double radians) => radians * 180 / Math.PI;

    private readonly record struct Vector3(double X, double Y, double Z)
    {
        public static Vector3 operator +(Vector3 left, Vector3 right) =>
            new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        public static Vector3 operator -(Vector3 left, Vector3 right) =>
            new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        public static Vector3 operator *(Vector3 value, double scalar) =>
            new(value.X * scalar, value.Y * scalar, value.Z * scalar);
        public static Vector3 operator /(Vector3 value, double scalar) =>
            new(value.X / scalar, value.Y / scalar, value.Z / scalar);
    }
}
