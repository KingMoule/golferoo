using Golferoo.Sim.Economy;
using Golferoo.Sim.Golfers;
using Golferoo.Sim.Pathing;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;
using Xunit.Abstractions;

namespace Golferoo.Sim.Tests;

/// <summary>C16 lot 1 : humeur, événements, abandon, réactions par trou, green fee et réputation.</summary>
public class MoodTests(ITestOutputHelper output)
{
    private const int Day = Calendar.TicksPerDay;

    /// <summary>Résolveur de test : <paramref name="script"/> décide du coup, ou rend nul pour laisser faire le résolveur provisoire.</summary>
    private sealed class Scripted(Func<ShotRequest, TerrainGrid, ShotResult?> script) : IShotResolver
    {
        private readonly StubShotResolver _stub = new();

        public ShotResult Resolve(in ShotRequest request, TerrainGrid terrain, DeterministicRng rng) =>
            script(request, terrain) ?? _stub.Resolve(request, terrain, rng);
    }

    private static bool OnGreen(ShotRequest r, TerrainGrid t) => t.SurfaceAt(r.Ball.X, r.Ball.Z) == Surface.Green;

    private static World Arrivals(IShotResolver? resolver = null, MoodParams? mood = null, ulong seed = 1) =>
        new(seed, ClubhouseTests.LeNeuf(), resolver ?? new StubShotResolver(), league: false, placeClubhouse: true, mood: mood);

    /// <summary>Avance jusqu'à ce que <paramref name="done"/> soit vrai (au plus 4 jours), en notant les changements d'humeur.</summary>
    private static List<MoodChange> Play(World world, Func<List<MoodChange>, bool> done)
    {
        var changes = new List<MoodChange>();
        world.MoodChanged += changes.Add;
        for (var i = 0; i < 4 * Day && !done(changes); i++)
        {
            world.Step();
        }

        return changes;
    }

    [Fact]
    public void EasyPuttMissed_RaisesTheExpectedEvent_AndLowersTheMood()
    {
        // Le premier putt reste à un mètre du trou sans tomber, le suivant tombe.
        var world = Arrivals(new Scripted((r, t) => OnGreen(r, t) && r.StrokeNumber < 3
            ? new ShotResult(new Point2(r.Hole.X + 1.0, r.Hole.Z), false)
            : null));
        var changes = Play(world, c => c.Any(x => x.Event == MoodEvent.EasyPuttMissed));

        var change = changes.First(c => c.Event == MoodEvent.EasyPuttMissed);
        Assert.Equal(world.Moods.Definitions[(int)MoodEvent.EasyPuttMissed].Value, change.Delta);
        Assert.True(change.Golfer.Mood < world.Moods.Start);
        Assert.True(world.Reactions.Count(change.Hole, MoodEvent.EasyPuttMissed) >= 1);
        Assert.Equal(change.Cell, (world.Terrain!.CellXOf(change.Golfer.Ball.X), world.Terrain.CellZOf(change.Golfer.Ball.Z)));
    }

    [Fact]
    public void BallInTheWater_RaisesTheEvent_AndCountsItOnTheHole()
    {
        var world = Arrivals(new Scripted((r, t) => !OnGreen(r, t) && r.StrokeNumber == 0
            ? new ShotResult(t.Tee, false, 1, ShotOutcome.Water)
            : null));
        var changes = Play(world, c => c.Any(x => x.Event == MoodEvent.Water));

        var change = Assert.Single(changes, c => c.Event == MoodEvent.Water);
        Assert.Equal(1, change.Hole);
        Assert.Equal(world.Moods.Definitions[(int)MoodEvent.Water].Value, change.Delta);
        Assert.Equal(world.Moods.Start + change.Delta, change.Golfer.Mood);
        Assert.Equal(1, world.Reactions.Count(1, MoodEvent.Water));
        Assert.Equal(0, world.Reactions.Count(2, MoodEvent.Water));
    }

    [Fact]
    public void PositiveEventAtTheCeiling_IsRaisedWithTheRealDelta_AndStillCounted()
    {
        var world = Arrivals(mood: MoodParams.Default with { Start = MoodParams.Max });
        var changes = Play(world, c => c.Any(x => world.Moods.Definitions[(int)x.Event].Value > 0));

        var change = changes.First(c => world.Moods.Definitions[(int)c.Event].Value > 0);
        Assert.Equal(0, change.Delta);
        Assert.Equal(MoodParams.Max, change.Golfer.Mood);
        Assert.True(world.Reactions.Count(change.Hole, change.Event) >= 1);
    }

