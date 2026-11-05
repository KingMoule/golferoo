using Golferoo.Sim.Golfers;
using Golferoo.Sim.League;
using Golferoo.Sim.Rating;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;

namespace Golferoo.Sim.Tests;

/// <summary>C12 lot 2 : jouer un parcours (enchaînement des trous, départs espacés, attente du groupe devant, ligue, pro et note sur N trous).</summary>
public class CoursePlayTests
{
    private const int MaxTicks = 120_000;

    private static bool AllFinished(World world) => world.Groups.All(g => g.Finished && g.Golfers.All(o => o.State == GolferState.Done));

    private static void PlayOut(World world, Action? each = null)
    {
        for (var i = 0; i < MaxTicks && !AllFinished(world); i++)
        {
            world.Step();
            each?.Invoke();
        }

        Assert.True(AllFinished(world), "la partie n'est pas finie");
    }

    private sealed record Play(ulong Checksum, List<int>[] Holes, long[] Released);

    private static Play FourGroups(ulong seed)
    {
        var world = new World(seed, CourseTests.ThreeHoles());
        for (var i = 0; i < 4; i++)
        {
            world.SpawnGroup();
        }

        var seen = Enumerable.Range(0, 4).Select(_ => new List<int> { 1 }).ToArray();
        var released = new long[4];
        PlayOut(world, () =>
        {
            foreach (var g in world.Groups)
            {
                if (g.Released && released[g.Id] == 0)
                {
                    released[g.Id] = world.Tick;
                }

                if (g.Hole != seen[g.Id][^1])
                {
                    seen[g.Id].Add(g.Hole);
                }
            }
        });

        foreach (var g in world.Groups)
        {
            foreach (var golfer in g.Golfers)
            {
                Assert.Equal(3, golfer.Card!.HoleCount);
                Assert.True(golfer.Card.IsComplete, "carte incomplète");
                Assert.All(Enumerable.Range(0, 3), h => Assert.InRange(golfer.Card.Strokes(h), 1, World.MaxStrokes));
            }
        }

        return new Play(world.Checksum(), seen, released);
    }

    [Fact]
    public void FourGroups_PlayThreeHolesInOrder_AndHandBackFullCards()
    {
        var play = FourGroups(5);
        Assert.All(play.Holes, h => Assert.Equal([1, 2, 3], h));

        // Departures are spaced: each group leaves the queue after the one in front of it.
        Assert.True(play.Released[0] <= 1);
        for (var i = 1; i < 4; i++)
        {
            Assert.True(play.Released[i] > play.Released[i - 1], $"le groupe {i} part avant le groupe {i - 1}");
        }

        Assert.Equal(play.Checksum, FourGroups(5).Checksum);
        Assert.NotEqual(play.Checksum, FourGroups(6).Checksum);
    }

    [Fact]
    public void ClosedHole_IsSkipped_AndCardShowsItUnplayed()
    {
        var terrain = CourseTests.ThreeHoles();
        terrain.Blank = true;
        var h2 = terrain.Course[2];
        h2.MoveTee(new Point2(h2.Tee.X + 100, h2.Tee.Z)); // no longer a tee cell: HoleCheck closes hole 2
        var world = new World(3, terrain);
        world.Step();
        Assert.True(world.IsHoleOpen(1));
        Assert.False(world.IsHoleOpen(2));
        Assert.True(world.IsHoleOpen(3));

        var group = world.SpawnGroup();
        var seen = new List<int> { 1 };
        PlayOut(world, () =>
        {
            if (group.Hole != seen[^1])
            {
                seen.Add(group.Hole);
            }
        });

        Assert.Equal([1, 3], seen);
        foreach (var g in group.Golfers)
        {
            Assert.True(g.Card!.Strokes(0) > 0);
            Assert.Equal(0, g.Card.Strokes(1));
            Assert.True(g.Card.Strokes(2) > 0);
            Assert.Equal(g.Card.Strokes(0) + g.Card.Strokes(2), g.Card.Gross);
        }
    }

