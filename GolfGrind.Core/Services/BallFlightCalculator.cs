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
    internal const double RolloutCalibrationScale = 3.0;
    private const double MinimumMeasuredSpinRpm = 100;
    private const double GroundResistanceCoefficient = 1.02;

    public static ShotData Calculate(ShotData measured)
    {
        if (measured.BallSpeedMph <= 0)
            return measured;

        var speed = measured.BallSpeedMph * MphToMetersPerSecond;
        var elevation = DegreesToRadians(measured.LaunchAngleDeg);
        var azimuth = DegreesToRadians(measured.LaunchDirectionDeg);
        var spin = ResolveSpin(measured);
        var totalSpin = spin.TotalSpinRpm;
        var spinAxis = DegreesToRadians(spin.SpinAxisDeg);

        // A launch at or below the horizon is a ground-running shot, not an
        // airborne flight. Model its energy loss separately so a worm burner
        // can still have a realistic total distance without inventing carry.
        if (measured.LaunchAngleDeg <= 0.5)
            return CalculateGroundShot(measured, speed, azimuth, spin);

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
        // Carry is the horizontal distance from the origin to the landing
        // point. Square includes lateral travel; using only Y understates a
        // shot increasingly as it finishes farther offline.
        var carryYards = Math.Sqrt(position.X * position.X + position.Y * position.Y) / MetersPerYard;
        var offlineYards = position.X / MetersPerYard;
        var rolloutYards = EstimateRolloutYards(velocity, totalSpin, carryYards);

        return measured with
        {
            CarryYards = Math.Round(Math.Max(0, carryYards), 1),
            TotalYards = Math.Round(Math.Max(0, carryYards + rolloutYards), 1),
            OfflineYards = Math.Round(offlineYards, 1),
            ApexYards = Math.Round(apexMeters / MetersPerYard, 1),
            FlightTimeSeconds = Math.Round(time, 2),
            BackSpinRpm = Math.Round(spin.BackSpinRpm),
            SideSpinRpm = Math.Round(spin.SideSpinRpm),
            TotalSpinRpm = Math.Round(spin.TotalSpinRpm),
            SpinAxisDeg = Math.Round(spin.SpinAxisDeg, 1),
            SpinSource = spin.Source,
            FlightPath = path
        };
    }

    private static ShotData CalculateGroundShot(ShotData measured, double speed, double azimuth, ResolvedSpin spin)
    {
        var totalMeters = speed * speed /
            (2 * GravityMetersPerSecondSquared * GroundResistanceCoefficient);
        var totalYards = totalMeters / MetersPerYard;
        var downRangeYards = totalYards * Math.Cos(azimuth);
        var offlineAtRestYards = totalYards * Math.Sin(azimuth);

        return measured with
        {
            CarryYards = 0,
            TotalYards = Math.Round(totalYards, 1),
            OfflineYards = 0,
            ApexYards = 0,
            FlightTimeSeconds = 0,
            BackSpinRpm = Math.Round(spin.BackSpinRpm),
            SideSpinRpm = Math.Round(spin.SideSpinRpm),
            TotalSpinRpm = Math.Round(spin.TotalSpinRpm),
            SpinAxisDeg = Math.Round(spin.SpinAxisDeg, 1),
            SpinSource = spin.Source,
            FlightPath =
            [
                new FlightPoint(0, 0, 0),
                new FlightPoint(downRangeYards, offlineAtRestYards, 0)
            ]
        };
    }

    private static ResolvedSpin ResolveSpin(ShotData shot)
    {
        var vectorSpin = Math.Sqrt(shot.BackSpinRpm * shot.BackSpinRpm + shot.SideSpinRpm * shot.SideSpinRpm);
        var total = shot.TotalSpinRpm.GetValueOrDefault();
        var axis = shot.SpinAxisDeg.GetValueOrDefault();
        var hasTotal = shot.TotalSpinRpm.HasValue && double.IsFinite(total) && total >= MinimumMeasuredSpinRpm;
        var hasVector = double.IsFinite(vectorSpin) && vectorSpin >= MinimumMeasuredSpinRpm;
        var hasAxis = shot.SpinAxisDeg.HasValue && double.IsFinite(axis);

        if (hasVector)
        {
            var vectorAxis = RadiansToDegrees(Math.Atan2(shot.SideSpinRpm, shot.BackSpinRpm));
            // A negative backspin component represents topspin. Its full
            // vector direction must win over a conventional small axis value
            // or the model would incorrectly turn downward lift into upward lift.
            if (shot.BackSpinRpm < 0 || !hasTotal || !hasAxis)
                return FromTotalAndAxis(vectorSpin, vectorAxis, SpinDataSource.Measured);
        }

        if (hasTotal && hasAxis)
            return FromTotalAndAxis(total, axis, SpinDataSource.Measured);

        if (hasTotal)
            return FromTotalAndAxis(total, 0, SpinDataSource.Estimated);

        var estimatedTotal = EstimateTotalSpinRpm(shot);
        var estimatedAxis = hasAxis ? axis : 0;
        return FromTotalAndAxis(estimatedTotal, estimatedAxis, SpinDataSource.Estimated);
    }

    private static ResolvedSpin FromTotalAndAxis(double totalSpinRpm, double spinAxisDeg, SpinDataSource source)
    {
        var radians = DegreesToRadians(spinAxisDeg);
        return new(
            totalSpinRpm,
            spinAxisDeg,
            totalSpinRpm * Math.Cos(radians),
            totalSpinRpm * Math.Sin(radians),
            source);
    }

    private static double EstimateTotalSpinRpm(ShotData shot)
    {
        var club = shot.Club.Trim();
        var (baselineSpin, baselineLaunch) = club switch
        {
            var value when value.Contains("Driver", StringComparison.OrdinalIgnoreCase) => (2600d, 13d),
            var value when value.Contains("Wood", StringComparison.OrdinalIgnoreCase) => (3400d, 14d),
            var value when value.Contains("Hybrid", StringComparison.OrdinalIgnoreCase) => (4000d, 16d),
            var value when value.StartsWith("PW", StringComparison.OrdinalIgnoreCase) ||
                           value.Contains("P-Wedge", StringComparison.OrdinalIgnoreCase) => (7500d, 25d),
            var value when value.StartsWith("GW", StringComparison.OrdinalIgnoreCase) ||
                           value.StartsWith("AW", StringComparison.OrdinalIgnoreCase) => (8500d, 28d),
            var value when value.Contains("Wedge", StringComparison.OrdinalIgnoreCase) || value.Contains('°') => (9000d, 30d),
            _ => EstimateIronSpin(club)
        };

        // Launch supplies a modest correction without pretending that an
        // unmeasured spin value has the precision of a marked-ball reading.
        return Math.Clamp(baselineSpin + (shot.LaunchAngleDeg - baselineLaunch) * 80, 1200, 11_000);
    }

    private static (double SpinRpm, double LaunchDeg) EstimateIronSpin(string club)
    {
        var numberText = new string(club.TakeWhile(char.IsDigit).ToArray());
        if (!int.TryParse(numberText, out var number))
            return (5500, 19);

        number = Math.Clamp(number, 3, 9);
        return (2400 + number * 450, 10 + number * 1.3);
    }

    private static double EstimateRolloutYards(Vector3 landingVelocity, double spinRpm, double carryYards)
    {
        var horizontalSpeed = Math.Sqrt(landingVelocity.X * landingVelocity.X + landingVelocity.Y * landingVelocity.Y);
        var descentAngle = Math.Atan2(Math.Abs(landingVelocity.Z), Math.Max(0.1, horizontalSpeed));
        var landingFactor = Math.Clamp(1 - descentAngle / DegreesToRadians(55), 0.08, 0.85);
        var spinFactor = Math.Clamp(1 - spinRpm / 10_000d, 0.12, 0.82);
        var rollMeters = horizontalSpeed * horizontalSpeed / (2 * GravityMetersPerSecondSquared * 1.7)
            * landingFactor * spinFactor * RolloutCalibrationScale;

        // Low-launch shots can run well beyond 18% of carry. The wider ceiling
        // still leaves landing speed, descent angle, and spin in control of the
        // estimate while avoiding an artificial cutoff for punch/thin shots.
        return Math.Clamp(rollMeters / MetersPerYard, 0, Math.Max(3, carryYards * 0.55));
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

    private readonly record struct ResolvedSpin(
        double TotalSpinRpm,
        double SpinAxisDeg,
        double BackSpinRpm,
        double SideSpinRpm,
        SpinDataSource Source);
}