    [Fact]
    public void MinorAnnoyance_CountsOnlyFromItsRepetition()
    {
        // Chaque coup hors green finit au bunker : la contrariété mineure se répète.
        var world = Arrivals(new Scripted((r, t) => !OnGreen(r, t) ? new ShotResult(r.Ball, false, 0, ShotOutcome.Bunker) : null));
        var threshold = world.Moods.RepeatThreshold;
        Assert.True(world.Moods.Definitions[(int)MoodEvent.Bunker].Minor);
        Assert.True(threshold >= 2);
        var golfer = (Golfer?)null;
        var counts = new List<int>();
        var changes = Play(world, _ =>
        {
            golfer ??= world.Groups.FirstOrDefault(g => g.Released)?.Golfers[0];
            if (golfer is not null && (counts.Count == 0 || counts[^1] != golfer.Occurrences[(int)MoodEvent.Bunker]))
            {
                counts.Add(golfer.Occurrences[(int)MoodEvent.Bunker]);
            }

            return golfer is not null && golfer.Occurrences[(int)MoodEvent.Bunker] > threshold;
        });

        var mine = changes.Where(c => c.Golfer == golfer && c.Event == MoodEvent.Bunker).ToList();
        Assert.NotEmpty(counts);
        // Les occurrences avant la répétition n'ont rien changé : le premier événement levé est celui de la n-ième.
        Assert.Equal(golfer!.Occurrences[(int)MoodEvent.Bunker] - threshold + 1, mine.Count);
        Assert.All(mine, c => Assert.Equal(world.Moods.Definitions[(int)MoodEvent.Bunker].Value, c.Delta));
        Assert.Equal(mine.Count, world.Reactions.Count(1, MoodEvent.Bunker) + world.Reactions.Count(2, MoodEvent.Bunker));
    }

    [Fact]
    public void IsolatedMinorAnnoyance_ChangesNothing()
    {
        var once = false;
        var world = Arrivals(new Scripted((r, t) =>
        {
            if (OnGreen(r, t) || once)
            {
                return null;
            }

            once = true;
            return new ShotResult(r.Ball, false, 0, ShotOutcome.Bunker);
        }));
        var changes = Play(world, _ => world.Groups.Any(g => g.Released && g.Hole >= 2));

        Assert.True(once);
        Assert.DoesNotContain(changes, c => c.Event == MoodEvent.Bunker);
        Assert.Equal(0, world.Reactions.Count(1, MoodEvent.Bunker));
        Assert.Equal(1, world.Groups.First(g => g.Released).Golfers.Sum(g => g.Occurrences[(int)MoodEvent.Bunker]));
    }

    [Fact]
    public void GolferBelowZero_Quits_GoesBackToTheClubhouse_AndHisPartnersLoseOne()
    {
        var world = Arrivals(mood: MoodParams.Default with { Start = 10 });
        Golfer? quitter = null;
        QuitInfo? info = null;
        world.GroupArrived += g => quitter ??= g.Golfers.Count >= 2 ? g.Golfers[0] : null;
        world.GolferQuit += q => info ??= q;
        Group? group = null;
        List<int>? before = null;
        var after = new List<int>();
        for (var i = 0; i < 4 * Day && info is null; i++)
        {
            if (quitter is not null)
            {
                group ??= world.Groups.First(g => g.Golfers.Contains(quitter));
                quitter.Mood = -1; // l'humeur ne se vérifie qu'en fin de trou
                before = [.. group.Golfers.Where(g => g != quitter).Select(g => g.Mood)];
            }

            world.Step();
        }

        Assert.NotNull(info);
        Assert.Same(quitter, info.Value.Golfer);
        Assert.True(quitter!.Quit);
        Assert.Equal(1, info.Value.Hole);
        after.AddRange(group!.Golfers.Where(g => g != quitter).Select(g => g.Mood));
        Assert.NotEmpty(after);
        Assert.Equal(before!.Select(m => m - 1), after); // aucun autre événement dans ce tick : la seule baisse est celle de l'abandon

        // Il rentre au pavillon, ses partenaires jouent le trou suivant sans lui.
        var door = world.Clubhouse!.DoorPosition(world.Terrain!.CellSize);
        world.Step(Day);
        Assert.Equal(GolferState.Done, quitter.State);
        Assert.True(Math.Sqrt(Math.Pow(quitter.Position.X - door.X, 2) + Math.Pow(quitter.Position.Z - door.Z, 2)) < 15.0);
        Assert.Equal(0, quitter.Card!.Strokes(1));
        Assert.True(group.Hole >= 2 || !world.Groups.Contains(group));
    }

