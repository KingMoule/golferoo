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

    /// <summary>C28 : le curseur de visée est ramené à la portée réelle du pro, et non à <c>TargetRange</c> (le niveau 0 n'en atteint que 74 à 85 %).</summary>
    [Fact]
    public void AimPoint_BeyondTheRealReach_IsPulledBackToIt_AndTheBallLandsThereWithinTwoPercent()
    {
        var pro = new ProSkills();
        var resolver = new PhysicalShotResolver();
        foreach (var club in Clubs.Full)
        {
            var click = new Point2(Start.X + (club.TargetRange * 1.5), Start.Z);
            var aim = pro.AimPoint(resolver, Fairway, Start, club, ShotShape.Straight, click, Pin);
            var force = pro.ForceFor(resolver, Fairway, Start, club, ShotShape.Straight, aim, Pin);
            var landed = pro.Execute(resolver, Fairway, Start, new ProShot(club, aim, ShotShape.Straight, force), null, Pin).Ball;
            output.WriteLine($"{club.Name} : portée visée {club.TargetRange:F0} m, portée réelle {Dist(Start, aim):F1} m, force {force:F3}, balle à {Dist(Start, landed):F1} m");
            Assert.True(Dist(Start, aim) < club.TargetRange);
            Assert.InRange(Dist(Start, landed), Dist(Start, aim) * 0.98, Dist(Start, aim) * 1.02);
            var near = new Point2(Start.X + 10, Start.Z);
            Assert.Equal(near, pro.AimPoint(resolver, Fairway, Start, club, ShotShape.Straight, near, Pin));
        }
    }

    [Fact]
    public void Spread_GrowsWithTheClickedDistance_UpToTheRealReach()
    {
        var pro = new ProSkills();
        var resolver = new PhysicalShotResolver();
        foreach (var club in Clubs.Full)
        {
            var reach = Dist(Start, pro.AimPoint(resolver, Fairway, Start, club, ShotShape.Straight, new Point2(Start.X + club.TargetRange, Start.Z), Pin));
            var (previousAcross, previousAlong) = (0.0, 0.0);
            for (var d = 1.0; d <= reach; d += 1.0)
            {
                var target = new Point2(Start.X + d, Start.Z);
                var force = pro.ForceFor(resolver, Fairway, Start, club, ShotShape.Straight, target, Pin);
                var (across, along) = pro.Spread(club, ShotShape.Straight, d, force);
                Assert.True(across >= previousAcross && along >= previousAlong, $"{club.Name} à {d} m : dispersion {across:F2}/{along:F2} après {previousAcross:F2}/{previousAlong:F2}");
                (previousAcross, previousAlong) = (across, along);
            }
        }
    }

    [Fact]
    public void AimJson_IsReadStrictly_UnknownKeyAndMissingFieldAreRefused()
    {
        Assert.Equal(0.2, ProAimParams.Parse("""{ "aimFloor": 0.2, "curveScale": 2, "walkFactor": 1.2 }""").AimFloor);
        Assert.Throws<System.Text.Json.JsonException>(() => ProAimParams.Parse("""{ "aimFloor": 0.2, "curveScale": 2, "walkFactor": 1.2, "inconnu": 1 }"""));
        Assert.Throws<System.Text.Json.JsonException>(() => ProAimParams.Parse("""{ "aimFloor": 0.2 }"""));
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

    // ---- Putt automatique (C28) ----------------------------------------------------------------------------------------------

    /// <summary>Part de putts rentrés à <paramref name="distance"/> mètres autour du trou, sur les neuf greens de « Le neuf » (pentes comprises), par le putt du résolveur que joue le pro (C28).</summary>
    private static double MakeRate(int level, double distance)
    {
        var course = CourseLoader.Load(Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "..", "courses", "le-neuf.json"), 2);
        var skill = ProSkills.Factor(level);
        var resolver = new PhysicalShotResolver();
        var rng = new DeterministicRng(7);
        var (made, total) = (0, 0);
        for (var hole = 1; hole <= course.Course.Count; hole++)
        {
            var pin = course.Course[hole].Hole;
            for (var angle = 0; angle < 360; angle += 45)
            {
                var ball = new Point2(pin.X + (distance * Math.Cos(angle * Math.PI / 180.0)), pin.Z + (distance * Math.Sin(angle * Math.PI / 180.0)));
                if (course.SurfaceAt(ball.X, ball.Z) != Surface.Green)
                {
                    continue;
                }

                for (var k = 0; k < 4; k++)
                {
                    made += resolver.Resolve(new ShotRequest(ball, pin, 1, skill), course, rng).Holed ? 1 : 0;
                    total++;
                }
            }
        }

        Assert.True(total >= 100, $"trop peu de positions sur le green : {total}");
        return (double)made / total;
    }

    [Fact]
    public void AutoPutt_OnLeNeuf_HolesMoreWithBetterSkill_AndMostPuttsFromThreeMetersAtMaxLevel()
    {
        var (rookie, mid, master) = (MakeRate(0, 3.0), MakeRate(25, 3.0), MakeRate(50, 3.0));
        output.WriteLine($"3 m, Le neuf : niveau 0 {rookie:P0}, niveau 25 {mid:P0}, niveau 50 {master:P0}");
        Assert.True(rookie < mid && mid < master);
        Assert.True(rookie >= 0.30);
        Assert.True(master >= 0.70);
    }

    /// <summary>NB1 (audit C28) : à la force pleine, le facteur de force passe du « dosé » au « non dosé » ; si Dosage est moins bon que le club, la dispersion en longueur saute vers le bas au seuil. Le test en fixe l'ampleur.</summary>
    [Fact]
    public void Spread_AtTheFullForceThreshold_JumpsByABoundedShare_WhenDosageDiffersFromTheClub()
    {
        var pro = new ProSkills();
        pro.SetLevel(ProSkill.Wedge, 40);
        pro.SetLevel(ProSkill.Dosage, 0);
        var club = Clubs.Wedge;
        var below = pro.Spread(club, ShotShape.Straight, 50.0, 0.89).Along;
        var full = pro.Spread(club, ShotShape.Straight, 50.0, 1.0).Along;
        output.WriteLine($"Dosage 0, wedge 40 : longueur {below:F2} m à 89 % de force, {full:F2} m à pleine force (saut {1.0 - (full / below):P0})");
        Assert.True(full <= below);
        Assert.True(full > below * 0.4);
    }

    // ---- Courbure --------------------------------------------------------------------------------------------------------------

    /// <summary>Courbure d'un draw plein du pro (ses vrais facteurs) visé à 220 m : écart latéral de la balle à l'arrêt.</summary>
    private static double Curve(Club club)
    {
        var target = new Point2(Start.X + 220, Start.Z);
        var end = new ProSkills().Execute(new PhysicalShotResolver(), Fairway, Start, new ProShot(club, target, ShotShape.Draw, 1.0), null, Pin).Ball;
        return Start.Z - end.Z;
    }

    [Fact]
    public void ProCurve_DriverDrawBendsFortyToFiftyMeters_AndAnIron7TwentyToTwentyEight()
    {
        var scale = ProAimParams.Default.CurveScale;
        var driver = Curve(Clubs.Driver);
        var iron = Curve(Clubs.Iron7);
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
