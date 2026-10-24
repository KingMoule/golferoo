using Golferoo.Sim.Pathing;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Tests;

public class PathingTests
{
    /// <summary>Grille plate de fairway, cellules de 2 m.</summary>
    internal static TerrainGrid Flat(int nx, int nz, double cell = 2.0)
    {
        var t = new TerrainGrid(new HeightField(nx, nz, cell), "test", new Point2(cell, cell), new Point2(cell * (nx - 2), cell), new Point2(cell * (nx - 2), cell));
        for (var z = 0; z < nz; z++)
        {
            for (var x = 0; x < nx; x++)
            {
                t.PaintCell(x, z, Surface.Fairway);
            }
        }

        return t;
    }

    [Fact]
    public void Distances_AreExactStraightAndDiagonal()
    {
        var t = Flat(10, 10);

        var f = FlowField.Build(t, 0, 0);

        Assert.Equal(0f, f.DistanceAt(0, 0));
        Assert.Equal(10f, f.DistanceAt(5, 0), 4);
        Assert.Equal(10f, f.DistanceAt(0, 5), 4);
        Assert.Equal(3 * 2 * Math.Sqrt(2), f.DistanceAt(3, 3), 3);
        Assert.Equal((2 * 2 * Math.Sqrt(2)) + 4, f.DistanceAt(4, 2), 3);
    }

    [Fact]
    public void Distance_UsesSurfaceWalkCost()
    {
        var t = Flat(10, 1);
        for (var x = 3; x < 10; x++)
        {
            t.PaintCell(x, 0, Surface.Rough);
        }

        var f = FlowField.Build(t, 0, 0);

        // Du centre de (3) au centre de (2) : demi-cellule de rough (1,4) et demi de fairway (1,0), soit 1,2 par cellule.
        Assert.Equal(2f + 2f + (2f * 1.2f), f.DistanceAt(3, 0), 3);
        Assert.True(f.DistanceAt(9, 0) > 9 * 2 * 1.0);
    }

    [Fact]
    public void WaterWall_IsGoneAround()
    {
        var t = Flat(11, 10);
        for (var z = 0; z < 9; z++)
        {
            t.PaintCell(5, z, Surface.Water);
        }

        var f = FlowField.Build(t, 8, 0);

        var straight = 6 * 2f;
        Assert.True(f.DistanceAt(2, 0) > straight + 10f, "il faut contourner par la brèche du bas");
        Assert.True(f.IsReachable(2, 0));

        // On suit les pas : jamais dans l'eau, jamais en coupant un coin bloqué.
        var (cx, cz) = (2, 0);
        for (var guard = 0; guard < 100 && (cx != 8 || cz != 0); guard++)
        {
            Assert.True(f.TryNextCell(cx, cz, out var nx, out var nz));
            Assert.True(t.IsWalkable(nx, nz));
            if (nx != cx && nz != cz)
            {
                Assert.True(t.IsWalkable(nx, cz) && t.IsWalkable(cx, nz), "coin bloqué coupé");
            }

            (cx, cz) = (nx, nz);
        }

        Assert.Equal((8, 0), (cx, cz));
    }

    [Fact]
    public void FullWall_MakesOtherSideUnreachable()
    {
        var t = Flat(11, 6);
        for (var z = 0; z < 6; z++)
        {
            t.PaintCell(5, z, Surface.Trees);
        }

        var f = FlowField.Build(t, 8, 2);

        Assert.False(f.IsReachable(2, 2));
        Assert.True(f.IsReachable(7, 2));
    }

    [Fact]
    public void DiagonalStep_DoesNotCutBlockedCorner()
    {
        var t = Flat(3, 3);
        t.PaintCell(1, 0, Surface.Water);
        t.PaintCell(0, 1, Surface.Water);

        var f = FlowField.Build(t, 0, 0);

        Assert.False(f.IsReachable(1, 1), "(0,0) n'est accessible que par la diagonale, qui coupe un coin bloqué");
    }

    [Fact]
    public void Steps_NeverCutCornersOnRealHole()
    {
        var t = HoleLoader.Load(TerrainTests.HolePath(), 2);
        var f = FlowField.Build(t, t.CellXOf(t.Hole.X), t.CellZOf(t.Hole.Z));

        var reachable = 0;
        for (var cz = 0; cz < t.CellsZ; cz++)
        {
            for (var cx = 0; cx < t.CellsX; cx++)
            {
                if (!f.TryNextCell(cx, cz, out var nx, out var nz))
                {
                    continue;
                }

                reachable++;
                Assert.True(t.IsWalkable(cx, cz) && t.IsWalkable(nx, nz));
                if (nx != cx && nz != cz)
                {
                    Assert.True(t.IsWalkable(nx, cz) && t.IsWalkable(cx, nz));
                }
            }
        }

        Assert.True(reachable > 1000);
    }

