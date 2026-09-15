using System.Buffers.Binary;
using GolfGrind.Core.Models;
using GolfGrind.Core.Protocol;
using GolfGrind.Core.Services;

Check(Convert.ToHexString(SquareProtocol.Heartbeat(0x2a)) == "11832A0000000000", "heartbeat command");
Check(Convert.ToHexString(SquareProtocol.EnableBallDetection(3)) == "118103011100000000", "ready command");
Check(Convert.ToHexString(SquareProtocol.SelectClub(4, "7 Iron")) == "118204070600000000", "club command");
Check(Convert.ToHexString(SquareProtocol.SelectClub(4, "Wedge · 56°")) == "1182040C0600000000", "custom wedge command");
Check(Convert.ToHexString(SquareProtocol.SelectClub(4, "4 Hybrid")) == "118204040600000000", "custom hybrid command");

var readyPacket = new byte[17];
readyPacket[0] = 0x11;
readyPacket[1] = 0x01;
readyPacket[3] = 0x01;
readyPacket[4] = 0x01;
Check(SquareProtocol.TryParseBallReady(readyPacket, out var ready) && ready, "ball-ready packet");
readyPacket[3] = 0x00;
Check(SquareProtocol.TryParseBallReady(readyPacket, out ready) && !ready, "detected ball is not ready yet");
readyPacket[3] = 0x02;
Check(SquareProtocol.TryParseBallReady(readyPacket, out ready) && !ready, "undocumented ball state is not treated as ready");

var shotPacket = new byte[17];
shotPacket[0] = 0x11;
shotPacket[1] = 0x02;
shotPacket[2] = 0x37;
Write(shotPacket, 3, 50_00);   // 50 m/s
Write(shotPacket, 5, 18_25);   // 18.25 degrees
Write(shotPacket, 7, -1_50);   // 1.5 degrees left
Write(shotPacket, 9, 5_800);
Write(shotPacket, 11, 3_25);   // sign is inverted by the protocol
Write(shotPacket, 13, 5_790);
Write(shotPacket, 15, -350);

Check(SquareProtocol.TryParseBallMetrics(shotPacket, out var ball), "shot packet recognized");
Check(Math.Abs(ball.BallSpeedMph - 111.8468) < .001, "ball speed conversion");
Check(ball.LaunchAngleDeg == 18.25 && ball.LaunchDirectionDeg == -1.5, "launch angles");
Check(ball.TotalSpinRpm == 5_800 && ball.SpinAxisDeg == -3.25, "spin metrics");
Check(ball.BackSpinRpm == 5_790 && ball.SideSpinRpm == -350, "component spin");

var calculatedShot = SquareProtocol.ToShot("7 Iron", ball);
Check(calculatedShot.CarryYards is > 145 and < 175, "plausible 7-iron carry");
Check(calculatedShot.TotalYards >= calculatedShot.CarryYards, "total includes non-negative rollout");
Check(calculatedShot.ApexYards is > 20 and < 40, "plausible apex");
Check(calculatedShot.FlightTimeSeconds is > 4 and < 7, "plausible flight time");
Check(calculatedShot.FlightPath is { Count: > 20 }, "flight path generated");

var wedge = new GolfClub { Name = "Wedge", Kind = GolfClubKind.Wedge, LoftDegrees = 54 };
var analyticsSession = new PracticeSession();
analyticsSession.Shots.Add(StoredShot.FromShot(calculatedShot with { Club = wedge.DisplayName, CarryYards = 88, SwingType = "Full" }, wedge.Id));
analyticsSession.Shots.Add(StoredShot.FromShot(calculatedShot with { Club = wedge.DisplayName, CarryYards = 92, SwingType = "Full" }, wedge.Id));
analyticsSession.Shots.Add(StoredShot.FromShot(calculatedShot with { Club = wedge.DisplayName, CarryYards = 61, SwingType = "Half" }, wedge.Id));
var wedgeStats = ShotAnalytics.CalculateBag([wedge], [analyticsSession]).Single();
Check(wedgeStats.ShotCount == 2 && wedgeStats.MedianCarryYards == 90, "bag mapping uses full wedge swings");
var halfWedge = ShotAnalytics.CalculateWedgeCell(analyticsSession.Shots, "Half");
Check(halfWedge.ShotCount == 1 && halfWedge.MedianCarryYards == 61, "wedge matrix groups swing length");
analyticsSession.Shots[1] = analyticsSession.Shots[1] with { ExcludedFromAnalytics = true };
var filteredWedgeStats = ShotAnalytics.CalculateBag([wedge], [analyticsSession]).Single();
Check(filteredWedgeStats.ShotCount == 1 && filteredWedgeStats.MedianCarryYards == 88, "excluded shot omitted from analytics");
var filteredFullWedge = ShotAnalytics.CalculateWedgeCell(analyticsSession.Shots, "Full");
Check(filteredFullWedge.ShotCount == 1 && filteredFullWedge.MedianCarryYards == 88, "excluded shot omitted from wedge matrix");

var storedPracticeShot = StoredShot.FromShot(calculatedShot with
{
    PracticeMode = "Target",
    TargetYards = 150,
    ProximityYards = 4.2,
    PracticeScore = 10
});
Check(storedPracticeShot.PracticeMode == "Target" &&
      storedPracticeShot.TargetYards == 150 &&
      storedPracticeShot.ProximityYards == 4.2 &&
      storedPracticeShot.PracticeScore == 10,
    "practice result metadata persists");

var clubPacket = new byte[11];
clubPacket[0] = 0x11;
clubPacket[1] = 0x07;
clubPacket[2] = 0x0f;
Write(clubPacket, 3, 1_25);
Write(clubPacket, 5, -75);
Write(clubPacket, 7, -3_20);
Write(clubPacket, 9, 21_50);
Check(SquareProtocol.TryParseClubMetrics(clubPacket, out var club), "club packet recognized");
Check(club.ClubPathDeg == 1.25 && club.FaceAngleDeg == -.75, "club path and face");
Check(club.AttackAngleDeg == -3.2 && club.DynamicLoftDeg == 21.5, "attack and loft");

Console.WriteLine("All Square protocol checks passed.");

static void Write(byte[] target, int offset, short value) =>
    BinaryPrimitives.WriteInt16LittleEndian(target.AsSpan(offset, 2), value);

static void Check(bool condition, string name)
{
    if (!condition)
        throw new InvalidOperationException($"Protocol check failed: {name}");
}
