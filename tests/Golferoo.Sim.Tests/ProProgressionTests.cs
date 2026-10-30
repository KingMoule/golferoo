using Golferoo.Sim.Golfers;
using Golferoo.Sim.Pro;
using Golferoo.Sim.Rating;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Xunit.Abstractions;

namespace Golferoo.Sim.Tests;

/// <summary>C25 lot 1 : qualité des coups, XP par habileté, niveaux et effets du pro.</summary>
public class ProProgressionTests(ITestOutputHelper output)
{
    private static readonly TerrainGrid Fairway = ShotTests.Uniform(Surface.Fairway, cells: 120);
    private static readonly Point2 Start = new(20, 240);

    private static ShotResult Stop(double x, double z, int penalty = 0) => new(new Point2(x, z), false, penalty, ShotOutcome.Fairway, null);

    private static long Xp(ProSkills p, ProSkill s) => p.Experience(s);

    // ---- Critère 1 -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void LongIronDraw_FullForce_ThreeMetersOff_FeedsFourSkillsByTheirOwnQuality()
    {
        // Fer 5 à pleine force, point visé à 170 m ; le coup prévu s'arrête à (190, 236) (courbe de 4 m), le coup joué à 3 m de là.
        var shot = new ProShot(Clubs.Iron5, new Point2(190, 240), ShotShape.Draw, 1.0);
        var aimed = Stop(190, 236);
        var played = Stop(190, 233);
        var p = new ProSkills();
        p.Record(Fairway, Start, shot, aimed, played, league: false, year: 1);

        // À la main : D = |(170, −4)| = 170,047 ; écart total ≈ 3,00 ; latéral 3,00.
        const double D = 170.047;
        double Q(double dev, double thr) => Math.Pow(1.0 - (dev / thr), 2.0);
        Assert.InRange(Xp(p, ProSkill.LongIrons), (1000 * Q(3.0008, 0.15 * D)) - 1, (1000 * Q(3.0008, 0.15 * D)) + 1); // 10 × 0,7... en centièmes
        Assert.InRange(Xp(p, ProSkill.Precision), (1000 * Q(3.0, 0.06 * D)) - 1, (1000 * Q(3.0, 0.06 * D)) + 1);
        Assert.Equal(1000, Xp(p, ProSkill.Distance)); // 170,1 m ≥ 100 % de la portée
        Assert.InRange(Xp(p, ProSkill.Draw), (1000 * Q(3.0, 4.0)) - 1, (1000 * Q(3.0, 4.0)) + 1); // courbe voulue 4 m, obtenue 7 m
        foreach (var other in Enum.GetValues<ProSkill>().Where(s => s is not (ProSkill.LongIrons or ProSkill.Precision or ProSkill.Distance or ProSkill.Draw)))
        {
            Assert.Equal(0, Xp(p, other));
        }
    }

    // ---- Critère 2 -------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(0.5, 0.25)]
    [InlineData(1.0, 0.0)]
    [InlineData(2.0, 0.0)]
    public void Quality_FollowsOneMinusDeviationOverThresholdSquared(double part, double expected) =>
        Assert.Equal(expected, ProQuality.Quality(part * 10.0, 10.0), 9);

    [Fact]
    public void EachSkillQuality_IsZeroAtItsThreshold_AndAQuarterAtHalfOfIt()
    {
        var target = new Point2(120, 240); // D = 100
        var straight = new ProShot(Clubs.Iron7, target, ShotShape.Straight, 1.0);
        var aimed = Stop(120, 240);

        // Précision : seuil latéral 6 % de D = 6 m.
        var half = new ProSkills();
        half.Record(Fairway, Start, straight, aimed, Stop(120, 243), false, 1);
        Assert.Equal(250, Xp(half, ProSkill.Precision));
        var edge = new ProSkills();
        edge.Record(Fairway, Start, straight, aimed, Stop(120, 246), false, 1);
        Assert.Equal(0, Xp(edge, ProSkill.Precision));

        // Club : seuil total 15 % de D = 15 m.
        var club = new ProSkills();
        club.Record(Fairway, Start, straight, aimed, Stop(120, 247.5), false, 1);
        Assert.Equal(250, Xp(club, ProSkill.MidIrons));

        // Dosage (force partielle) : seuil en profondeur 8 % de D = 8 m.
        var partial = new ProShot(Clubs.Iron7, target, ShotShape.Straight, 0.7);
        var dosage = new ProSkills();
        dosage.Record(Fairway, Start, partial, aimed, Stop(124, 240), false, 1);
        Assert.Equal(250, Xp(dosage, ProSkill.Dosage));
        Assert.Equal(0, Xp(dosage, ProSkill.Distance));
    }

