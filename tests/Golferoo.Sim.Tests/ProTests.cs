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

    /// <summary>Le joueur le plus simple : droit sur l'angle du dogleg depuis le tee, puis sur le trou, avec le club dont la portée couvre la distance ; au green, le putter.</summary>
    private static ProShot Simple(World w, Golfer pro)
    {
        var t = w.Terrain!;
        if (t.SurfaceAt(pro.Ball.X, pro.Ball.Z) == Surface.Green)
        {
            return new ProShot(Clubs.Putter, t.Hole, ShotShape.Straight, 1.0);
        }

        var target = pro.Ball.X < 200 ? new Point2(250, 236) : t.Hole;
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
    public void ManualPro_PlaysARoundByCommands_AndGainsGeneralExperience()
    {
        var w = ManualWorld(5);
        var group = PlayRound(w);

        Assert.True(group.Finished);
        var pro = Assert.Single(group.Golfers);
        output.WriteLine($"{pro.Strokes} coups ; long jeu {w.Pro!.Experience(ProSkill.LongGame)}, fers {w.Pro.Experience(ProSkill.Irons)}, petit jeu {w.Pro.Experience(ProSkill.ShortGame)}, putting {w.Pro.Experience(ProSkill.Putting)}");
        Assert.True(w.Pro.Experience(ProSkill.LongGame) >= 1);
        Assert.True(w.Pro.Experience(ProSkill.Putting) >= 1);
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

    [Fact]
    public void League_CountsDoubleWithoutCap_TrainingStopsAtTheCapAndRestartsNextYear()
    {
        var p = new ProSkills();
        for (var i = 0; i < 100; i++)
        {
            p.Gain(ProSkill.Irons, league: false, year: 1);
            p.Gain(ProSkill.Putting, league: true, year: 1);
        }

        Assert.Equal(ProSkills.TrainingCapPerYear, p.Experience(ProSkill.Irons));
        Assert.Equal(200, p.Experience(ProSkill.Putting));
        p.Gain(ProSkill.Irons, league: false, year: 2);
        Assert.Equal(ProSkills.TrainingCapPerYear + 1, p.Experience(ProSkill.Irons));
    }

    [Fact]
    public void Handicap_StartsAt36_NeverBelow2_AFullSeasonBringsAWorkedSkillNear20()
    {
        var p = new ProSkills();
        Assert.Equal(36.0, p.Handicap(ProSkill.Draw));

        // Une saison : l'entraînement au plafond et un drive par journée de ligue (cinq journées et la finale).
        for (var i = 0; i < ProSkills.TrainingCapPerYear; i++)
        {
            p.Gain(ProSkill.LongGame, league: false, year: 1);
        }

        for (var i = 0; i < 6; i++)
        {
            p.Gain(ProSkill.LongGame, league: true, year: 1);
        }

        output.WriteLine($"long jeu après une saison : handicap {p.Handicap(ProSkill.LongGame):F1}");
        Assert.InRange(p.Handicap(ProSkill.LongGame), 18.0, 23.0);
        for (var i = 0; i < 5_000; i++)
        {
            p.Gain(ProSkill.Draw, league: true, year: 1);
        }

        Assert.InRange(p.Handicap(ProSkill.Draw), 2.0, 2.01);
    }

    [Fact]
    public void Record_AMissedShotFeedsOnlyItsGeneralSkill_ASuccessfulDrawAlsoDrawAndPrecision_NotFade()
    {
        var start = new Point2(20, 240);
        var target = new Point2(160, 240);
        var draw = new ProShot(Clubs.Iron7, target, ShotShape.Draw, 1.0);
        var p = new ProSkills();

        p.Record(Fairway, start, draw, new ShotResult(new Point2(140, 260), false, 0, ShotOutcome.Fairway, null), league: false, year: 1);
        Assert.Equal(1, p.Experience(ProSkill.Irons));
        Assert.Equal(0, p.Experience(ProSkill.Draw));

        p.Record(Fairway, start, draw, new ShotResult(new Point2(158, 243), false, 0, ShotOutcome.Fairway, null), league: true, year: 1);
        Assert.Equal(3, p.Experience(ProSkill.Irons));
        Assert.Equal(2, p.Experience(ProSkill.Draw));
        Assert.Equal(2, p.Experience(ProSkill.Precision));
        Assert.Equal(0, p.Experience(ProSkill.Fade));
        Assert.Equal(0, p.Experience(ProSkill.Distance));

        // Viser le rough n'est pas un lie voulu : même un coup au but ne compte que pour sa compétence générale.
        var rough = ShotTests.Uniform(Surface.Rough, cells: 120);
        p.Record(rough, start, draw, new ShotResult(new Point2(158, 243), false, 0, ShotOutcome.Rough, null), league: true, year: 1);
        Assert.Equal(5, p.Experience(ProSkill.Irons));
        Assert.Equal(2, p.Experience(ProSkill.Draw));
    }

    [Fact]
    public void Record_ALongSuccessfulDriveFeedsDistance()
    {
        var start = new Point2(20, 240);
        var target = new Point2(235, 240);
        var p = new ProSkills();
        p.Record(Fairway, start, new ProShot(Clubs.Driver, target, ShotShape.Straight, 1.0), new ShotResult(new Point2(232, 238), false, 0, ShotOutcome.Fairway, null), league: false, year: 1);
        Assert.Equal(1, p.Experience(ProSkill.Distance));
        Assert.Equal(1, p.Experience(ProSkill.LongGame));
    }

    [Fact]
    public void Experience_TightensDispersion()
    {
        var p = new ProSkills();
        var before = p.Dispersion(Clubs.Iron7);
        for (var i = 0; i < 200; i++)
        {
            p.Gain(ProSkill.Irons, league: true, year: 1);
        }

        Assert.True(p.Dispersion(Clubs.Iron7) < before * 0.7);
        Assert.Equal(before, p.Dispersion(Clubs.Driver));
    }
}
