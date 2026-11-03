using Golferoo.Sim.Golfers;
using Golferoo.Sim.Pro;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Xunit.Abstractions;

namespace Golferoo.Sim.Tests;

/// <summary>C27 lot 1 : force qui suit le clic, dispersion proportionnelle à la distance visée, courbure accentuée du pro.</summary>
public class ProAimTests(ITestOutputHelper output)
{
    private static readonly TerrainGrid Fairway = ShotTests.Uniform(Surface.Fairway, cells: 120);
    private static readonly Point2 Start = new(20, 240);
    private static readonly Point2 Pin = new(400, 240);

    private static double Dist(Point2 a, Point2 b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));

    // ---- Force -----------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Driver", 0.5)]
    [InlineData("Fer 7", 0.5)]
    [InlineData("Wedge", 0.5)]
    [InlineData("Fer 5", 0.8)]
    public void Force_AtMidRange_LandsTheBallWithinTwoPercentOfTheClick(string name, double share)
    {
        var club = Clubs.Full.First(c => c.Name == name);
        var pro = new ProSkills();
        var resolver = new PhysicalShotResolver();
        var target = new Point2(Start.X + (club.TargetRange * share), Start.Z);
        var force = pro.ForceFor(resolver, Fairway, Start, club, ShotShape.Straight, target, Pin);
        var landed = pro.Execute(resolver, Fairway, Start, new ProShot(club, target, ShotShape.Straight, force), null, Pin).Ball;
        output.WriteLine($"{name} : force {force:F3}, balle à {Dist(Start, landed):F1} m pour {Dist(Start, target):F1} m visés");
        Assert.InRange(force, 0.2, 1.0);
        Assert.InRange(Dist(Start, landed), Dist(Start, target) * 0.98, Dist(Start, target) * 1.02);
    }

    [Fact]
    public void Force_BeyondTheRange_IsTheMaximum()
    {
        var pro = new ProSkills();
        var resolver = new PhysicalShotResolver();
        foreach (var club in Clubs.Full)
        {
            var target = new Point2(Start.X + (club.TargetRange * 1.5), Start.Z);
            Assert.Equal(1.0, pro.ForceFor(resolver, Fairway, Start, club, ShotShape.Straight, target, Pin));
        }
    }

    // ---- Dispersion ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AimFactor_IsProportionalToTheDistance_WithAFloor_AndNeverAboveOne()
    {
        var club = Clubs.Iron7;
        Assert.Equal(0.5, ProSkills.AimFactor(club, club.TargetRange / 2.0), 6);
        Assert.Equal(1.0, ProSkills.AimFactor(club, club.TargetRange), 6);
        Assert.Equal(1.0, ProSkills.AimFactor(club, club.TargetRange * 2.0), 6);
        Assert.Equal(0.15, ProSkills.AimFactor(club, 1.0), 6);
        Assert.Equal(0.15, ProSkills.AimFactor(club, 0.0), 6);
        Assert.Equal(1.0, ProSkills.AimFactor(Clubs.Putter, 5.0));
    }

    [Fact]
    public void Dispersion_AtMidRange_IsAboutHalfOfTheOneAtFullRange_AndTheFloorKeepsAnyClickImperfect()
    {
        var club = Clubs.Iron7;
        var pro = new ProSkills();
        var resolver = new PhysicalShotResolver();

        double Lateral(double distance)
        {
            var target = new Point2(Start.X + distance, Start.Z);
            var force = pro.ForceFor(resolver, Fairway, Start, club, ShotShape.Straight, target, Pin);
            var rng = new DeterministicRng(11);
            var sum = 0.0;
            const int n = 300;
            for (var i = 0; i < n; i++)
            {
                var z = pro.Execute(resolver, Fairway, Start, new ProShot(club, target, ShotShape.Straight, force), rng, Pin).Ball.Z - Start.Z;
                sum += z * z;
            }

            return Math.Sqrt(sum / n) / distance;
        }

        var full = Lateral(club.TargetRange * 0.95);
        var half = Lateral(club.TargetRange * 0.5);
        var near = Lateral(club.TargetRange * 0.05);
        output.WriteLine($"écart latéral relatif : pleine portée {full:F4}, mi-portée {half:F4}, tout près {near:F4}");
        Assert.InRange(half / full, 0.4, 0.62);
        Assert.True(near > 0.0);
    }

    // ---- Courbure --------------------------------------------------------------------------------------------------------------

    private static double Curve(Club club, double scale)
    {
        var (v, curve, spin) = ShotShapes.Launch(club, 1, 0, club.Speed, ShotShape.Draw, 1.0, scale);
        return Start.Z - BallSimulator.Run(Fairway, Start, v, rolling: false, BallSimulator.DefaultDrag, recordPath: false, curve: curve, backspin: spin).FirstLanding.Z;
    }

    [Fact]
    public void ProCurve_DriverDrawBendsFortyToFiftyMeters_AndAnIron7TwentyToTwentyEight()
    {
        var scale = ProAimParams.Default.CurveScale;
        var driver = Curve(Clubs.Driver, scale);
        var iron = Curve(Clubs.Iron7, scale);
        output.WriteLine($"ProCurveScale {scale} : driver {driver:F1} m, fer 7 {iron:F1} m");
        Assert.InRange(driver, 40.0, 50.0);
        Assert.InRange(iron, 20.0, 28.0);
    }

    [Fact]
    public void ProDraw_CurvesMoreThanTheClubShotOfOtherGolfers()
    {
        var pro = new ProSkills();
        var resolver = new PhysicalShotResolver();
        var target = new Point2(Start.X + 140, Start.Z);
        var proEnd = pro.Execute(resolver, Fairway, Start, new ProShot(Clubs.Iron7, target, ShotShape.Draw, 1.0), null, Pin).Ball;
        var plain = resolver.ExecuteShaped(Start, Clubs.Iron7, 1, 0, pro.SpeedFactor, ShotShape.Draw, Fairway, null).Ball;
        Assert.True(Start.Z - proEnd.Z > 2.0 * (Start.Z - plain.Z));
    }

    // ---- Enquête C27 critère 7 : « la balle part loin de la visée » ---------------------------------------------------------------

    [Fact]
    public void FarFromTheAim_TheWindMisreadAtLevelZero_ShiftsThePreviewByTensOfMeters_AndNothingAtMaxLevel()
    {
        var target = new Point2(Start.X + 170, Start.Z);
        var wind = new Vec3(0.0, 0.0, 8.0);
        var rookie = new ProSkills();
        var master = new ProSkills();
        for (var i = 0; i < ProSkills.Count; i++)
        {
            master.SetLevel((ProSkill)i, ProSkills.MaxLevel);
        }

        foreach (var (name, pro, expectZero) in new[] { ("niveau 0", rookie, false), ("niveau 50", master, true) })
        {
            var worst = 0.0;
            for (var tick = 1; tick <= 40; tick++)
            {
                var bias = pro.BiasFor(tick, 1, wind);
                var resolver = new PhysicalShotResolver { Wind = bias.Wind };
                var seen = pro.Execute(resolver, Fairway, Start, new ProShot(Clubs.Iron5, target, ShotShape.Straight, 1.0), null, Pin, bias).Ball;
                resolver.Wind = wind;
                var real = pro.Execute(resolver, Fairway, Start, new ProShot(Clubs.Iron5, target, ShotShape.Straight, 1.0), null, Pin, bias).Ball;
                worst = Math.Max(worst, Dist(seen, real));
            }

            output.WriteLine($"{name} : vent 8 m/s, écart maximal entre aperçu et vol réel (sans dispersion) : {worst:F1} m");
            if (expectZero)
            {
                Assert.True(worst < 0.01);
            }
            else
            {
                Assert.True(worst > 5.0);
            }
        }
    }
}
