using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Xunit.Abstractions;

namespace Golferoo.Sim.Tests;

/// <summary>C08 lot 2 : trajectoires (droit, draw, fade, chip) et spin d'un coup choisi.</summary>
public class ShapeTests(ITestOutputHelper output)
{
    private static readonly TerrainGrid Fairway = ShotTests.Uniform(Surface.Fairway, cells: 120);

    private static Point2 Start => new(20, 240);

    /// <summary>Coup prévu (sans hasard) vers +x : position finale et premier point de chute.</summary>
    private static ShotResult Shot(Club club, ShotShape shape, double force = 1.0, double amount = 1.0) =>
        new PhysicalShotResolver().ExecuteShaped(Start, club, 1, 0, force, shape, Fairway, null, amount: amount);

    private static SimulationResult Sim(Club club, ShotShape shape)
    {
        var (v, curve, spin) = ShotShapes.Launch(club, 1, 0, club.Speed, shape);
        return BallSimulator.Run(Fairway, Start, v, rolling: false, BallSimulator.DefaultDrag, recordPath: false, curve: curve, backspin: spin);
    }

    [Fact]
    public void Draw_EndsLeftOfStraight_FadeRight()
    {
        // Vers +x, la gauche d'un droitier est −z.
        var straight = Shot(Clubs.Iron7, ShotShape.Straight).Ball;
        Assert.True(Shot(Clubs.Iron7, ShotShape.Draw).Ball.Z < straight.Z - 3);
        Assert.True(Shot(Clubs.Iron7, ShotShape.Fade).Ball.Z > straight.Z + 3);
    }

    [Theory]
    [InlineData("Driver", 12.0, 18.0)]
    [InlineData("Fer 7", 5.0, 10.0)]
    public void Draw_CurvesAtLanding_AsCalibrated(string name, double min, double max)
    {
        var club = Clubs.Full.First(c => c.Name == name);
        var curved = Start.Z - Sim(club, ShotShape.Draw).FirstLanding.Z;
        output.WriteLine($"{name} : courbure {curved:F1} m à la chute, portée {Sim(club, ShotShape.Draw).FirstLanding.X - Start.X:F0} m");
        Assert.InRange(curved, min, max);
    }

    [Fact]
    public void Wedge_StopsCloserToItsLandingThanADriver()
    {
        double Roll(Club c)
        {
            var s = Sim(c, ShotShape.Straight);
            return s.Shot.Ball.X - s.FirstLanding.X;
        }

        output.WriteLine($"roulement : wedge {Roll(Clubs.Wedge):F1} m, driver {Roll(Clubs.Driver):F1} m");
        Assert.True(Roll(Clubs.Wedge) < Roll(Clubs.Driver) / 3);
    }

    [Fact]
    public void Chip_FliesLowerAndRollsMoreThanAStraightShot()
    {
        var chip = Sim(Clubs.Wedge, ShotShape.Chip);
        var straight = Sim(Clubs.Wedge, ShotShape.Straight);
        var chipRoll = chip.Shot.Ball.X - chip.FirstLanding.X;
        var straightRoll = straight.Shot.Ball.X - straight.FirstLanding.X;
        output.WriteLine($"wedge : chip {chip.MaxHeight:F1} m de haut, roule {chipRoll:F1} m ; droit {straight.MaxHeight:F1} m, roule {straightRoll:F1} m");
        Assert.True(chip.MaxHeight < straight.MaxHeight / 2);
        Assert.True(chipRoll > 1.5 * straightRoll);
    }

    [Fact]
    public void ZeroCurveAndSpin_IsTheShotOfC06BitForBit()
    {
        var club = Clubs.Driver;
        var v = new Vec3(club.LaunchCos * club.Speed, club.LaunchSin * club.Speed, 0);
        var a = BallSimulator.Run(Fairway, Start, v, rolling: false, BallSimulator.DefaultDrag, recordPath: true);
        var b = BallSimulator.Run(Fairway, Start, v, rolling: false, BallSimulator.DefaultDrag, recordPath: true, curve: 0.0, backspin: 0.0);
        Assert.Equal(a.Shot.Ball, b.Shot.Ball);
        Assert.Equal(a.Shot.Trajectory, b.Shot.Trajectory);
    }

    [Fact]
    public void Dogleg_ADrawAimedRightOfTheFairwayComesBackToIt_AStraightShotStaysInTheRough()
    {
        var t = HoleLoader.Load(Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "dogleg-par4.json"), 2);
        var a = 4 * Math.PI / 180;
        var r = new PhysicalShotResolver();

        var straight = r.ExecuteShaped(t.Tee, Clubs.Driver, Math.Cos(a), Math.Sin(a), 1.0, ShotShape.Straight, t, null);
        var draw = r.ExecuteShaped(t.Tee, Clubs.Driver, Math.Cos(a), Math.Sin(a), 1.0, ShotShape.Draw, t, null);

        Assert.Equal(ShotOutcome.Rough, straight.Outcome);
        Assert.Equal(ShotOutcome.Fairway, draw.Outcome);
        Assert.True(Dist(draw.Ball, t.Hole) < Dist(straight.Ball, t.Hole) - 15);
    }

    private static double Dist(Point2 a, Point2 b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));

    [Fact]
    public void LessAmount_CurvesLess()
    {
        var full = Start.Z - Shot(Clubs.Iron5, ShotShape.Draw).Ball.Z;
        var half = Start.Z - Shot(Clubs.Iron5, ShotShape.Draw, amount: 0.5).Ball.Z;
        Assert.InRange(half, 0.3 * full, 0.7 * full);
    }
}
