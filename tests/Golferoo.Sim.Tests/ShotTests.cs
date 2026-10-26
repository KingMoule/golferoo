using Golferoo.Sim.Golfers;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Tests;

public class ShotTests
{
    private const double None = 0.0;

    /// <summary>Terrain de <paramref name="cells"/> × <paramref name="cells"/> cellules de 4 m, toutes de la même surface.</summary>
    internal static TerrainGrid Uniform(Surface surface, Func<double, double, double>? height = null, int cells = 150, Point2? hole = null)
    {
        const double cell = 4.0;
        var field = new HeightField(cells, cells, cell);
        for (var j = 0; j <= cells; j++)
        {
            for (var i = 0; i <= cells; i++)
            {
                field.SetCornerHeight(i, j, height?.Invoke(i * cell, j * cell) ?? 0.0);
            }
        }

        var size = cells * cell;
        var h = hole ?? new Point2(size - 8, 8);
        var grid = new TerrainGrid(field, "test", new Point2(8, size / 2), h, h);
        for (var cz = 0; cz < cells; cz++)
        {
            for (var cx = 0; cx < cells; cx++)
            {
                grid.PaintCell(cx, cz, surface);
            }
        }

        return grid;
    }

    private static Vec3 Launch(Club club, double force = 1.0) =>
        new(club.LaunchCos * club.Speed * force, club.LaunchSin * club.Speed * force, 0);

