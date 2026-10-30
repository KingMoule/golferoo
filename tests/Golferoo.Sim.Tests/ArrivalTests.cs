using Golferoo.Sim.Golfers;
using Golferoo.Sim.Pathing;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;
using Golferoo.Sim.Weather;
using Xunit.Abstractions;

namespace Golferoo.Sim.Tests;

/// <summary>C15 lot 2 : arrivées au pavillon, file, départs, sortie par la porte, vivier et réputation.</summary>
public class ArrivalTests(ITestOutputHelper output)
{
    private const int Day = Calendar.TicksPerDay;

    /// <summary>« Le neuf » avec son pavillon, sans ligue (les journées de ligue ne mêlent pas leurs groupes aux arrivées) ; résolveur de substitution pour aller vite sauf demande.</summary>
    internal static World NewWorld(ulong seed = 1, ArrivalParams? arrivals = null, bool physical = false) =>
        new(seed, ClubhouseTests.LeNeuf(), physical ? null : new StubShotResolver(), league: false, placeClubhouse: true, arrivals: arrivals);

    /// <summary>Arrivées comptées par les évènements (file et groupes refusés), jour par jour.</summary>
    private sealed class Counter
    {
        public int Arrived;
        public int TurnedAway;
        public readonly List<int> Sizes = [];
        public readonly List<double> Hours = [];

        public Counter(World world)
        {
            world.GroupArrived += g =>
            {
                Arrived++;
                Sizes.Add(g.Golfers.Count);
                Hours.Add(ArrivalParams.HourOf(world.Date.DayFraction));
            };
            world.GroupTurnedAway += v =>
            {
                TurnedAway++;
                Sizes.Add(v.Count);
                Hours.Add(ArrivalParams.HourOf(world.Date.DayFraction));
            };
        }

        public int Total => Arrived + TurnedAway;
    }

    [Fact]
    public void Arrivals_StayWithinOpeningHours_AndNoGroupLeavesAfterTheLastDeparture()
    {
        var world = NewWorld(2);
        var counter = new Counter(world);
        var released = new HashSet<int>();
        var late = new List<string>();
        for (var i = 0; i < 4 * Day; i++)
        {
            world.Step();
            var hour = ArrivalParams.HourOf(world.Date.DayFraction);
            foreach (var g in world.Groups.Where(g => g.Released && released.Add(g.Id)))
            {
                if (hour >= world.Arrivals.LastDepartureHour + 0.01)
                {
                    late.Add($"groupe {g.Id} parti à {hour:0.00} h");
                }
            }
        }

        Assert.True(counter.Total > 20, $"{counter.Total} arrivées en 4 jours");
        Assert.All(counter.Hours, h => Assert.InRange(h, Calendar.OpeningHour, Calendar.ClosingHour));
        Assert.Empty(late);
        Assert.NotEmpty(released);
    }

    [Fact]
    public void WithoutAnOpenHole_NoGroupArrives()
    {
        var world = NewWorld(3);
        world.Terrain!.Blank = true;
        world.Terrain.Course[1].MoveTee(new Point2(world.Terrain.Course[1].Tee.X + 100, world.Terrain.Course[1].Tee.Z)); // plus un départ : HoleCheck ferme le trou 1
        var counter = new Counter(world);
        world.Step(3 * Day);

        Assert.False(world.HoleOpen);
        Assert.Equal(0, counter.Total);
        Assert.Empty(world.Groups);
        Assert.Equal(0.0, world.Arrivals.Attraction(0, 0.5, Sky.Sun));
    }

    [Fact]
    public void Attraction_FollowsHolesReputationAndWeather_NotTheCourseRating()
    {
        var p = ArrivalParams.Default;
        var baseline = p.Attraction(9, 0.5, Sky.Sun);
        Assert.True(p.Attraction(18, 0.5, Sky.Sun) > baseline);
        Assert.True(p.Attraction(4, 0.5, Sky.Sun) < baseline);
        Assert.True(p.Attraction(9, 0.9, Sky.Sun) > baseline);
        Assert.True(p.Attraction(9, 0.5, Sky.Rain) < p.Attraction(9, 0.5, Sky.Clouds));
        Assert.True(p.Attraction(9, 0.5, Sky.Clouds) < baseline);
        Assert.Equal(p.MaxAttraction, p.AtMaxAttraction().Attraction(3, 0.0, Sky.HeavyRain));
        Assert.True(p.MaxAttraction >= p.Attraction(18, 1.0, Sky.Sun));
    }

