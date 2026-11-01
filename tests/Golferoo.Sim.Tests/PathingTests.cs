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
    public void Cache_ReusesInstanceThenInvalidatesAfterPaintNotAfterHeight()
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

        // Les coûts ne dépendent que des surfaces : un relief seul ne reconstruit rien.
        t.SetCornerHeight(1, 1, 5.0);
        Assert.Same(c, cache.Get(new Point2(3, 3)));
    }

    /// <summary>
    /// Banc C12 : un champ borné par son origine couvre exactement les cellules du champ complet à au plus sa borne (1,5 fois la distance de l'origine,
    /// plus une marge), avec les mêmes distances et les mêmes pas ; il s'arrête bien avant la carte entière.
    /// </summary>
    [Fact]
    public void BoundedField_CoversExactlyTheFullFieldUpToItsBound()
    {
        var t = HoleLoader.Load(TerrainTests.HolePath(), 2);
        var costs = FlowField.BuildCosts(t);
        var (hx, hz) = (t.CellXOf(t.Hole.X), t.CellZOf(t.Hole.Z));
        var full = FlowField.Build(t, costs, hx, hz);
        var origin = (t.CellZOf(t.Hole.Z) * t.CellsX) + t.CellXOf(t.Hole.X) - 20;
        var bounded = FlowField.Build(t, costs, hx, hz, origin);

        Assert.True(full.IsComplete);
        Assert.False(bounded.IsComplete);
        Assert.Equal(origin, bounded.Origin);
        Assert.True(bounded.Bound >= full.DistanceAt(origin % t.CellsX, origin / t.CellsX) * FlowField.BoundFactor);
        var covered = 0;
        var reachable = 0;
        for (var z = 0; z < t.CellsZ; z++)
        {
            for (var x = 0; x < t.CellsX; x++)
            {
                reachable += full.IsReachable(x, z) ? 1 : 0;
                if (full.DistanceAt(x, z) <= bounded.Bound)
                {
                    covered++;
                    Assert.Equal(full.DistanceAt(x, z), bounded.DistanceAt(x, z));
                    Assert.Equal(full.TryNextCell(x, z, out var fx, out var fz), bounded.TryNextCell(x, z, out var bx, out var bz));
                    Assert.Equal((fx, fz), (bx, bz));
                }
                else
                {
                    Assert.False(bounded.IsReachable(x, z));
                    Assert.False(bounded.TryNextCell(x, z, out _, out _));
                }
            }
        }

        Assert.InRange(covered, 1, reachable / 2);
    }

    /// <summary>
    /// Un marcheur fait calculer un champ borné par sa cellule ; un autre, hors de sa couverture, le fait refaire complet (sous le budget du tick,
    /// sinon il attend son tour) ; <see cref="FlowFieldCache.Get(int, int)"/> rend toujours un champ complet, qui ne lit jamais « inatteignable » une cellule reliée.
    /// </summary>
    [Fact]
    public void Cache_BoundedFieldForWalkers_WidenedWhenNeeded_CompleteForGet()
    {
        var t = Flat(200, 20);
        var cache = new FlowFieldCache(t);
        var dest = t.CellCenter(10, 10);
        var near = t.CellCenter(14, 10);
        var far = t.CellCenter(190, 10);

        cache.BeginTick();
        var bounded = cache.TryGet(dest, near)!;
        Assert.False(bounded.IsComplete);
        Assert.False(bounded.IsReachable(190, 10));
        Assert.Same(bounded, cache.TryGet(dest, t.CellCenter(12, 10))); // couvert : le même champ, sans calcul

        // Hors de la couverture, le budget du tick déjà pris : il attend son tour, complet.
        Assert.Null(cache.TryGet(dest, far));
        Assert.Equal((10 * 200) + 10, Assert.Single(cache.Pending).Key);
        Assert.Equal(-1, cache.Pending[0].Origin);
        cache.BeginTick();
        var widened = cache.TryGet(dest, far)!;
        Assert.True(widened.IsComplete);
        Assert.True(widened.IsReachable(190, 10));

        // Le tick suivant, un autre champ borné, que Get refait complet à sa place.
        var other = t.CellCenter(100, 5);
        cache.BeginTick();
        Assert.False(cache.TryGet(other, t.CellCenter(102, 5))!.IsComplete);
        var complete = cache.Get(other);
        Assert.True(complete.IsComplete);
        Assert.True(complete.IsReachable(0, 0));
        Assert.Equal(2, cache.Count);
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
                // Pas de saut : au plus deux pas de marche par tick (le chemin se marche plus vite que le fairway).
                Assert.True(Math.Abs(pos.X - previous.X) + Math.Abs(pos.Z - previous.Z) < 2.0 * FlowWalker.BaseSpeed * World.TickSeconds);
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
