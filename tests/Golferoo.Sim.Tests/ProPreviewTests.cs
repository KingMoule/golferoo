using Golferoo.Sim.Golfers;
using Golferoo.Sim.Pro;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Tests;

/// <summary>C25 lot 3 : XP de Jeu dans le vent et de Lecture du green, aperçus faussés, effets et avis de niveau.</summary>
public class ProPreviewTests
{
    private static readonly TerrainGrid Fairway = ShotTests.Uniform(Surface.Fairway, cells: 120);
    private static readonly Point2 Start = new(20, 240);
    private static readonly Vec3 Wind8 = new(0.0, 0.0, 8.0);

    private static ShotResult Stop(double x, double z) => new(new Point2(x, z), false, 0, ShotOutcome.Fairway, null);

    private static double Dist(Point2 a, Point2 b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));

    // ---- XP --------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Wind_FeedsOnlyFromThreeMetersPerSecond_ByTheClubQualityTimesTheWindShare()
    {
        var shot = new ProShot(Clubs.Iron7, new Point2(120, 240), ShotShape.Straight, 1.0);
        var aimed = Stop(120, 240);
        var played = Stop(120, 238);

        var calm = new ProSkills();
        calm.Record(Fairway, Start, shot, aimed, played, league: false, year: 1, windSpeed: 2.9);
        Assert.Equal(0, calm.Experience(ProSkill.Wind));

        var five = new ProSkills();
        five.Record(Fairway, Start, shot, aimed, played, league: false, year: 1, windSpeed: 5.0);
        var strong = new ProSkills();
        strong.Record(Fairway, Start, shot, aimed, played, league: false, year: 1, windSpeed: 15.0);

        var club = strong.Experience(ProSkill.MidIrons);
        Assert.Equal(club, strong.Experience(ProSkill.Wind)); // min(1, 15 / 10) = 1
        Assert.InRange(five.Experience(ProSkill.Wind), (club / 2.0) - 1, (club / 2.0) + 1);
    }

    [Fact]
    public void Wind_AMissedShot_GivesNothing_AndAPuttGivesNone()
    {
        var shot = new ProShot(Clubs.Iron7, new Point2(120, 240), ShotShape.Straight, 1.0);
        var p = new ProSkills();
        p.Record(Fairway, Start, shot, Stop(120, 240), Stop(120, 200), league: false, year: 1, windSpeed: 9.0);
        Assert.Equal(0, p.Experience(ProSkill.Wind));

        var putt = new ProSkills();
        putt.Record(Fairway, Start, new ProShot(Clubs.Putter, new Point2(30, 240), ShotShape.Straight, 1.0), Stop(30, 240), Stop(30, 240), league: false, year: 1, windSpeed: 9.0);
        Assert.Equal(0, putt.Experience(ProSkill.Wind));
    }

    [Fact]
    public void GreenReading_NeedsASlope_AndMeasuresTheLateralMissAtTheHole()
    {
        var target = new Point2(30, 240);
        var shot = new ProShot(Clubs.Putter, target, ShotShape.Straight, 1.0);
        var flat = ShotTests.Uniform(Surface.Green, cells: 120);
        var slope = ShotTests.Uniform(Surface.Green, (_, z) => 0.03 * z, cells: 120);

        var onFlat = new ProSkills();
        onFlat.Record(flat, Start, shot, Stop(30, 240), Stop(30, 239.5), league: false, year: 1);
        Assert.Equal(0, onFlat.Experience(ProSkill.GreenReading));

        // D = 10 : seuil max(0,5 m, 1 m) = 1 m ; écart latéral de 0,5 m : (1 - 0,5)^2 = 0,25.
        var onSlope = new ProSkills();
        onSlope.Record(slope, Start, shot, Stop(30, 240), Stop(30, 239.5), league: false, year: 1);
        Assert.InRange(onSlope.Experience(ProSkill.GreenReading), 249, 251);

        var holed = new ProSkills();
        holed.Record(slope, Start, shot, Stop(30, 240), new ShotResult(target, true, 0, ShotOutcome.Holed, null), league: false, year: 1);
        Assert.Equal(1000, holed.Experience(ProSkill.GreenReading));
    }

    // ---- Critère 8 : aperçus faussés ---------------------------------------------------------------------------------------------

    [Fact]
    public void WindPreview_DiffersAtLevel0_AndIsExactAtLevel50()
    {
        var shot = new ProShot(Clubs.Driver, new Point2(Start.X + 230, Start.Z), ShotShape.Straight, 1.0);
        var resolver = new PhysicalShotResolver { Wind = Wind8 };
        var worst = double.MaxValue;
        for (var tick = 0; tick < 20; tick++)
        {
            var p = new ProSkills();
            var truth = p.Execute(resolver, Fairway, Start, shot, null, new Point2(500, 500));
            var bias = p.BiasFor(tick * 7, tick % 5, Wind8);
            resolver.Wind = bias.Wind;
            var seen = p.Execute(resolver, Fairway, Start, shot, null, new Point2(500, 500), bias);
            resolver.Wind = Wind8;
            worst = Math.Min(worst, Dist(seen.Ball, truth.Ball));
        }

        Assert.True(worst > 4.0, $"le plus petit écart de l'aperçu au niveau 0 est {worst:F1} m");

        var master = new ProSkills();
        master.SetLevel(ProSkill.Wind, 50);
        var bias50 = master.BiasFor(123, 4, Wind8);
        Assert.Equal(Wind8, bias50.Wind);
        resolver.Wind = bias50.Wind;
        Assert.Equal(master.Execute(resolver, Fairway, Start, shot, null, new Point2(500, 500)).Ball, master.Execute(resolver, Fairway, Start, shot, null, new Point2(500, 500), bias50).Ball);
    }

    [Fact]
    public void Bias_ShrinksLinearlyWithTheLevel_AndIsDeterministicPerShot()
    {
        Assert.Equal(1.0, ProSkills.BiasAmplitude(0));
        Assert.Equal(0.5, ProSkills.BiasAmplitude(25), 12);
        Assert.Equal(0.0, ProSkills.BiasAmplitude(50));

        var p = new ProSkills();
        Assert.Equal(p.BiasFor(100, 3, Wind8), p.BiasFor(100, 3, Wind8));
        Assert.NotEqual(p.BiasFor(100, 3, Wind8), p.BiasFor(100, 4, Wind8));
        Assert.NotEqual(p.BiasFor(100, 3, Wind8), p.BiasFor(101, 3, Wind8));
    }

    [Fact]
    public void PuttPreview_ReadsAFalseSlopeAtLevel0_AndTheTrueOneAtLevel50()
    {
        var green = ShotTests.Uniform(Surface.Green, (x, z) => 0.04 * z, cells: 120);
        var shot = new ProShot(Clubs.Putter, new Point2(Start.X + 12, Start.Z), ShotShape.Straight, 1.0);
        var resolver = new PhysicalShotResolver();
        var pin = new Point2(500, 500);
        var novice = new ProSkills();
        var truth = novice.Execute(resolver, green, Start, shot, null, pin);
        var seen = novice.Execute(resolver, green, Start, shot, null, pin, novice.BiasFor(55, 2, default));
        Assert.True(Dist(seen.Ball, truth.Ball) > 0.3, $"{Dist(seen.Ball, truth.Ball):F2} m");

        var master = new ProSkills();
        master.SetLevel(ProSkill.GreenReading, 50);
        Assert.Equal(truth.Ball, master.Execute(resolver, green, Start, shot, null, pin, master.BiasFor(55, 2, default)).Ball);
    }

    [Fact]
    public void Preview_DoesNotConsumeTheWorldRng_NorChangeItsChecksum()
    {
        World Make()
        {
            var w = new World(5, HoleLoader.Load(Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "dogleg-par4.json"), 2), league: true) { ManualPro = true };
            w.StartProRound();
            while (w.AwaitingPro is null)
            {
                w.Step();
            }

            return w;
        }

        var withPreview = Make();
        var without = Make();
        var shot = new ProShot(Clubs.Driver, new Point2(250, 236), ShotShape.Straight, 1.0);
        var before = withPreview.Checksum();
        _ = withPreview.PreviewProShot(shot);
        _ = withPreview.PreviewProShot(shot);
        Assert.Equal(before, withPreview.Checksum());

        withPreview.PlayProShot(shot);
        without.PlayProShot(shot);
        for (var i = 0; i < 400; i++)
        {
            withPreview.Step();
            without.Step();
        }

        Assert.Equal(without.Checksum(), withPreview.Checksum());
    }

    // ---- Critère 9 : effets et avis ----------------------------------------------------------------------------------------------

    [Fact]
    public void Effects_AreZeroAtLevel0_AndGrowWithTheLevel()
    {
        foreach (var skill in Enum.GetValues<ProSkill>())
        {
            Assert.Equal(0.0, Math.Round(ProEffects.Of(skill, 0).Percent));
            Assert.True(Math.Abs(ProEffects.Of(skill, 20).Percent) > 1.0, skill.ToString());
            Assert.True(Math.Abs(ProEffects.Of(skill, 21).Percent) > Math.Abs(ProEffects.Of(skill, 20).Percent), skill.ToString());
        }

        Assert.Equal(-100.0, ProEffects.Of(ProSkill.Wind, 50).Percent, 9);
        Assert.Equal(-100.0, ProEffects.Of(ProSkill.GreenReading, 50).Percent, 9);
        Assert.True(ProEffects.Of(ProSkill.Distance, 50).Percent > 0.0);
    }

    [Fact]
    public void LevelMessage_NamesTheSkillTheLevelAndTheEffect()
    {
        var message = ProEffects.LevelMessage(ProSkill.LongIrons, 13);
        Assert.StartsWith("Fers longs : niveau 13, dispersion −", message);
        Assert.EndsWith(" %", message);
        Assert.Matches(@"^Chip : niveau 4, −\d+ % roulement$", ProEffects.LevelMessage(ProSkill.Chip, 4));
    }

    [Fact]
    public void SheetLine_ShowsTheCurrentEffectAndTheNextOne()
    {
        var line = ProEffects.Line(ProSkill.LongIrons, 12);
        Assert.StartsWith("niv. 12 : dispersion −", line);
        Assert.Contains(", niv. 13 : −", line);
        Assert.EndsWith("(maximum)", ProEffects.Line(ProSkill.LongIrons, 50));
        Assert.Matches(@"^niv\. 3 : −\d+ % roulement, niv\. 4 : −\d+ %$", ProEffects.Line(ProSkill.Chip, 3));
    }

    [Fact]
    public void TheSeventeenSkills_AreInFourGroups_EachWithAName()
    {
        var all = Enum.GetValues<ProSkill>();
        Assert.Equal(ProSkills.Count, all.Length);
        Assert.Equal(4, all.Select(ProEffects.GroupOf).Distinct().Count());
        Assert.Equal(all.Length, all.Select(ProEffects.Name).Distinct().Count());
    }
}