    [Fact]
    public void QuitterWhoLeavesAlone_DoesNotBlockTheGroupFromLeavingTheWorld()
    {
        // Tout le monde commence à zéro et perd un point à chaque trou : le groupe entier abandonne, puis quitte le monde.
        var world = Arrivals(mood: MoodParams.Default with { Start = 0, DropEveryHoles = 1 });
        var quits = 0;
        var left = 0;
        world.GolferQuit += _ => quits++;
        world.GroupLeft += _ => left++;
        world.Step(3 * Day);

        Assert.True(quits > 0);
        Assert.True(left > 0);
        Assert.DoesNotContain(world.Groups, g => g.FromArrivals && g.Finished); // un groupe fini est sorti de la liste
    }

    [Fact]
    public void Reactions_AreKeptForThePreviousMonth_AndResetEachMonth()
    {
        var world = Arrivals(new Scripted((r, t) => !OnGreen(r, t) && r.StrokeNumber == 0 ? new ShotResult(t.Tee, false, 1, ShotOutcome.Water) : null));
        Play(world, _ => world.Reactions.Count(1, MoodEvent.Water) >= 2);
        var counted = world.Reactions.Count(1, MoodEvent.Water);
        Assert.True(counted >= 2);
        Assert.Equal(0, world.Reactions.Previous(1, MoodEvent.Water));

        world.SkipToNextMonth();

        Assert.Equal(counted, world.Reactions.Previous(1, MoodEvent.Water));
        Assert.Equal(0, world.Reactions.Count(1, MoodEvent.Water));
    }

    [Fact]
    public void GreenFee_FollowsTheMood_FromHalfToOneAndAHalfTimes()
    {
        var world = Arrivals();
        Golfer? g = null;
        world.GroupArrived += x => g ??= x.Golfers[0];
        while (g is null && world.Tick < 5 * Day)
        {
            world.Step();
        }

        Assert.NotNull(g);
        var visitor = world.Economy.GreenFeeVisitor;
        long Fee(int mood)
        {
            g.Mood = mood;
            return world.GreenFee(g);
        }

        Assert.Equal(visitor, Fee(0));
        Assert.Equal(visitor * world.Economy.MoodFactorMinBasisPoints / 10_000, Fee(-10));
        Assert.Equal(visitor * world.Economy.MoodFactorMaxBasisPoints / 10_000, Fee(10));
        Assert.True(Fee(-5) < Fee(0) && Fee(0) < Fee(5));
    }

    [Fact]
    public void QuitterPaysNoMoreGreenFees()
    {
        // Un paiement par golfeur et par trou joué : un golfeur qui abandonne après le trou k ne paie que k fois, les autres autant que de trous sur leur carte.
        // Le premier de chaque groupe arrive à humeur nulle et passe sous zéro dès le trou 1 ; ses partenaires, à 10, continuent seuls.
        var world = Arrivals(mood: MoodParams.Default with { DropEveryHoles = 1 });
        var golfers = new List<Golfer>();
        var quitAfter = new Dictionary<Golfer, int>();
        var withPartners = 0;
        var payments = 0;
        world.GroupArrived += g =>
        {
            golfers.AddRange(g.Golfers);
            withPartners += g.Golfers.Count > 1 ? 1 : 0;
            foreach (var o in g.Golfers)
            {
                o.Mood = o == g.Golfers[0] ? 0 : MoodParams.Max;
            }
        };
        world.GolferQuit += q => quitAfter[q.Golfer] = q.Hole;
        world.Ledger.MoneyChanged += _ => payments++;
        world.Step(3 * Day);

        var expected = golfers.Sum(g => quitAfter.TryGetValue(g, out var hole) ? hole : Enumerable.Range(0, g.Card!.HoleCount).Count(h => g.Card.Strokes(h) > 0));
        output.WriteLine($"{quitAfter.Count} abandons, {payments} paiements, {expected} attendus");
        Assert.True(withPartners > 0 && quitAfter.Values.Any(h => h < 9), "aucun abandon avec des partenaires qui continuent : le test ne couvre rien");
        Assert.Equal(expected, payments);
    }

