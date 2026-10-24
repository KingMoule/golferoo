using System.Diagnostics;
using Golferoo.Sim.Golfers;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Tests;

public class GolferTests
{
    private static World NewWorld(ulong seed, int factor = 2) =>
        new(seed, HoleLoader.Load(TerrainTests.HolePath(), factor));

    private sealed class NeverHoleResolver : Golferoo.Sim.Shots.IShotResolver
    {
        public Golferoo.Sim.Shots.ShotResult Resolve(in Golferoo.Sim.Shots.ShotRequest request, TerrainGrid terrain, DeterministicRng rng) =>
            new(request.Ball, Holed: false);
    }

    [Fact]
    public void PickUp_AfterMaxStrokes_IsNotCountedAsHoled()
    {
        var world = new World(3, HoleLoader.Load(TerrainTests.HolePath(), 2), new NeverHoleResolver());
        var group = world.SpawnGroup();

        for (var ticks = 0; !group.Finished && ticks < 20 * 3600; ticks++)
        {
            world.Step();
        }

        Assert.True(group.Finished);
        foreach (var g in group.Golfers)
        {
            Assert.Equal(World.MaxStrokes, g.Strokes);
            Assert.False(g.Holed);
            Assert.True(g.PickedUp);
        }
    }

    [Fact]
    public void FullGame_PairPlaysHoleAndExits()
    {
        var world = NewWorld(7);
        var group = world.SpawnGroup();
        var watch = Stopwatch.StartNew();

        var ticks = 0;
        while (!group.Finished && ticks < 20 * 3600)
        {
            world.Step();
            ticks++;
        }

        watch.Stop();
        Assert.True(group.Finished, "la paire n'a pas fini en une heure simulée");
        Assert.True(watch.Elapsed.TotalSeconds < 5, $"partie trop lente : {watch.Elapsed.TotalSeconds:F2} s");
        foreach (var g in group.Golfers)
        {
            Assert.InRange(g.Strokes, 3, 8);
            Assert.True(g.Holed);
            Assert.Equal(GolferState.Done, g.State);
            Assert.True(Math.Abs(g.Position.X - world.ExitPoint.X) < 4 && Math.Abs(g.Position.Z - world.ExitPoint.Z) < 4);
        }
    }

    [Fact]
    public void FullGame_GolfersNeverStandOnBlockedCells()
    {
        var world = NewWorld(11, factor: 4);
        var group = world.SpawnGroup();
        var t = world.Terrain!;
        for (var i = 0; i < 20 * 3600 && !group.Finished; i++)
        {
            world.Step();
            foreach (var g in group.Golfers)
            {
                Assert.True(t.IsWalkable(t.CellXOf(g.Position.X), t.CellZOf(g.Position.Z)));
                Assert.True(t.IsWalkable(t.CellXOf(g.Ball.X), t.CellZOf(g.Ball.Z)));
            }
        }

        Assert.True(group.Finished);
    }

    [Fact]
    public void FarthestFromHolePlaysFirst()
    {
        var world = NewWorld(3);
        var group = world.SpawnGroup();
        var order = new List<int>();
        var lastStrokes = new int[2];
        for (var i = 0; i < 20 * 600 && order.Count < 2; i++)
        {
            world.Step();
            for (var k = 0; k < 2; k++)
            {
                if (group.Golfers[k].Strokes != lastStrokes[k])
                {
                    lastStrokes[k] = group.Golfers[k].Strokes;
                    order.Add(k);
                }
            }
        }

        // Au tee les deux sont à égalité (même balle) : le premier de la liste joue, puis le plus loin du trou.
        Assert.Equal(2, order.Count);
        Assert.Equal(0, order[0]);
        Assert.Equal(1, order[1]);
    }

    [Fact]
    public void SecondGroup_WaitsUntilFirstHasLeftTheGreen()
    {
        var world = NewWorld(5);
        var first = world.SpawnGroup();
        var second = world.SpawnGroup();

        var checkedOnce = false;
        for (var i = 0; i < 20 * 3600 && !first.Finished; i++)
        {
            world.Step();
            if (!first.Finished)
            {
                Assert.False(second.Started);
                Assert.All(second.Golfers, g => Assert.Equal(0, g.Strokes));
                checkedOnce = true;
            }
        }

        Assert.True(checkedOnce);
        for (var i = 0; i < 20 * 3600 && !second.Finished; i++)
        {
            world.Step();
        }

        Assert.True(second.Finished);
    }

    private static ulong Run(ulong seed)
    {
        var world = NewWorld(seed);
        world.SpawnGroup();
        world.SpawnGroup();
        world.SetLoadMode(200);
        world.Step(2000);
        return world.Checksum();
    }

    [Fact]
    public void SameSeed_SameChecksum_DifferentSeed_Differs()
    {
        Assert.Equal(Run(42), Run(42));
        Assert.NotEqual(Run(42), Run(43));
    }

    [Fact]
    public void LoadMode_200GolfersFor1000Ticks_NeverOnWaterOrTrees()
    {
        var world = NewWorld(9, factor: 4);
        world.SetLoadMode(200);
        var t = world.Terrain!;
        var start = world.Roamers.Select(g => g.Position).ToArray();

        for (var i = 0; i < 1000; i++)
        {
            world.Step();
            foreach (var g in world.Roamers)
            {
                Assert.True(t.IsWalkable(t.CellXOf(g.Position.X), t.CellZOf(g.Position.Z)), $"golfeur {g.Id} en terrain bloqué au tick {i}");
            }
        }

        Assert.Equal(200, world.Roamers.Count);
        var moved = world.Roamers.Select((g, i) => g.Position != start[i]).Count(m => m);
        Assert.True(moved > 150, "les marcheurs doivent bouger");
    }

    [Fact]
    public void Snapshot_ListsGolfersAndReusesBuffer()
    {
        var world = NewWorld(1);
        world.SpawnGroup();
        world.SetLoadMode(10);
        world.Step(5);

        var snap = world.Snapshot();
        var again = world.Snapshot(snap);

        Assert.Same(snap, again);
        Assert.Equal(12, snap.Golfers.Count);
        Assert.Equal(5, snap.Tick);
        Assert.Equal(GolferState.Roaming, snap.Golfers[^1].State);
    }

    [Fact]
    public void SpawnGroup_WithoutTerrain_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new World(1).SpawnGroup());
    }
}