    [Fact]
    public void CompletelyMissedShot_GivesHalfAnXpToTheClub_ZeroToTheOthers_NeverNegative()
    {
        var shot = new ProShot(Clubs.Iron5, new Point2(190, 240), ShotShape.Draw, 1.0);
        var p = new ProSkills();
        p.Record(Fairway, Start, shot, Stop(190, 236), Stop(60, 300), league: false, year: 1);
        Assert.Equal(50, Xp(p, ProSkill.LongIrons));
        foreach (var s in Enum.GetValues<ProSkill>().Where(s => s != ProSkill.LongIrons))
        {
            Assert.Equal(0, Xp(p, s));
        }

        // Et en pénalité, partout à zéro sauf le plancher du club.
        var rng = new DeterministicRng(5);
        var q = new ProSkills();
        for (var i = 0; i < 500; i++)
        {
            var club = Clubs.Full[i % Clubs.Full.Count];
            var shape = (ShotShape)(i % 4);
            var res = Stop(20 + (rng.NextDouble() * 300), 100 + (rng.NextDouble() * 300), i % 7 == 0 ? 1 : 0);
            var before = Enum.GetValues<ProSkill>().Select(s => Xp(q, s)).ToArray();
            q.Record(Fairway, Start, new ProShot(club, new Point2(20 + club.TargetRange, 240), shape, i % 3 == 0 ? 1.0 : 0.6), Stop(20 + club.TargetRange, 240), res, i % 2 == 0, 1);
            Assert.All(Enum.GetValues<ProSkill>().Select(s => Xp(q, s)).Zip(before), t => Assert.True(t.First >= t.Second));
        }
    }

    // ---- Critère 3 -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void League_CountsDoubleWithoutCap_TrainingStopsAtTheYearCap_AndRestartsNextYear()
    {
        var p = new ProSkills();
        for (var i = 0; i < 100; i++)
        {
            p.Gain(ProSkill.MidIrons, 1000, league: false, year: 1);
            p.Gain(ProSkill.Putter, 1000, league: true, year: 1);
        }

        Assert.Equal(p.TrainingCap, Xp(p, ProSkill.MidIrons));
        Assert.Equal(200_000, Xp(p, ProSkill.Putter));
        p.Gain(ProSkill.MidIrons, 1000, league: false, year: 2);
        Assert.Equal(p.TrainingCap + 1000, Xp(p, ProSkill.MidIrons));
    }

    [Fact]
    public void AGimme_HolesTheBall_AndGivesNoExperience()
    {
        var w = new World(1, HoleLoader.Load(Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "dogleg-par4.json"), 2), league: true) { ManualPro = true };
        w.StartProRound();
        while (w.AwaitingPro is null)
        {
            w.Step();
        }

        var pro = w.AwaitingPro!;
        w.PlayProShot(new ProShot(Clubs.Driver, new Point2(250, 236), ShotShape.Straight, 1.0));
        for (var i = 0; i < 5_000 && pro.State != GolferState.WalkingToBall; i++)
        {
            w.Step();
        }

        Assert.Equal(GolferState.WalkingToBall, pro.State);

        // La balle (vue par le monde à l'arrêt) est à 1 m du trou sur le green : à son tour, le putt est donné.
        pro.Ball = new Point2(w.Terrain!.Hole.X - 1.0, w.Terrain.Hole.Z);
        Assert.Equal(Surface.Green, w.Terrain.SurfaceAt(pro.Ball.X, pro.Ball.Z));
        var before = Enum.GetValues<ProSkill>().Select(sk => w.Pro!.Experience(sk)).ToArray();
        var strokes = pro.Strokes;
        for (var i = 0; i < 5_000 && !pro.Holed; i++)
        {
            w.Step();
        }