    [Fact]
    public void Reputation_FollowsTheExitMood()
    {
        var world = Arrivals();
        var weight = world.Arrivals.ReputationWeight;
        var byVisitor = new Dictionary<int, Golfer>();
        world.GroupArrived += g =>
        {
            foreach (var o in g.Golfers)
            {
                byVisitor[o.VisitorId] = o;
            }
        };
        var before = world.Reputation;
        var checkedOne = false;
        world.GroupLeft += visitors =>
        {
            var score = visitors.Average(v => world.Moods.ExitScore(byVisitor[v.Id].Mood));
            Assert.Equal(before + ((score - before) * weight), world.Reputation, 12);
            checkedOne = true;
        };
        for (var i = 0; i < 5 * Day && !checkedOne; i++)
        {
            before = world.Reputation;
            world.Step();
        }

        Assert.True(checkedOne);
    }

    [Fact]
    public void ExitScore_GoesFromZeroToOne()
    {
        var p = MoodParams.Default;
        Assert.Equal(0.0, p.ExitScore(-10));
        Assert.Equal(1.0, p.ExitScore(p.ReputationFullMood));
        Assert.Equal(1.0, p.ExitScore(10));
        Assert.True(p.ExitScore(0) is > 0.0 and < 1.0);
    }

    [Fact]
    public void MoodData_HasOneFrenchThoughtPerEvent()
    {
        var p = MoodParams.Default;
        Assert.Equal(16, p.Definitions.Count);
        Assert.InRange(p.Start, 3, 7); // assez bas pour que les événements positifs comptent (pas écrêtés au plafond)
        Assert.All(Enum.GetValues<MoodEvent>(), e => Assert.Equal(e, p.Definitions[(int)e].Event));
        Assert.All(p.Definitions, d => Assert.True(d.Thought.Length > 10 && d.Value != 0));
        Assert.Equal(p.Definitions.Count, p.Definitions.Select(d => d.Thought).Distinct().Count());
    }