    [Fact]
    public void BlockedDestination_FallsBackToNearestWalkable()
    {
        var t = Flat(10, 10);
        t.PaintCell(5, 5, Surface.Water);

        var f = FlowField.Build(t, 5, 5);

        Assert.True(t.IsWalkable(f.DestX, f.DestZ));
        Assert.True(Math.Abs(f.DestX - 5) + Math.Abs(f.DestZ - 5) == 1);
        Assert.Equal((5, 4), (f.DestX, f.DestZ)); // à distance égale, la première en ordre ligne par ligne
    }

    [Fact]
    public void Cache_ReusesInstanceThenInvalidatesAfterPaint()
    {
        var t = Flat(10, 10);
        var cache = new FlowFieldCache(t);

        var a = cache.Get(new Point2(3, 3));
        var b = cache.Get(new Point2(3.5, 2.5));
        Assert.Same(a, b);

        t.PaintCell(6, 6, Surface.Water);
        var c = cache.Get(new Point2(3, 3));
        Assert.NotSame(a, c);
        Assert.False(c.IsReachable(6, 6));
        Assert.Same(c, cache.Get(new Point2(3, 3)));

        t.SetCornerHeight(1, 1, 5.0);
        Assert.NotSame(c, cache.Get(new Point2(3, 3)));
    }

    [Fact]
    public void Cache_EvictsOldestBeyondCapacity()
    {
        var t = Flat(10, 10);
        var cache = new FlowFieldCache(t, capacity: 3);

        var first = cache.Get(0, 0);
        cache.Get(1, 0);
        cache.Get(2, 0);
        cache.Get(3, 0);

        Assert.Equal(3, cache.Count);
        Assert.NotSame(first, cache.Get(0, 0));
    }

    [Fact]
    public void Walker_GoesFromTeeToGreenWithoutEnteringBlockedCell()
    {
        foreach (var factor in new[] { 2, 3, 4 })
        {
            var t = HoleLoader.Load(TerrainTests.HolePath(), factor);
            var field = FlowField.Build(t, t.CellXOf(t.Green.X), t.CellZOf(t.Green.Z));
            var pos = t.Tee;
            var arrived = false;
            var previous = pos;
            for (var tick = 0; tick < 20 * 600 && !arrived; tick++)
            {
                var r = FlowWalker.Advance(t, field, ref pos, t.Green, World.TickSeconds);
                Assert.NotEqual(WalkResult.Stuck, r);
                Assert.True(t.IsWalkable(t.CellXOf(pos.X), t.CellZOf(pos.Z)), $"golfeur en terrain bloqué à {pos} (facteur {factor})");
                Assert.True(Math.Abs(pos.X - previous.X) + Math.Abs(pos.Z - previous.Z) < 0.2);
                previous = pos;
                arrived = r == WalkResult.Arrived;
            }

            Assert.True(arrived, $"n'arrive pas au green (facteur {factor})");
        }
    }

    [Fact]
    public void Walker_PathIsSmooth_NoSharpTurnsOnOpenFairway()
    {
        var t = Flat(60, 60);
        var field = FlowField.Build(t, 55, 30);
        var pos = new Point2(5, 15);
        double lastX = 0, lastZ = 0;
        var worst = 1.0;
        for (var i = 0; i < 400; i++)
        {
            var before = pos;
            FlowWalker.Advance(t, field, ref pos, t.CellCenter(55, 30), World.TickSeconds);
            var mx = pos.X - before.X;
            var mz = pos.Z - before.Z;
            var len = Math.Sqrt((mx * mx) + (mz * mz));
            if (i > 0 && len > 0)
            {
                worst = Math.Min(worst, ((mx * lastX) + (mz * lastZ)) / len / Math.Sqrt((lastX * lastX) + (lastZ * lastZ)));
            }

            (lastX, lastZ) = (mx, mz);
        }

        Assert.True(worst > 0.99, $"virage trop brusque entre deux ticks (cos = {worst})");
    }
}