        Assert.True(pro.Holed);
        Assert.Equal(strokes + 1, pro.Strokes);
        Assert.Equal(before, Enum.GetValues<ProSkill>().Select(sk => w.Pro!.Experience(sk)).ToArray());
    }

    [Fact]
    public void TrainingYear_ChangesOnApril1st_NotOnJanuary1st()
    {
        var w = new World(3);
        var p = new ProSkills();
        var years = new List<(int Month, int Year)>();
        for (var i = 0; i < 25; i++)
        {
            years.Add((w.Date.Month, w.PlayYear));
            p.Gain(ProSkill.MidIrons, p.TrainingCap, league: false, year: w.PlayYear);
            w.SkipToNextMonth();
        }

        output.WriteLine(string.Join(" ", years));
        for (var i = 1; i < years.Count; i++)
        {
            Assert.Equal(years[i].Month == 4 ? years[i - 1].Year + 1 : years[i - 1].Year, years[i].Year);
        }

        // Un gain par mois, plafond plein : seul un mois d'avril ajoute l'année suivante (12 mois = 2 plafonds sur 25 mois, soit 3 plafonds).
        Assert.Equal(3 * p.TrainingCap, p.Experience(ProSkill.MidIrons));
    }

    [Fact]
    public void Dosage_ShrinksTheDepthErrorOfAPutt()
    {
        var green = ShotTests.Uniform(Surface.Green, cells: 120);
        var resolver = new PhysicalShotResolver();
        var shot = new ProShot(Clubs.Putter, new Point2(30, 240), ShotShape.Straight, 1.0);
        double Depth(int dosage)
        {
            var p = AtLevel(25);
            p.SetLevel(ProSkill.Dosage, dosage);
            var aimed = p.Execute(resolver, green, Start, shot, null, new Point2(500, 500));
            var rng = new DeterministicRng(4);
            var sum = 0.0;
            for (var i = 0; i < 1000; i++)
            {
                sum += Math.Abs(p.Execute(resolver, green, Start, shot, rng, new Point2(500, 500)).Ball.X - aimed.Ball.X);
            }

            return sum / 1000;
        }

        var (low, high) = (Depth(0), Depth(50));
        output.WriteLine($"écart en profondeur à 10 m : Dosage 0 : {low:F3} m ; Dosage 50 : {high:F3} m");
        Assert.True(high < low * 0.85);
    }

    [Fact]
    public void ATinyAim_FeedsNeitherPrecisionNorDosage_ButTheClubKeepsItsFloor()
    {
        var shot = new ProShot(Clubs.Wedge, new Point2(20.3, 240), ShotShape.Straight, 0.5);
        var p = new ProSkills();
        p.Record(Fairway, Start, shot, Stop(20.3, 240), Stop(20.3, 240.2), league: false, year: 1);
        Assert.Equal(0, Xp(p, ProSkill.Precision));
        Assert.Equal(0, Xp(p, ProSkill.Dosage));
        Assert.True(Xp(p, ProSkill.Wedge) >= 50);
    }

    [Fact]
    public void APenaltyOnThePlannedShot_MeasuresFromTheClickedTarget_APenaltyOnThePlayedShot_FeedsOnlyTheClub()
    {
        var shot = new ProShot(Clubs.Iron7, new Point2(160, 240), ShotShape.Straight, 1.0);
        var p = new ProSkills();
        p.Record(Fairway, Start, shot, Stop(60, 300, penalty: 1), Stop(160, 240), league: false, year: 1);
        Assert.Equal(1000, Xp(p, ProSkill.Precision));
        Assert.Equal(1000, Xp(p, ProSkill.MidIrons));

        var q = new ProSkills();
        q.Record(Fairway, Start, shot, Stop(160, 240), Stop(160, 240, penalty: 1), league: false, year: 1);
        Assert.Equal(0, Xp(q, ProSkill.Precision));
        Assert.Equal(50, Xp(q, ProSkill.MidIrons));
    }

    // ---- Critère 4 -------------------------------------------------------------------------------------------------------------

    private static ProSkills DrawSeason(ulong seed)
    {
        var rng = new DeterministicRng(seed);
        var p = new ProSkills();
        for (var i = 0; i < 300; i++)
        {
            var shot = new ProShot(Clubs.Iron7, new Point2(160, 240), ShotShape.Draw, 1.0);
            var miss = (rng.NextDouble() - 0.5) * 24.0;
            p.Record(Fairway, Start, shot, Stop(158, 234), Stop(158 + (miss / 3.0), 234 + miss), league: i % 5 == 0, year: 1);
        }

        return p;
    }

    [Fact]
    public void ASeasonOfDraws_ProgressesDraw_NotFade_AndIsReproducible()
    {
        var a = DrawSeason(11);
        var b = DrawSeason(11);
        Assert.True(Xp(a, ProSkill.Draw) > 0);
        Assert.Equal(0, Xp(a, ProSkill.Fade));
        for (var i = 0; i < ProSkills.Count; i++)
        {
            Assert.Equal(Xp(a, (ProSkill)i), Xp(b, (ProSkill)i));
        }

        Assert.NotEqual(Xp(a, ProSkill.Draw), Xp(DrawSeason(12), ProSkill.Draw));
    }

    // ---- Niveaux et coût -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Cost_DoublesEverySevenLevels_AndLevel43HoldsHalfOfLevel50()
    {
        var p = ProgressionParams.Default;
        for (var n = 42; n + 7 < ProgressionParams.MaxLevel; n++)
        {
            var ratio = (double)p.CostHundredths(n + 7) / p.CostHundredths(n);
            Assert.InRange(ratio, 1.98, 2.02);
        }

        var skills = new ProSkills();
        var share = (double)skills.XpForLevel(43) / skills.XpForLevel(50);
        output.WriteLine($"niveau 43 : {share:P1} de l'XP du niveau 50 ; XP du niveau 50 : {skills.XpForLevel(50) / 100.0:F0}");
        Assert.InRange(share, 0.48, 0.52);
    }

    [Fact]
    public void Level_FollowsTheCumulativeCost_AndStopsAt50()
    {
        var p = new ProSkills();
        Assert.Equal(0, p.Level(ProSkill.Draw));
        p.Gain(ProSkill.Draw, p.XpForLevel(13) / 2, league: true, year: 1); // la ligue double
        Assert.Equal(13, p.Level(ProSkill.Draw));
        Assert.InRange(p.LevelProgress(ProSkill.Draw), 0.0, 0.01);
        var q = new ProSkills();
        q.SetLevel(ProSkill.Draw, 50);
        q.Gain(ProSkill.Draw, 1_000_000, league: true, year: 1);
        Assert.Equal(50, q.Level(ProSkill.Draw));
        Assert.Equal(1.0, q.LevelProgress(ProSkill.Draw));
        Assert.Equal(-4.0, q.Handicap(ProSkill.Draw));
        Assert.Equal(36.0, new ProSkills().Handicap(ProSkill.Fade));
    }

    [Fact]
    public void FromHandicap_UnchangedForOrdinaryGolfers_AndReachesPlusFour()
    {
        for (var h = 0.0; h <= 54.0; h += 1.5)
        {
            Assert.Equal(1.0 + (1.1 * h / 20.0), SkillLevels.FromHandicap(h), 12);
        }

        Assert.Equal(SkillLevels.PlusFour, SkillLevels.FromHandicap(-4.0), 12);
        Assert.Equal(SkillLevels.Scratch, SkillLevels.FromHandicap(0.0), 12);
        Assert.True(SkillLevels.FromHandicap(-2.0) < SkillLevels.Scratch && SkillLevels.FromHandicap(-2.0) > SkillLevels.PlusFour);
        Assert.Equal(SkillLevels.FromHandicap(-4.0), SkillLevels.FromHandicap(-9.0), 12);
        Assert.Equal(0.0, SkillLevels.ToHandicap(SkillLevels.FromHandicap(-4.0)), 12);
    }

    /// <summary>Mesure provisoire du critère 7 (le calibrage est au lot 4) : une saison de 600 XP sur une habileté travaillée.</summary>
    [Fact]
    public void ProvisionalSeason_OfSixHundredXp_ReachesAboutLevel25_AndFiftyTakesTenSeasonsOrMore()
    {
        var p = new ProSkills();
        p.Gain(ProSkill.MidIrons, 30_000, league: false, year: 1); // plafond d'entraînement : 300 XP
        p.Gain(ProSkill.MidIrons, 15_000, league: true, year: 1); // 150 XP de ligue, comptés double
        output.WriteLine($"après une saison de {Xp(p, ProSkill.MidIrons) / 100.0:F0} XP : niveau {p.Level(ProSkill.MidIrons)} ; niveau 50 à {p.XpForLevel(50) / 100.0 / (Xp(p, ProSkill.MidIrons) / 100.0):F1} saisons");
        Assert.InRange(p.Level(ProSkill.MidIrons), 20, 30);
        Assert.True(p.XpForLevel(50) >= 10 * Xp(p, ProSkill.MidIrons));
    }

    // ---- Critère 5 -------------------------------------------------------------------------------------------------------------

    private static ProSkills AtLevel(int level)
    {
        var p = new ProSkills();
        for (var i = 0; i < ProSkills.Count; i++)
        {
            p.SetLevel((ProSkill)i, level);
        }

        return p;
    }

    [Fact]
    public void EachLevelOfAClubSkill_ShrinksTheMeasuredDispersionByTheSameStep()
    {
        var resolver = new PhysicalShotResolver();
        var shot = new ProShot(Clubs.Iron7, new Point2(160, 240), ShotShape.Straight, 1.0);
        double Mean(int level)
        {
            var p = AtLevel(level);
            var aimed = p.Execute(resolver, Fairway, Start, shot, null, new Point2(500, 500));
            var rng = new DeterministicRng(99);
            var sum = 0.0;
            const int N = 2000;
            for (var i = 0; i < N; i++)
            {
                var r = p.Execute(resolver, Fairway, Start, shot, rng, new Point2(500, 500));
                sum += Math.Sqrt(Math.Pow(r.Ball.X - aimed.Ball.X, 2) + Math.Pow(r.Ball.Z - aimed.Ball.Z, 2));
            }

            return sum / N;
        }

        var levels = new[] { 0, 10, 20, 30, 40, 50 };
        var means = levels.Select(Mean).ToArray();
        output.WriteLine(string.Join(" ; ", levels.Zip(means, (l, m) => $"niv. {l} : {m:F2} m ({100 * (1 - (m / means[0])):F1} %)")));
        var expected = 100.0 * (1.0 - (SkillLevels.FromHandicap(36.0 - (0.8 * 10)) / SkillLevels.FromHandicap(36.0))) / 10.0; // pas par niveau, en points
        for (var i = 1; i < levels.Length; i++)
        {
            var step = 100.0 * (means[i - 1] - means[i]) / means[0] / 10.0;
            Assert.InRange(step, expected - 1.0, expected + 1.0);
        }
    }

    /// <summary>Décision 11 : Chip réduit l'erreur sur l'abaissement (donc sur le roulement) et resserre la dispersion des chips ; sans effet sur un coup droit.</summary>
    [Fact]
    public void ChipLevel50_RollsCloserToTheWantedRoll_AndSpreadsLess_ThanLevel0()
    {
        var resolver = new PhysicalShotResolver();
        var chip = new ProShot(Clubs.Wedge, new Point2(60, 240), ShotShape.Chip, 0.6);
        (double Roll, double Lateral) Measure(int level)
        {
            var p = new ProSkills();
            p.SetLevel(ProSkill.Chip, level);
            var aimed = p.Execute(resolver, Fairway, Start, chip, null, new Point2(500, 500));
            var rng = new DeterministicRng(2024);
            double roll = 0.0, lateral = 0.0;
            const int N = 3000;
            for (var i = 0; i < N; i++)
            {
                var r = p.Execute(resolver, Fairway, Start, chip, rng, new Point2(500, 500));
                roll += Math.Abs(r.Ball.X - aimed.Ball.X);
                lateral += Math.Abs(r.Ball.Z - aimed.Ball.Z);
            }

            return (roll / N, lateral / N);
        }

        var (zeroRoll, zeroLateral) = Measure(0);
        var (fiftyRoll, fiftyLateral) = Measure(50);
        output.WriteLine($"chip : écart de roulement {zeroRoll:F2} m -> {fiftyRoll:F2} m ; latéral {zeroLateral:F2} m -> {fiftyLateral:F2} m");
        Assert.True(fiftyRoll < 0.8 * zeroRoll);
        Assert.True(fiftyLateral < 0.8 * zeroLateral);

        // Un coup droit ne change pas avec Chip.
        var straight = new ProShot(Clubs.Wedge, new Point2(60, 240), ShotShape.Straight, 0.6);
        var a = new ProSkills();
        var b = new ProSkills();
        b.SetLevel(ProSkill.Chip, 50);
        Assert.Equal(
            a.Execute(resolver, Fairway, Start, straight, new DeterministicRng(5), new Point2(500, 500)).Ball,
            b.Execute(resolver, Fairway, Start, straight, new DeterministicRng(5), new Point2(500, 500)).Ball);
    }

    private const int Rounds = 160;

    /// <summary>
    /// Écart moyen (carte de 9 trous sur « Le neuf », différentiel C06) d'un golfeur ordinaire de niveau <paramref name="ordinary"/>
    /// ou du pro <paramref name="pro"/> : le coup est planifié comme celui d'un golfeur de même niveau, puis joué avec les effets du pro.
    /// Sur le green, la lecture de pente reste celle du golfeur (lot 3).
    /// </summary>
    private double Differential(double? ordinary, ProSkills? pro, CourseRating rating, TerrainGrid terrain, World world)
    {
        var skill = ordinary ?? SkillLevels.FromHandicap(ProSkills.HandicapOf(pro!.Level(ProSkill.MidIrons)));
        var gross = new int[Rounds];
        Parallel.For(
            0,
            Rounds,
            () => new PhysicalShotResolver { Trees = world.Trees, Season = world.Season, MapCapacity = World.MapCapacityFor(terrain) },
            (n, _, resolver) =>
            {
                var plan = new DeterministicRng(0xA11CE + (ulong)n);
                var exec = new DeterministicRng(0xB0B + (ulong)n);
                foreach (var hole in terrain.Course.Holes)
                {
                    var ball = hole.Tee;
                    var strokes = 0;
                    while (strokes < World.MaxStrokes)
                    {
                        var request = new ShotRequest(ball, hole.Hole, strokes, skill);
                        ShotResult r;
                        if (pro is null || terrain.SurfaceAt(ball.X, ball.Z) == Surface.Green)
                        {
                            r = resolver.Resolve(request, terrain, plan);
                        }
                        else
                        {
                            resolver.Resolve(request, terrain, plan, out var chosen);
                            var c = chosen!.Value;
                            var shape = c.Low && c.Shape == ShotShape.Straight ? ShotShape.Chip : c.Shape;
                            r = pro.Execute(resolver, terrain, ball, new ProShot(c.Club, new Point2(ball.X + (c.DirX * 100.0), ball.Z + (c.DirZ * 100.0)), shape, c.Force), exec, hole.Hole);
                        }

                        strokes += 1 + r.PenaltyStrokes;
                        ball = r.Ball;
                        if (r.Holed)
                        {
                            break;
                        }
                    }

                    gross[n] += Math.Min(strokes, World.MaxStrokes);
                }

                return resolver;
            },
            _ => { });
        return gross.Average(g => Handicap.Differential(g, rating, terrain.Course.Count));
    }

    /// <summary>
    /// Le pro joue au niveau 0 comme un golfeur de handicap 36 et au niveau 50 comme un +4 (même backspin et même lie pour tous, lot 2). Les deux
    /// mesures du pro seulement : le golfeur ordinaire du même niveau en est proche, et chaque mesure coûte une minute en Debug.
    /// </summary>
    [Fact]
    public void OnLeNeuf_FromLevel0To50_ThePro_PlaysLike36ThenPlusFour()
    {
        var terrain = CourseLoader.Load(Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "..", "courses", "le-neuf.json"), 2);
        var world = new World(1, terrain);
        var rating = CourseRater.Rate(terrain, 7, 30);
        var pro0 = Differential(null, AtLevel(0), rating, terrain, world);
        var pro50 = Differential(null, AtLevel(50), rating, terrain, world);
        var ordinary36 = Differential(SkillLevels.FromHandicap(36.0), null, rating, terrain, world);
        var ordinaryPlus4 = Differential(SkillLevels.FromHandicap(-4.0), null, rating, terrain, world);
        output.WriteLine($"pro niveau 0 : {pro0:F2} ; pro niveau 50 : {pro50:F2} ; ordinaire 36 : {ordinary36:F2} ; ordinaire +4 : {ordinaryPlus4:F2}");

        // Le pro joue la même partie que le golfeur ordinaire de son niveau, à un coup près en moyenne (un coup = 2 x 113 / pente en différentiel, pour 9 trous).
        var oneStroke = 2.0 * CourseRating.Reference / rating.Slope;
        Assert.InRange(pro0 - ordinary36, -oneStroke, oneStroke);
        Assert.InRange(pro50 - ordinaryPlus4, -oneStroke, oneStroke);

        // Critère 5 de C25 (absolu, lot 2) : le backspin et le lie étant les mêmes pour tous, le pro joue au niveau 50 comme un +4 (± 1,5)
        // et au niveau 0 comme un 36 (± 2). Différentiels moyens lus comme un handicap (l'index WHS borne à 0 : on prend l'écart tel quel).
        Assert.InRange(pro50, -5.5, -2.5);
        Assert.InRange(pro0, 34.0, 38.0);
    }

    /// <summary>Réserve de l'audit de #127 : le latéral d'un putt ne dépend que de Putter, pas de Dosage.</summary>
    [Fact]
    public void PuttLateralSpread_DependsOnPutterOnly_NotOnDosage()
    {
        var green = ShotTests.Uniform(Surface.Green, cells: 120);
        var resolver = new PhysicalShotResolver();
        var shot = new ProShot(Clubs.Putter, new Point2(30, 240), ShotShape.Straight, 1.0);
        double Lateral(int dosage)
        {
            var p = AtLevel(25);
            p.SetLevel(ProSkill.Dosage, dosage);
            var rng = new DeterministicRng(11);
            var sum = 0.0;
            const int N = 4000;
            for (var i = 0; i < N; i++)
            {
                sum += Math.Abs(p.Execute(resolver, green, Start, shot, rng, new Point2(500, 500)).Ball.Z - Start.Z);
            }

            return sum / N;
        }

        var (low, high) = (Lateral(0), Lateral(50));
        output.WriteLine($"latéral d'un putt de 10 m : Dosage 0 : {low:F4} m ; Dosage 50 : {high:F4} m");
        Assert.InRange(high / low, 0.95, 1.05);
    }

    [Theory]
    [InlineData(ProSkill.RoughExit, Surface.Rough)]
    [InlineData(ProSkill.BunkerExit, Surface.Bunker)]
    public void ExitSkill_AtLevel50_CutsTheLiePenaltyByAtLeastHalf_AndAtLevel0IsTheHandicap36Penalty(ProSkill skill, Surface lie)
    {
        var p = new ProSkills();
        p.SetLevel(skill, 0);
        var zero = p.LieOf(lie);
        p.SetLevel(skill, 50);
        var fifty = p.LieOf(lie);
        Assert.Equal(LieParams.Default.At(lie, 36.0), zero);
        Assert.True(1.0 - fifty.Speed <= 0.5 * (1.0 - zero.Speed));
        Assert.True(fifty.Sigma - 1.0 <= 0.5 * (zero.Sigma - 1.0));
        Assert.Equal(LieEffect.None, p.LieOf(Surface.Fairway));
    }
}