    /// <summary>
    /// Critère 4 (version du lot 2, en attente de la décision sur la capacité) : 30 jours de printemps et d'été sur « Le neuf » ouvert, météo naturelle de la graine de référence.
    /// Le parcours n'absorbe qu'environ 1 à 1,5 départ par jour (un tour de 9 trous dure 3 à 4 jours de jeu) : voir le rapport du lot.
    /// </summary>
    [Fact]
    public void LeNeuf_AttractsBetween15And25GroupsADay()
    {
        var world = NewWorld(1, physical: true);
        var counter = new Counter(world);
        var departed = new HashSet<int>();
        for (var i = 0; i < 30 * Day; i += 100)
        {
            world.Step(100);
            departed.UnionWith(world.Groups.Where(g => g.FromArrivals && g.Released).Select(g => g.Id));
        }

        output.WriteLine($"{counter.Total} arrivées en 30 jours ({counter.Total / 30.0:0.0} par jour), {departed.Count} départs ({departed.Count / 30.0:0.0} par jour), {counter.TurnedAway} refusées, réputation {world.Reputation:0.00}");

        Assert.InRange(counter.Total / 30.0, 15.0, 25.0);
    }

    [Fact]
    public void GroupSizes_StayBetween1And4_AndFollowTheData()
    {
        var world = NewWorld(5);
        var counter = new Counter(world);
        while (counter.Sizes.Count < 1000)
        {
            world.Step(Day);
        }

        var sizes = counter.Sizes.Take(1000).ToList();
        Assert.All(sizes, s => Assert.InRange(s, 1, 4));
        var weights = world.Arrivals.GroupSizeWeights;
        for (var n = 1; n <= 4; n++)
        {
            var observed = 100.0 * sizes.Count(s => s == n) / sizes.Count;
            var expected = 100.0 * weights[n - 1] / weights.Sum();
            Assert.InRange(observed, expected - 5.0, expected + 5.0);
        }
    }

    [Fact]
    public void FullQueue_TurnsGroupsAway_RaisesTheEventAndLowersTheReputation()
    {
        var world = NewWorld(6, ArrivalParams.Default.AtMaxAttraction());
        var turned = new List<(int Queue, double Before, double After, int Golfers)>();
        var reputation = world.Reputation;
        var golfers = 0;
        world.GroupTurnedAway += visitors =>
        {
            Assert.All(visitors, v => Assert.False(v.InPlay));
            turned.Add((world.QueueLength, reputation, world.Reputation, golfers));
        };
        var longest = 0;
        for (var i = 0; i < 2 * Day; i++)
        {
            reputation = world.Reputation;
            golfers = world.Groups.Sum(g => g.Golfers.Count);
            world.Step();
            longest = Math.Max(longest, world.QueueLength);
            Assert.True(world.QueueLength <= world.Arrivals.QueueCapacity, $"{world.QueueLength} groupes en file");
        }

        Assert.Equal(world.Arrivals.QueueCapacity, longest);
        Assert.NotEmpty(turned);
        Assert.All(turned, t =>
        {
            Assert.Equal(world.Arrivals.QueueCapacity, t.Queue); // la file était pleine
            Assert.True(t.After < t.Before); // la réputation baisse
        });
    }

    [Fact]
    public void TurnedAwayGroup_PaysNothing_AndItsVisitorsLoseDesire()
    {
        var world = NewWorld(7, ArrivalParams.Default.AtMaxAttraction());
        IReadOnlyList<Visitor>? seen = null;
        var before = new List<double>();
        var balance = 0L;
        var groups = 0;
        world.GroupTurnedAway += v =>
        {
            if (seen is null)
            {
                seen = v;
                before = [.. v.Select(x => x.Desire)];
                Assert.Equal(groups, world.Groups.Count); // aucun groupe de plus dans le monde
            }
        };
        for (var i = 0; i < 2 * Day && seen is null; i++)
        {
            groups = world.Groups.Count;
            balance = world.Ledger.Balance;
            world.Step();
        }

        Assert.NotNull(seen);
        Assert.Equal(balance, world.Ledger.Balance);
        for (var i = 0; i < seen.Count; i++)
        {
            Assert.True(seen[i].Desire <= before[i]);
        }

        Assert.All(seen, v => Assert.Equal(world.DayIndex, v.LastVisitDay));
    }

    [Fact]
    public void FinishedGroup_WalksToTheDoor_ThenLeavesAndItsVisitorsReturnToThePool()
    {
        var world = NewWorld(8);
        var door = world.Clubhouse!.DoorPosition(world.Terrain!.CellSize);
        Group? group = null;
        IReadOnlyList<Visitor>? left = null;
        world.GroupArrived += g => group ??= g;
        world.GroupLeft += v => left ??= v;
        var leaving = new List<Point2>();
        for (var i = 0; i < 5 * Day && left is null; i++)
        {
            world.Step();
            if (group is { Leaving: true })
            {
                leaving.Add(group.Golfers[0].Position);
            }
        }

        Assert.NotNull(group);
        Assert.NotNull(left);
        Assert.DoesNotContain(group, world.Groups);
        Assert.NotEmpty(leaving); // elle a marché vers la porte
        Assert.True(Math.Sqrt(Math.Pow(leaving[^1].X - door.X, 2) + Math.Pow(leaving[^1].Z - door.Z, 2)) < 15.0, "le dernier pas est près de la porte");
        Assert.Equal(group.Golfers.Select(g => g.VisitorId), left.Select(v => v.Id));
        Assert.All(left, v =>
        {
            Assert.False(v.InPlay);
            Assert.Equal(world.DayIndex, v.LastVisitDay);
            Assert.True(v.Desire > world.Arrivals.DesireStart);
        });
        Assert.All(group.Golfers, g => Assert.True(g.Card!.Gross > 0 || g.Card.HoleCount > 0));
    }