    private static double Dist(Point2 a, Point2 b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));

    private static TerrainGrid Proto() => HoleLoader.Load(TerrainTests.HolePath(), 2);

    // Critère 1
    [Fact]
    public void Flight_NoDrag_MatchesAnalyticRangeAndApex()
    {
        var terrain = Uniform(Surface.Fairway);
        const double v = 40.0;
        var angle = 30.0 * Math.PI / 180.0;
        var vel = new Vec3(v * Math.Cos(angle), v * Math.Sin(angle), 0);

        var sim = BallSimulator.Run(terrain, new Point2(100, 300), vel, rolling: false, drag: None, recordPath: false);

        var range = v * v * Math.Sin(2 * angle) / BallSimulator.Gravity;
        var apex = v * v * Math.Sin(angle) * Math.Sin(angle) / (2 * BallSimulator.Gravity);
        Assert.True(sim.Landed);
        Assert.InRange(sim.FirstLanding.X - 100, range * 0.99, range * 1.01);
        Assert.InRange(sim.MaxHeight, apex * 0.99, apex * 1.01);
    }

    [Fact]
    public void Flight_WithDrag_IsShorter()
    {
        var terrain = Uniform(Surface.Fairway);
        var vel = new Vec3(30, 20, 0);

        var vacuum = BallSimulator.Run(terrain, new Point2(100, 300), vel, false, None, false);
        var air = BallSimulator.Run(terrain, new Point2(100, 300), vel, false, BallSimulator.DefaultDrag, false);

        Assert.True(air.FirstLanding.X < vacuum.FirstLanding.X - 5.0);
        Assert.True(air.MaxHeight < vacuum.MaxHeight);
    }

    public static TheoryData<string> ClubNames => new(Clubs.Full.Select(c => c.Name));

    [Theory]
    [MemberData(nameof(ClubNames))]
    public void Club_TotalRangeOnFlatFairway_IsWithinFifteenPercentOfTable(string name)
    {
        var club = Clubs.Full.Single(c => c.Name == name);
        var terrain = Uniform(Surface.Fairway);
        var start = new Point2(30, 300);

        var sim = BallSimulator.Run(terrain, start, Launch(club), rolling: false, BallSimulator.DefaultDrag, recordPath: false);

        var total = Dist(start, sim.Shot.Ball);
        Assert.InRange(total, club.TargetRange * 0.85, club.TargetRange * 1.15);
    }

    // Critère 2
    [Fact]
    public void DroppedBall_BouncesLowerAndLowerThenStops()
    {
        var terrain = Uniform(Surface.Fairway);

        var sim = BallSimulator.Run(terrain, new Point2(100, 300), new Vec3(1.5, 0, 0), false, BallSimulator.DefaultDrag, true, startHeight: 5.0);

        var path = sim.Shot.Trajectory!;
        var peaks = new List<double>();
        for (var i = 1; i < path.Count - 1; i++)
        {
            if (path[i].Y > path[i - 1].Y && path[i].Y >= path[i + 1].Y)
            {
                peaks.Add(path[i].Y);
            }
        }

        Assert.True(peaks.Count >= 2, "la balle doit rebondir plusieurs fois");
        for (var i = 1; i < peaks.Count; i++)
        {
            Assert.True(peaks[i] < peaks[i - 1]);
        }

        Assert.True(sim.Duration < 30.0);
        Assert.Equal(path[^1], path[^1] with { Y = terrain.Sample(path[^1].X, path[^1].Z) });
    }

    [Fact]
    public void BallOnSlopedGreen_RollsDownhill_AndStaysOnFlatGreen()
    {
        var slope = Uniform(Surface.Green, (x, _) => 0.10 * Math.Max(0.0, 300 - x));
        var flat = Uniform(Surface.Green);
        var start = new Point2(200, 300);
        var rest = new Vec3(0, 0, 0);

        var down = BallSimulator.Run(slope, start, rest, rolling: true, BallSimulator.DefaultDrag, recordPath: true);
        var still = BallSimulator.Run(flat, start, rest, rolling: true, BallSimulator.DefaultDrag, recordPath: true);

        Assert.True(down.Shot.Ball.X > start.X + 20.0, $"la balle doit descendre la pente (x = {down.Shot.Ball.X})");
        Assert.True(down.Shot.Trajectory![5].X > start.X, "la balle part dans le sens de la pente");
        Assert.Equal(start, still.Shot.Ball);
    }

    [Fact]
    public void BallInBunker_StopsWithinOneMeter()
    {
        var terrain = Uniform(Surface.Bunker);

        var sim = BallSimulator.Run(terrain, new Point2(100, 300), new Vec3(18, 0, 0), false, BallSimulator.DefaultDrag, false, startHeight: 3.0);

        Assert.True(sim.Landed);
        Assert.True(Dist(sim.FirstLanding, sim.Shot.Ball) < 1.0, $"roulé {Dist(sim.FirstLanding, sim.Shot.Ball):F2} m");
    }

    [Fact]
    public void Ball_NeverGoesBelowTerrain_OnTheHole()
    {
        var terrain = Proto();
        var resolver = new PhysicalShotResolver();
        var rng = new DeterministicRng(5);
        var ball = terrain.Tee;
        var worst = 0.0;
        for (var shot = 0; shot < 8 && Dist(ball, terrain.Hole) > 0.1; shot++)
        {
            var result = resolver.Resolve(new ShotRequest(ball, terrain.Hole, shot), terrain, rng);
            foreach (var p in result.Trajectory!)
            {
                worst = Math.Max(worst, terrain.Sample(p.X, p.Z) - p.Y);
            }

            ball = result.Ball;
        }

        Assert.True(worst <= 0.01, $"balle sous le terrain de {worst:F3} m");
    }

    // Critère 3
    [Fact]
    public void ShotIntoPond_GivesOnePenaltyAndDropsOnDrySideNearTee()
    {
        var terrain = Proto();
        var resolver = new PhysicalShotResolver();
        var start = new Point2(190, 84);
        var pond = new Point2(248, 104);
        var d = Dist(start, pond);
        var dir = new Point2((pond.X - start.X) / d, (pond.Z - start.Z) / d);

        var result = resolver.Execute(start, Clubs.Wedge, dir.X, dir.Z, Math.Sqrt(d / Clubs.Wedge.TargetRange) * 0.95, terrain, new DeterministicRng(1));

        Assert.Equal(ShotOutcome.Water, result.Outcome);
        Assert.Equal(1, result.PenaltyStrokes);
        var cx = terrain.CellXOf(result.Ball.X);
        var cz = terrain.CellZOf(result.Ball.Z);
        Assert.True(terrain.IsWalkable(cx, cz));
        Assert.NotEqual(Surface.Water, terrain.GetCellSurface(cx, cz));
        Assert.True(Dist(result.Ball, terrain.Tee) < Dist(pond, terrain.Tee), "le drop doit être du côté du tee");
        Assert.Equal(result.Ball.X, result.Trajectory![^1].X);
    }

    [Fact]
    public void ShotOutOfBounds_GivesPenaltyAndReplaysFromPreviousSpot()
    {
        var terrain = Proto();
        var resolver = new PhysicalShotResolver();
        var start = new Point2(12, 68);

        var result = resolver.Execute(start, Clubs.Driver, -1.0, 0.0, 1.0, terrain, new DeterministicRng(1));

        Assert.Equal(ShotOutcome.OutOfBounds, result.Outcome);
        Assert.Equal(1, result.PenaltyStrokes);
        Assert.Equal(start, result.Ball);
    }

    // Critère 4
    private static ShotResult Putt(TerrainGrid terrain, Point2 ball, ulong seed) =>
        new PhysicalShotResolver().Resolve(new ShotRequest(ball, terrain.Hole, 0), terrain, new DeterministicRng(seed));

    [Fact]
    public void Putt_FromThreeMetersOnFlatGreen_IsHoledAtLeast90PercentOf100Seeds()
    {
        var terrain = Uniform(Surface.Green, hole: new Point2(300, 300));
        var holed = 0;
        for (ulong seed = 0; seed < 100; seed++)
        {
            holed += Putt(terrain, new Point2(297, 300), seed).Holed ? 1 : 0;
        }

        Assert.True(holed >= 90, $"{holed} putts rentrés sur 100");
    }

    [Fact]
    public void Putt_FromTenMeters_TakesAtMostThreePuttsIn95PercentOf100Seeds()
    {
        var terrain = Uniform(Surface.Green, hole: new Point2(300, 300));
        var good = 0;
        for (ulong seed = 0; seed < 100; seed++)
        {
            var rng = new DeterministicRng(seed);
            var resolver = new PhysicalShotResolver();
            var ball = new Point2(290, 300);
            var putts = 0;
            ShotResult r;
            do
            {
                r = resolver.Resolve(new ShotRequest(ball, terrain.Hole, putts), terrain, rng);
                ball = r.Ball;
                putts++;
            }
            while (!r.Holed && putts < 10);
            good += putts <= 3 ? 1 : 0;
        }

        Assert.True(good >= 95, $"{good} trous en 3 putts ou moins sur 100");
    }

    [Fact]
    public void Putt_OnSlopedGreen_StillGoesInOftenEnough()
    {
        var terrain = Uniform(Surface.Green, (x, z) => 0.03 * (z - 300), hole: new Point2(300, 300));
        var holed = 0;
        for (ulong seed = 0; seed < 100; seed++)
        {
            holed += Putt(terrain, new Point2(297, 300), seed).Holed ? 1 : 0;
        }

        Assert.True(holed >= 50, $"{holed} putts rentrés sur 100 en pente");
    }

    // Critère 5
    private static (int Strokes, bool PickedUp)[] PlayGame(ulong seed)
    {
        var world = new World(seed, Proto());
        var group = world.SpawnGroup();
        for (var i = 0; i < 20 * 3600 && !group.Finished; i++)
        {
            world.Step();
        }

        Assert.True(group.Finished, $"graine {seed} : partie non finie");
        return group.Golfers.Select(g => (g.Strokes, g.PickedUp)).ToArray();
    }

    [Fact]
    public void FullGame_Physics_Over50Seeds_AverageStrokesAndNoPickUp()
    {
        var games = new (int Strokes, bool PickedUp)[50][];
        Parallel.For(0, 50, i => games[i] = PlayGame((ulong)i));
        var all = games.SelectMany(g => g).ToArray();

        var average = all.Average(g => g.Strokes);
        Assert.InRange(average, 3.5, 7.0);
        Assert.DoesNotContain(all, g => g.PickedUp);
    }

    // Critère 6
    private static (ulong Checksum, Vec3 Ball) RunTwoGroups(ulong seed)
    {
        var world = new World(seed, Proto());
        world.SpawnGroup();
        world.SpawnGroup();
        world.Step(3000);
        return (world.Checksum(), world.Snapshot().Golfers[0].Ball3D);
    }

    [Fact]
    public void FullGame_SameSeedSameChecksumAndBall_DifferentSeedDiffers()
    {
        Assert.Equal(RunTwoGroups(8), RunTwoGroups(8));
        Assert.NotEqual(RunTwoGroups(8).Checksum, RunTwoGroups(9).Checksum);
    }

    [Fact]
    public void Shot_SameSeedSameTrajectory()
    {
        var terrain = Proto();
        var resolver = new PhysicalShotResolver();
        var request = new ShotRequest(terrain.Tee, terrain.Hole, 0);

        var a = resolver.Resolve(request, terrain, new DeterministicRng(4));
        var b = resolver.Resolve(request, terrain, new DeterministicRng(4));
        var c = resolver.Resolve(request, terrain, new DeterministicRng(5));

        Assert.Equal(a.Trajectory!, b.Trajectory!);
        Assert.NotEqual(a.Trajectory!, c.Trajectory!);
    }

    // Critère 7
    [Fact]
    public void BallInFlight_SnapshotShowsHeightAndStrokesCountOnlyAtRest()
    {
        var world = new World(7, Proto());
        var group = world.SpawnGroup();
        var shooter = group.Golfers[0];
        var snap = world.Snapshot();
        var sawFlightAboveGround = false;
        var flightTicks = 0;

        for (var i = 0; i < 20 * 600 && shooter.Strokes == 0; i++)
        {
            world.Step();
            if (shooter.State != GolferState.BallInFlight)
            {
                continue;
            }

            flightTicks++;
            Assert.Equal(0, shooter.Strokes);
            snap = world.Snapshot(snap);
            var view = snap.Golfers.Single(g => g.Id == shooter.Id);
            if (view.Ball3D.Y > world.Terrain!.Sample(view.Ball3D.X, view.Ball3D.Z) + 1.0)
            {
                sawFlightAboveGround = true;
            }
        }

        Assert.True(flightTicks > 20, "le drive doit voler plus d'une seconde");
        Assert.True(sawFlightAboveGround);
        Assert.Equal(1, shooter.Strokes);
        Assert.NotEqual(GolferState.BallInFlight, shooter.State);
    }
}