    [Fact]
    public void Group_WaitsToTeeOff_UntilTheOneAheadIsAwayFromTheTee()
    {
        var world = new World(2, CourseTests.ThreeHoles());
        var first = world.SpawnGroup();
        var second = world.SpawnGroup();
        var tee = world.Terrain!.Course[1].Tee;
        for (var i = 0; i < MaxTicks && !second.Released; i++)
        {
            world.Step();
            Assert.True(first.Released);
            Assert.True(!second.Released || first.Hole > 1 || first.Golfers.All(g => Math.Sqrt(Dist2(g.Position, tee)) > World.TeeZoneRadius
                && (Math.Sqrt(Dist2(g.Ball, tee)) > World.DepartureGap || world.Terrain.SurfaceAt(g.Ball.X, g.Ball.Z) == Surface.Green)));
        }

        Assert.True(second.Released);
    }

    [Fact]
    public void Partners_WaitApartAtTheTee_AndTheShooterHitsFromTheBall()
    {
        var world = new World(2, CourseTests.ThreeHoles());
        var group = world.SpawnGroup(4);
        var waited = new int[4];
        var previous = group.Golfers.Select(g => g.State).ToArray();
        var minGap = double.MaxValue;
        for (var t = 0; t < MaxTicks && group.Golfers.Any(g => g.Strokes == 0); t++)
        {
            world.Step();
            for (var i = 0; i < 4; i++)
            {
                var g = group.Golfers[i];
                waited[i] = g.State == GolferState.WaitingTurn && g.Strokes == 0 ? waited[i] + 1 : 0;
                if (g.State == GolferState.Swinging && previous[i] != GolferState.Swinging)
                {
                    Assert.True(Dist2(g.Position, g.Ball) < 1e-6, $"le golfeur {i} frappe à {Math.Sqrt(Dist2(g.Position, g.Ball)):0.0} m de sa balle");
                }

                previous[i] = g.State;
            }

            for (var i = 0; i < 4; i++)
            {
                for (var j = i + 1; j < 4; j++)
                {
                    if (waited[i] > 40 && waited[j] > 40)
                    {
                        minGap = Math.Min(minGap, Math.Sqrt(Dist2(group.Golfers[i].Position, group.Golfers[j].Position)));
                    }
                }
            }
        }

        Assert.NotEqual(double.MaxValue, minGap);
        Assert.True(minGap >= 1.5, $"deux golfeurs en attente à {minGap:0.00} m");
    }

    /// <summary>Un seul trou, des visiteurs en rafale : le plafond de groupes sur le parcours ne retient plus le suivant tant que celui de devant n'a pas fini le trou, il le libère dès qu'il est sur le green.</summary>
    [Fact]
    public void ArrivalsGroup_LeavesOnceTheOneAheadIsOnTheGreen_NotOnceItHasFinishedTheHole()
    {
        var terrain = CourseLoader.Load(Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "proto-par4.json"), 2);
        var world = new World(1, terrain, new StubShotResolver(), league: false, placeClubhouse: true, arrivals: ArrivalParams.Default with { GroupsPerDay = 400 });
        var onCourse = 0;
        var freed = 0; // ticks où plus de groupes que le plafond à l'ancienne sont sur le parcours
        for (var t = 0; t < 3 * Calendar.TicksPerDay; t++)
        {
            world.Step();
            onCourse = world.Groups.Count(g => g.Released && !g.Finished && !g.Leaving);
            freed += onCourse > world.Arrivals.MaxGroupsPerHole ? 1 : 0;
        }