    [Theory]
    [InlineData("\"start\": 7,", "\"start\": 11,")]
    [InlineData("\"id\": \"water\"", "\"id\": \"inconnu\"")]
    [InlineData("\"thought\": \"Ma balle est partie à l'eau, zut.\"", "\"thought\": \"\"")]
    [InlineData("\"dropEveryHoles\": 5,", "")]
    public void MoodData_Incomplete_IsRefused(string from, string to)
    {
        var text = File.ReadAllText(Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "..", "golfers", "mood.json"));
        Assert.Contains(from, text);
        Assert.Throws<InvalidDataException>(() => MoodParams.Parse(text.Replace(from, to)));
    }

    /// <summary>Critère 7 : même graine, mêmes réglages, même somme de contrôle après 10 jours avec humeur, abandons et réactions.</summary>
    [Fact]
    public void SameSeed_GivesTheSameChecksum_AfterTenDaysWithQuits()
    {
        var grumpy = MoodParams.Default with { Start = 2, DropEveryHoles = 1 };
        var a = Arrivals(mood: grumpy, seed: 7);
        var b = Arrivals(mood: grumpy, seed: 7);
        var quits = 0;
        a.GolferQuit += _ => quits++;
        a.Step(10 * Day);
        b.Step(10 * Day);

        Assert.True(quits > 0, "aucun abandon : le test ne couvre rien");
        Assert.Equal(a.Checksum(), b.Checksum());
        Assert.True(Enumerable.Range(1, 9).Sum(h => Enum.GetValues<MoodEvent>().Sum(e => a.Reactions.Count(h, e) + a.Reactions.Previous(h, e))) > 0, "aucune réaction comptée");
    }

    /// <summary>« Le neuf » sans modification, résolveur physique, 8 jours de printemps : partagé par le test de calage de la CI et celui du lac.</summary>
    private static readonly Lazy<MoodRun> PlainWeek = new(() => MoodRun.Go(8, physical: true));

    /// <summary>
    /// Critère 3 en CI. Le résolveur simplifié ne peut pas servir au calage : il ne rend ni eau, ni arbre, ni bunker, ni mauvais coup, donc personne n'abandonne
    /// et l'humeur reste au plafond (mesuré : 0 abandon, sortie à 9,9, 29 % des positifs écrêtés). On cale donc en CI sur le résolveur physique, 8 jours
    /// (environ 50 golfeurs, graine 1), avec une borne qui échoue si le calage dérive : au moins un abandon, moins de 8 %, sortie positive et sous le plafond.
    /// </summary>
    [Fact]
    public void LeNeuf_PhysicalWeek_SomeQuitButFewAndTheyLeaveHappy()
    {
        var run = PlainWeek.Value;
        output.WriteLine($"physique, 8 jours : {run.Golfers} golfeurs, {run.Quits} abandons ({run.QuitShare:P1}), humeur de sortie moyenne {run.MeanExit:0.00}, positifs écrêtés {run.Clipped}/{run.Positives} ({run.ClippedShare:P1})");
        Assert.True(run.Golfers >= 30, $"{run.Golfers} golfeurs seulement : l'échantillon est trop petit");
        Assert.True(run.Quits >= 1, "aucun abandon : le calage est devenu trop indulgent");
        Assert.True(run.QuitShare < 0.08, $"{run.QuitShare:P1} d'abandons");
        Assert.InRange(run.MeanExit, 1.0, 9.0);
        Assert.True(run.ClippedShare < 0.10, $"{run.ClippedShare:P1} des événements positifs perdus au plafond");
    }

    /// <summary>Même borne avec le résolveur physique sur trois graines (environ 3 minutes chacune) : lancée si <c>GOLFEROO_LONG</c> est défini, comme à la main au calage.</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void LeNeuf_Physical_FewQuitAndTheyLeaveHappy(ulong seed)
    {
        if (Environment.GetEnvironmentVariable("GOLFEROO_LONG") is null)
        {
            return;
        }

        AssertCalibration(MoodRun.Go(30, physical: true, seed), $"physique, graine {seed}");
    }

    private void AssertCalibration(MoodRun run, string label)
    {
        output.WriteLine($"{label} : {run.Golfers} golfeurs, {run.Quits} abandons ({run.QuitShare:P1}), humeur de sortie moyenne {run.MeanExit:0.00}, positifs écrêtés {run.Clipped}/{run.Positives} ({run.ClippedShare:P1}), réputation {run.World.Reputation:0.00}");
        Assert.True(run.Golfers >= 100, $"{run.Golfers} golfeurs seulement : l'échantillon est trop petit");
        Assert.True(run.QuitShare < 0.05, $"{run.QuitShare:P1} d'abandons");
        Assert.True(run.MeanExit > 0.0, $"humeur de sortie moyenne {run.MeanExit:0.00}");
        Assert.True(run.ClippedShare < 0.10, $"{run.ClippedShare:P1} des événements positifs perdus au plafond : l'humeur de départ est trop haute");
    }

    /// <summary>
    /// Critère 3 : un lac devant le green du trou 1 fait baisser l'humeur sur ce trou, et le compteur « balle dans l'eau » monte.
    /// Résolveur physique, 8 jours de printemps, même graine des deux côtés.
    /// </summary>
    [Fact]
    public void LakeInFrontOfTheGreen_LowersTheMoodOnThatHole_AndCountsTheWater()
    {
        var plain = PlainWeek.Value;
        var lake = MoodRun.Go(8, physical: true, setup: w =>
        {
            var hole = w.Terrain!.Course[1];
            var (dx, dz) = (hole.Hole.X - hole.Tee.X, hole.Hole.Z - hole.Tee.Z);
            var len = Math.Sqrt((dx * dx) + (dz * dz));
            w.PaintSurface(new Point2(hole.Hole.X - (dx / len * 15.0), hole.Hole.Z - (dz / len * 15.0)), 14.0, Surface.Water);
        });

        double PerPlay(MoodRun r) => r.DeltaByHole[1] / Math.Max(1, r.PlaysByHole[1]);
        var (plainWater, lakeWater) = (plain.World.Reactions.Count(1, MoodEvent.Water), lake.World.Reactions.Count(1, MoodEvent.Water));
        output.WriteLine($"trou 1, humeur par partie : {PerPlay(plain):0.00} sans lac, {PerPlay(lake):0.00} avec ; balle dans l'eau : {plainWater} puis {lakeWater}");

        Assert.True(lakeWater > plainWater, $"balle dans l'eau : {plainWater} sans lac, {lakeWater} avec");
        Assert.True(PerPlay(lake) < PerPlay(plain), $"humeur par partie : {PerPlay(plain):0.00} sans lac, {PerPlay(lake):0.00} avec");
    }
}
