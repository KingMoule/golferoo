using Golferoo.Sim.Golfers;
using Golferoo.Sim.Pro;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Xunit.Abstractions;

namespace Golferoo.Sim.Tests;

/// <summary>C08 lot 3 : compétences et expérience de votre pro, coups joués par le joueur.</summary>
public class ProTests(ITestOutputHelper output)
{
    private static TerrainGrid Dogleg() =>
        HoleLoader.Load(Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "dogleg-par4.json"), 2);

    private static readonly TerrainGrid Fairway = ShotTests.Uniform(Surface.Fairway, cells: 120);

    private static World ManualWorld(ulong seed) => new(seed, Dogleg(), league: true) { ManualPro = true };

    /// <summary>Le joueur le plus simple : droit sur l'angle du dogleg jusqu'à 30 m de lui (des arbres deux fois plus grands bouchent le raccourci), puis sur le trou, avec le club dont la portée couvre la distance ; au green, le putter.</summary>
    private static ProShot Simple(World w, Golfer pro)
    {
        var t = w.Terrain!;
        if (t.SurfaceAt(pro.Ball.X, pro.Ball.Z) == Surface.Green)
        {
            return new ProShot(Clubs.Putter, t.Hole, ShotShape.Straight, 1.0);
        }

        var corner = new Point2(250, 236);
        var target = Math.Abs(corner.X - pro.Ball.X) + Math.Abs(corner.Z - pro.Ball.Z) > 30 && pro.Ball.X < corner.X ? corner : t.Hole;
        var d = Math.Sqrt(Math.Pow(target.X - pro.Ball.X, 2) + Math.Pow(target.Z - pro.Ball.Z, 2));
        var club = Clubs.Full.LastOrDefault(c => c.TargetRange >= d) ?? Clubs.Driver;
        return new ProShot(club, target, ShotShape.Straight, Math.Min(1.0, Math.Sqrt(d / club.TargetRange)));
    }

    private static Group PlayRound(World w)
    {
        var group = w.StartProRound();
        for (var i = 0; i < 60_000 && !group.Finished; i++)
        {
            if (w.AwaitingPro is { } pro)
            {
                w.PlayProShot(Simple(w, pro));
            }

            w.Step();
        }

        return group;
    }

    [Fact]
    public void ManualPro_WaitsForThePlayer_NeverPlaysAlone()
    {
        var w = ManualWorld(4);
        w.StartProRound();
        for (var i = 0; i < 5_000 && w.AwaitingPro is null; i++)
        {
            w.Step();
        }

        var pro = Assert.IsType<Golfer>(w.AwaitingPro);
        w.Step(2_000);
        Assert.Same(pro, w.AwaitingPro);
        Assert.Equal(0, pro.Strokes);
    }

    [Fact]
    public void ManualPro_PlaysARoundByCommands_AndGainsClubExperience()
    {
        var w = ManualWorld(5);
        var group = PlayRound(w);

        Assert.True(group.Finished);
        var pro = Assert.Single(group.Golfers);
        output.WriteLine($"{pro.Strokes} coups ; driver {w.Pro!.Experience(ProSkill.Driver)}, fer 7 {w.Pro.Experience(ProSkill.MidIrons)}, wedge {w.Pro.Experience(ProSkill.Wedge)}, putter {w.Pro.Experience(ProSkill.Putter)} (centièmes)");
        Assert.True(w.Pro.Experience(ProSkill.Driver) > 0);
        Assert.True(w.Pro.Experience(ProSkill.Putter) > 0);
    }

    [Fact]
    public void ManualPro_NeverPuttsFromInsideTwoMeters_TheGimmeHolesTheBall()
    {
        // Graine 1 : le joueur simple finit le trou (C23 : avec le geste de 23 ticks, la graine 7 le menait au ramassage).
        var w = ManualWorld(1);
        var group = w.StartProRound();
        for (var i = 0; i < 60_000 && !group.Finished; i++)
        {
            if (w.AwaitingPro is { } pro)
            {
                var d = Math.Sqrt(Math.Pow(w.Terrain!.Hole.X - pro.Ball.X, 2) + Math.Pow(w.Terrain.Hole.Z - pro.Ball.Z, 2));
                Assert.True(w.Terrain.SurfaceAt(pro.Ball.X, pro.Ball.Z) != Surface.Green || d >= World.GimmeDistance, $"putt à {d:F2} m");
                w.PlayProShot(Simple(w, pro));
            }

            w.Step();
        }

        Assert.True(Assert.Single(group.Golfers).Holed);
    }

    [Fact]
    public void PlayProShot_IsPendingUntilTheNextTick()
    {
        var w = ManualWorld(9);
        w.StartProRound();
        while (w.AwaitingPro is null)
        {
            w.Step();
        }

        w.PlayProShot(Simple(w, w.AwaitingPro!));
        Assert.True(w.ProShotPending);
        Assert.NotNull(w.AwaitingPro);
        w.Step();
        Assert.False(w.ProShotPending);
        Assert.Null(w.AwaitingPro);
    }

    [Fact]
    public void SameCommands_SameChecksum()
    {
        var a = ManualWorld(6);
        var b = ManualWorld(6);
        PlayRound(a);
        PlayRound(b);
        Assert.Equal(a.Checksum(), b.Checksum());
        Assert.Equal(a.Tick, b.Tick);
    }
}