    [Fact]
    public void Visitors_ComeBackWithinTwoMonths()
    {
        var world = NewWorld(9);
        var back = 0;
        world.GroupArrived += g => back += g.Golfers.Count(o => world.Visitors.Visitors[o.VisitorId].LastVisitDay >= 0);
        world.Step(60 * Day);
        output.WriteLine($"{world.Visitors.Visitors.Count} visiteurs au vivier, {back} retours en 60 jours");

        Assert.True(back >= 1, "aucun visiteur n'est revenu");
        Assert.Contains(world.Visitors.Visitors, v => v.LastVisitDay >= 0);
    }

    [Fact]
    public void SameSeedAndCommands_GiveTheSameChecksumAfterTenDays()
    {
        var a = NewWorld(10);
        var b = NewWorld(10);
        a.Step(10 * Day);
        b.Step(10 * Day);

        Assert.NotEmpty(a.Visitors.Visitors);
        Assert.Equal(a.Checksum(), b.Checksum());
        Assert.NotEqual(a.Checksum(), NewWorld(11).Checksum());
    }

    [Fact]
    public void WithoutClubhouse_NobodyArrives()
    {
        var world = new World(1, ClubhouseTests.LeNeuf(), new StubShotResolver());
        world.Step(Day);

        Assert.Empty(world.Groups);
        Assert.Empty(world.Visitors.Visitors);
    }

    [Fact]
    public void ArrivalParams_RejectBadFiles()
    {
        Assert.Throws<InvalidDataException>(() => ArrivalParams.Parse("{}"));
        var text = File.ReadAllText(Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "..", "golfers", "arrivals.json"));
        Assert.Equal(12, ArrivalParams.Parse(text).HourProfile.Length);
        Assert.Throws<InvalidDataException>(() => ArrivalParams.Parse(text.Replace("\"groupsPerDay\": 35", "\"groupsPerDay\": 35, \"inconnu\": 1")));
    }

    [Fact]
    public void PaintingOrRaisingTheClubhouseFootprint_IsRefused()
    {
        var world = NewWorld(12);
        var t = world.Terrain!;
        var middle = world.Clubhouse!.Position(t.CellSize);
        var cell = (X: world.Clubhouse.Footprint.MinX + 2, Z: world.Clubhouse.Footprint.MinZ + 2);
        var surface = t.GetCellSurface(cell.X, cell.Z);
        var height = t.Heights.Sample(middle.X, middle.Z);
        var balance = world.Ledger.Balance;

        world.PaintSurface(middle, 3.0, Surface.Bunker);
        world.RaiseTerrain(middle, 3.0, 0.5);
        world.Step();

        Assert.Equal(surface, t.GetCellSurface(cell.X, cell.Z));
        Assert.Equal(height, t.Heights.Sample(middle.X, middle.Z));
        Assert.Equal(balance, world.Ledger.Balance);

        // Loin de l'emprise, la même peinture passe.
        var far = new Point2(world.Clubhouse.Position(t.CellSize).X + (40 * t.CellSize), middle.Z);
        far = new Point2(Math.Min(far.X, (t.CellsX - 3) * t.CellSize), far.Z);
        var before = t.GetCellSurface(t.CellXOf(far.X), t.CellZOf(far.Z));
        world.PaintSurface(far, 6.0, before == Surface.Bunker ? Surface.Fairway : Surface.Bunker);
        world.Step();
        Assert.NotEqual(before, t.GetCellSurface(t.CellXOf(far.X), t.CellZOf(far.Z)));
    }

    [Fact]
    public void FlowDestination_InsideAnObstacle_MovesOutOfIt()
    {
        var t = PathingTests.Flat(40, 20);
        var rect = new CellRect(8, 8, 15, 12);
        var cache = new FlowFieldCache(t, obstacles: [rect]);
        var field = cache.Get(11, 10); // au cœur du bâtiment

        Assert.False(rect.Contains(field.DestX, field.DestZ));
        Assert.True(field.IsReachable(30, 3));
        Assert.True(field.IsReachable(field.DestX, field.DestZ));

        var costs = FlowField.BuildCosts(t, [rect]);
        var direct = FlowField.Build(t, costs, 11, 10);
        Assert.Equal((field.DestX, field.DestZ), (direct.DestX, direct.DestZ));
        Assert.False(rect.Contains(direct.DestX, direct.DestZ));
    }
}
