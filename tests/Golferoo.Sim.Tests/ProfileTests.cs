using Golferoo.Sim.Golfers;
using Golferoo.Sim.League;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Xunit.Abstractions;

namespace Golferoo.Sim.Tests;

/// <summary>C09 lot 3 : six archétypes de golfeurs, qui changent les choix et l'exécution à niveau égal.</summary>
public class ProfileTests(ITestOutputHelper output)
{
    private const int Holes = 200;

    private static readonly double Level = SkillLevels.FromHandicap(10);

    private static (TerrainGrid Terrain, World World) Dogleg()
    {
        var terrain = HoleLoader.Load(Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "dogleg-par4.json"), 2);
        return (terrain, new World(1, terrain));
    }

    private sealed record Stats(double TeeDriver, double ShapedShare, int Penalties, double DriveLength, double Strokes);

    private static Stats Play(Archetype profile)
    {
        var (terrain, world) = Dogleg();
        var drivers = 0;
        var full = 0;
        var shaped = 0;
        var penalties = 0;
        var drive = 0.0;
        var strokes = 0;
        var gate = new object();
        Parallel.For(1, Holes + 1, seed =>
        {
            // Un vent par trou, comme d'un jour à l'autre (jusqu'à 8 m/s, toutes directions) : le golfeur le connaît et planifie avec.
            var weather = new DeterministicRng(0xC0FFEEUL + (ulong)seed);
            var angle = weather.NextDouble() * 2.0 * Math.PI;
            var speed = 8.0 * weather.NextDouble();
            var resolver = new PhysicalShotResolver { Trees = world.Trees, Season = world.Season, Wind = new Vec3(Math.Cos(angle) * speed, 0.0, Math.Sin(angle) * speed) };
            var rng = new DeterministicRng((ulong)seed * 7919UL);
            var ball = terrain.Tee;
            int n = 0, d = 0, f = 0, s = 0, p = 0;
            var length = 0.0;
            while (n < World.MaxStrokes)
            {
                var r = resolver.Resolve(new ShotRequest(ball, terrain.Hole, n, Level, profile), terrain, rng, out var plan);
                if (plan is { } shot)
                {
                    f += shot.Low ? 0 : 1;
                    s += shot.Shape != ShotShape.Straight ? 1 : 0;
                    if (n == 0)
                    {
                        d += shot.Club == Clubs.Driver ? 1 : 0;
                        length = Math.Sqrt(((r.Ball.X - ball.X) * (r.Ball.X - ball.X)) + ((r.Ball.Z - ball.Z) * (r.Ball.Z - ball.Z)));
                    }
                }

                n += 1 + r.PenaltyStrokes;
                p += r.PenaltyStrokes;
                ball = r.Ball;
                if (r.Holed)
                {
                    break;
                }
            }

            lock (gate)
            {
                drivers += d;
                full += f;
                shaped += s;
                penalties += p;
                drive += length;
                strokes += Math.Min(n, World.MaxStrokes);
            }
        });

        return new Stats((double)drivers / Holes, (double)shaped / Math.Max(1, full), penalties, drive / Holes, (double)strokes / Holes);
    }

    [Fact]
    public void Dogleg_SixArchetypes_PlayVisiblyDifferently()
    {
        var stats = new Dictionary<Archetype, Stats>();
        foreach (var a in Enum.GetValues<Archetype>())
        {
            stats[a] = Play(a);
            var s = stats[a];
            output.WriteLine($"{a,-10} driver au tee {s.TeeDriver:P0}, effet {s.ShapedShare:P0} des coups pleins, pénalités {s.Penalties}, drive {s.DriveLength:F1} m, coups {s.Strokes:F2}");
        }

        Assert.True(stats[Archetype.LongHitter].TeeDriver > stats[Archetype.Cautious].TeeDriver);
        Assert.True(stats[Archetype.Drawer].ShapedShare > 0.5);
        Assert.True(stats[Archetype.Cautious].Penalties < stats[Archetype.Charger].Penalties);
        Assert.True(stats[Archetype.LongHitter].DriveLength > stats[Archetype.Cautious].DriveLength);
    }

    [Fact]
    public void Members_HaveAStableProfile_AndGenerateMembersKeepsNamesAndHandicaps()
    {
        var a = LeagueSeason.GenerateMembers(42);
        var b = LeagueSeason.GenerateMembers(42);

        Assert.Equal(a.Select(m => m.Profile), b.Select(m => m.Profile));
        Assert.DoesNotContain(a, m => m.Profile == Archetype.Neutral);
        Assert.Equal(GolferProfile.Count, a.Select(m => m.Profile).Distinct().Count());

        // Noms et handicaps tirés comme avant le lot (graine 42, relevés sur origin/main).
        Assert.Equal(Names42, string.Join(";", a.Take(3).Select(m => $"{m.Name}|{m.StartHandicap}|{m.Opinion}")));
    }

    private const string Names42 = "Romane T.|3|47;Quentin I.|29|23;Claire D.|23|68";

    [Fact]
    public void Profiles_AreSpreadUniformly()
    {
        var counts = new int[GolferProfile.Count + 1];
        for (var id = 0; id < 6000; id++)
        {
            counts[(int)GolferProfile.ForMember(7, id)]++;
        }

        Assert.Equal(0, counts[0]);
        Assert.All(counts.Skip(1), c => Assert.InRange(c, 850, 1150));
    }

    [Fact]
    public void World_GivesOrdinaryGolfersAProfile_AndMixesItInTheChecksum()
    {
        var (_, world) = Dogleg();
        var group = world.SpawnGroup();

        Assert.All(group.Golfers, g => Assert.NotEqual(Archetype.Neutral, g.Profile));
        var before = world.Checksum();
        var g0 = group.Golfers[0];
        var kept = g0.Profile;
        g0.Profile = kept == Archetype.Artist ? Archetype.Charger : Archetype.Artist;
        Assert.NotEqual(before, world.Checksum());
        g0.Profile = kept;
        Assert.Equal(before, world.Checksum());
    }

    [Fact]
    public void Beauty_IsZeroMidFairway_RisesNearWater_AndWithABeautySource()
    {
        var terrain = HoleLoader.Load(TerrainTests.HolePath(), 2);
        var world = new World(1, terrain);
        var resolver = new PhysicalShotResolver { Trees = world.Trees, Season = Trees.Season.Summer, BeautySources = world.BeautySources };
        var (fairway, water) = FairwayAndWaterEdge(terrain, resolver.Beauty(terrain), world);

        Assert.Equal(0.0, resolver.Beauty(terrain).At(fairway.X, fairway.Z));
        Assert.True(resolver.Beauty(terrain).At(water.X, water.Z) > 0.5);

        world.AddBeautySource(new Point2(fairway.X + 4.0, fairway.Z));
        Assert.True(resolver.Beauty(terrain).At(fairway.X, fairway.Z) > 0.5);
    }

    /// <summary>Une cellule de fairway à plus de 15 m de l'eau et des arbres, et une cellule de terre au bord de l'eau.</summary>
    private static (Point2 Fairway, Point2 WaterEdge) FairwayAndWaterEdge(TerrainGrid terrain, BeautyMap map, World world)
    {
        Point2? fairway = null;
        Point2? edge = null;
        for (var cz = 1; cz < terrain.CellsZ - 1; cz++)
        {
            for (var cx = 1; cx < terrain.CellsX - 1; cx++)
            {
                var c = terrain.CellCenter(cx, cz);
                var surface = terrain.GetCellSurface(cx, cz);
                if (fairway is null && surface == Surface.Fairway && map.At(c.X, c.Z) == 0.0)
                {
                    fairway = c;
                }

                if (edge is null && surface != Surface.Water && terrain.GetCellSurface(cx + 1, cz) == Surface.Water)
                {
                    edge = c;
                }
            }
        }

        Assert.NotNull(fairway);
        Assert.NotNull(edge);
        return (fairway!.Value, edge!.Value);
    }

    [Fact]
    public void Artist_ScattersLessInABeautifulPlace()
    {
        var terrain = HoleLoader.Load(TerrainTests.HolePath(), 2);
        var world = new World(1, terrain);
        var resolver = new PhysicalShotResolver { Trees = world.Trees, Season = world.Season, BeautySources = world.BeautySources };
        var (_, edge) = FairwayAndWaterEdge(terrain, resolver.Beauty(terrain), world);
        var beauty = resolver.Beauty(terrain).At(edge.X, edge.Z);
        output.WriteLine($"départ {edge}, beauté {beauty:F2}");

        double Spread(Archetype profile)
        {
            var points = new List<Point2>();
            for (var seed = 1; seed <= 200; seed++)
            {
                points.Add(resolver.Resolve(new ShotRequest(edge, terrain.Hole, 1, Level, profile), terrain, new DeterministicRng((ulong)seed)).Ball);
            }

            var mx = points.Average(p => p.X);
            var mz = points.Average(p => p.Z);
            return Math.Sqrt(points.Average(p => ((p.X - mx) * (p.X - mx)) + ((p.Z - mz) * (p.Z - mz))));
        }

        var artist = Spread(Archetype.Artist);
        var neutral = Spread(Archetype.Neutral);
        output.WriteLine($"dispersion : artiste {artist:F2} m, neutre {neutral:F2} m");
        Assert.True(artist < neutral);
    }

    /// <summary>
    /// Point visé : celui du golfeur neutre, à 5 m près sur la carte de jeu, depuis une cellule sur trois du proto et du dogleg,
    /// en été et au printemps (fleurs), par trois vents, au niveau 1,4.
    /// </summary>
    [Fact]
    public void Artist_NeverAimsMoreThanFiveMetersFartherThanNeutral()
    {
        var holes = new[] { TerrainTests.HolePath(), Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "dogleg-par4.json") };
        Vec3[] winds = [new(0.0, 0.0, 0.0), new(-4.0, 0.0, 3.0), new(5.0, 0.0, -2.0)];
        var worst = double.MinValue;
        var where = "";
        var gate = new object();
        foreach (var path in holes)
        {
            var terrain = HoleLoader.Load(path, 2);
            var world = new World(1, terrain);
            var map = Pathing.PlayMap.Build(terrain, world.Trees, terrain.Hole);
            var starts = new List<Point2>();
            for (var cz = 0; cz < terrain.CellsZ; cz += 3)
            {
                for (var cx = 0; cx < terrain.CellsX; cx += 3)
                {
                    if (terrain.GetCellSurface(cx, cz) is Surface.Fairway or Surface.Rough or Surface.Tee)
                    {
                        starts.Add(terrain.CellCenter(cx, cz));
                    }
                }
            }

            foreach (var season in new[] { Trees.Season.Summer, Trees.Season.Spring })
            {
                foreach (var wind in winds)
                {
                    var resolver = new PhysicalShotResolver { Trees = world.Trees, Season = season, Wind = wind, BeautySources = world.BeautySources };
                    Parallel.ForEach(starts, from =>
                    {
                        resolver.Resolve(new ShotRequest(from, terrain.Hole, 1, 1.4, Archetype.Artist), terrain, new DeterministicRng(1), out var a);
                        resolver.Resolve(new ShotRequest(from, terrain.Hole, 1, 1.4), terrain, new DeterministicRng(1), out var n);
                        var gap = map.DistanceAt(a!.Value.Landing.X, a.Value.Landing.Z, terrain.Hole) - map.DistanceAt(n!.Value.Landing.X, n.Value.Landing.Z, terrain.Hole);
                        lock (gate)
                        {
                            if (gap > worst)
                            {
                                worst = gap;
                                where = $"{Path.GetFileName(path)} {from} {season} vent {wind}";
                            }
                        }
                    });
                }
            }
        }

        output.WriteLine($"écart du point visé de l'artiste au plus : {worst:F2} m ({where})");
        Assert.True(worst <= GolferProfile.BeautyCost + 1e-9, $"{worst:F2} m ({where})");
    }

    [Fact]
    public void Cautious_AimsAtTheFlag_OnAShortApproach()
    {
        var (terrain, world) = Dogleg();
        var resolver = new PhysicalShotResolver { Trees = world.Trees, Season = world.Season };
        double Left(Archetype profile)
        {
            var total = 0.0;
            var n = 0;
            for (var cz = 0; cz < terrain.CellsZ; cz++)
            {
                for (var cx = 0; cx < terrain.CellsX; cx++)
                {
                    var from = terrain.CellCenter(cx, cz);
                    var d = Math.Sqrt(((from.X - terrain.Hole.X) * (from.X - terrain.Hole.X)) + ((from.Z - terrain.Hole.Z) * (from.Z - terrain.Hole.Z)));
                    if (d is < 10.0 or > 25.0 || terrain.GetCellSurface(cx, cz) is not (Surface.Fairway or Surface.Rough))
                    {
                        continue;
                    }

                    resolver.Resolve(new ShotRequest(from, terrain.Hole, 1, Level, profile), terrain, new DeterministicRng(1), out var plan);
                    var l = plan!.Value.Landing;
                    total += Math.Sqrt(((l.X - terrain.Hole.X) * (l.X - terrain.Hole.X)) + ((l.Z - terrain.Hole.Z) * (l.Z - terrain.Hole.Z)));
                    n++;
                }
            }

            Assert.True(n > 0);
            return total / n;
        }

        var cautious = Left(Archetype.Cautious);
        var neutral = Left(Archetype.Neutral);
        output.WriteLine($"approche de 10 à 25 m, distance prévue au drapeau : prudent {cautious:F1} m, neutre {neutral:F1} m");
        Assert.True(cautious < 3.0, $"{cautious:F1} m");
    }

    [Fact]
    public void Planning_StaysDeterministic_ForEveryProfile()
    {
        var (terrain, world) = Dogleg();
        foreach (var a in Enum.GetValues<Archetype>())
        {
            var r1 = new PhysicalShotResolver { Trees = world.Trees, Season = world.Season };
            var r2 = new PhysicalShotResolver { Trees = world.Trees, Season = world.Season };
            r1.Resolve(new ShotRequest(terrain.Tee, terrain.Hole, 0, Level, a), terrain, new DeterministicRng(1), out var p1);
            r2.Resolve(new ShotRequest(terrain.Tee, terrain.Hole, 0, Level, a), terrain, new DeterministicRng(99), out var p2);
            Assert.Equal(p1, p2);
        }
    }
}