        Assert.True(freed > 0, "jamais plus de groupes que le plafond sur le parcours : la règle ne libère rien");
    }

    private static double Dist2(Point2 a, Point2 b) => ((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z));

    /// <summary>Plays <paramref name="games"/> games of two close groups ; checks every ball that comes to rest and every idle stretch.</summary>
    private static (double MinGap, int LongestIdle, int LeaderIdle) TwoGroups(int games, Func<IShotResolver?> resolver, int maxTicks)
    {
        var minGap = double.MaxValue;
        var longest = 0;
        var leader = 0;
        for (var game = 0; game < games; game++)
        {
            var world = new World((ulong)(1000 + game), CourseTests.ThreeHoles(), resolver());
            var groups = new[] { world.SpawnGroup(), world.SpawnGroup() };
            var strokes = groups.SelectMany(g => g.Golfers).ToDictionary(g => g.Id, _ => 0);
            var idle = new int[2];
            for (var t = 0; t < maxTicks && !AllFinished(world); t++)
            {
                world.Step();
                for (var k = 0; k < 2; k++)
                {
                    var g = groups[k];
                    idle[k] = g.Released && !g.Finished && g.Golfers.All(o => o.State is GolferState.WaitingTurn or GolferState.Done) ? idle[k] + 1 : 0;
                    longest = Math.Max(longest, idle[k]);
                    leader = k == 0 ? Math.Max(leader, idle[k]) : leader;
                    foreach (var o in g.Golfers)
                    {
                        if (o.Strokes == strokes[o.Id] || o.Strokes == 0)
                        {
                            strokes[o.Id] = o.Strokes;
                            continue;
                        }

                        strokes[o.Id] = o.Strokes;
                        foreach (var other in groups.Where(x => x != g && x.Released && x.Hole == g.Hole).SelectMany(x => x.Golfers).Where(x => x.State != GolferState.Done))
                        {
                            minGap = Math.Min(minGap, Math.Sqrt(Dist2(o.Ball, other.Position)));
                        }
                    }
                }
            }
        }

        return (minGap, longest, leader);
    }

    [Fact]
    public void FiveHundredGamesOfTwoCloseGroups_NoBallStopsNearAnotherGroup_AndNobodyWaitsTooLong_Stub()
    {
        // Stub resolver and a bounded stretch of play (about the first two holes) keep 500 games well under a minute.
        var (minGap, longest, leader) = TwoGroups(500, () => new StubShotResolver(), 9_000);
        Assert.True(minGap >= 10.0, $"balle à {minGap:0.0} m d'un autre groupe");
        Assert.Equal(0, leader);
        Assert.True(longest <= 2 * 60 * World.TicksPerSecond, $"attente de {longest} ticks");
    }

    [Fact]
    public void SixGamesOfTwoCloseGroups_WithThePhysicalResolver_NoBallStopsNearAnotherGroup()
    {
        var (minGap, _, leader) = TwoGroups(6, () => null, 14_000);
        Assert.True(minGap >= 10.0, $"balle à {minGap:0.0} m d'un autre groupe");
        Assert.Equal(0, leader);
    }

    [Fact]
    public void LeagueDay_PlaysEveryHole_AndRatingCountsThem()
    {
        var terrain = CourseTests.ThreeHoles();
        var world = new World(7, terrain, new StubShotResolver(), league: true);
        world.SetCourseRating(CourseRater.Rate(terrain, 7, games: 3));
        world.Step();
        Assert.Equal(3, world.CourseRating!.Holes);
        Assert.Equal(12, world.CourseRating.Par);

        var first = world.League!.Rounds[0];
        for (var i = 0; i < 20 * Golferoo.Sim.Time.Calendar.TicksPerDay && !first.Closed; i++)
        {
            world.Step();
        }

        Assert.True(first.Closed, "la première journée de ligue n'est pas finie");
        Assert.NotEmpty(first.Cards);
        Assert.All(first.Cards.Values, c =>
        {
            Assert.Equal(3, c.HoleCount);
            Assert.True(c.IsComplete);
        });
    }

    [Fact]
    public void ProTrainingRound_PlaysEveryHole()
    {
        var world = new World(4, CourseTests.ThreeHoles(), new StubShotResolver(), league: true);
        var group = world.StartProRound();
        PlayOut(world);
        Assert.Single(group.Golfers);
        Assert.True(group.Golfers[0].Card!.IsComplete);
        Assert.Equal(3, group.Golfers[0].Card!.HoleCount);
        Assert.Equal(3, group.Hole);
    }

    [Fact]
    public void Rater_PlaysEveryHole()
    {
        var terrain = CourseTests.ThreeHoles();
        var rating = CourseRater.Rate(terrain, 11, games: 4);
        Assert.Equal(3, rating.Holes);
        Assert.Equal(terrain.Course.Holes.Sum(h => h.Par), rating.Par);
        Assert.InRange(rating.ScratchPerHole, 2.5, 7.0);
        var one = CourseRater.Rate(CourseLoader.Load(Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "proto-par4.json"), 2), 11, games: 4);
        Assert.Equal(1, one.Holes);
    }
}
