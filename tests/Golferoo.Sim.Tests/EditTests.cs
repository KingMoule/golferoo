using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Tests;

public class EditTests
{
    private static World NewWorld(ulong seed = 5) => new(seed, PathingTests.Flat(30, 30));

    [Fact]
    public void PaintWater_UnderGolfer_RelocatesToWalkableCell()
    {
        var world = NewWorld();
        world.SetLoadMode(3);
        var p = world.Roamers[0].Position;

        world.PaintSurface(p, 3.0, Surface.Water);
        Assert.Equal(1, world.PendingEdits);
        world.Step();

        var t = world.Terrain!;
        Assert.Equal(Surface.Water, t.SurfaceAt(p.X, p.Z));
        foreach (var g in world.Roamers)
        {
            Assert.True(t.IsWalkable(t.CellXOf(g.Position.X), t.CellZOf(g.Position.Z)));
        }
    }

    [Fact]
    public void Raise_ChangesCenterByDelta_AndNothingBeyondRadiusPlusTwoCells()
    {
        var world = NewWorld();
        var t = world.Terrain!;
        var center = new Point2(30.0, 30.0); // coin (15, 15)
        var radius = 6.0;
        var before = new double[60 * 60];
        for (var z = 0; z < 60; z++)
        {
            for (var x = 0; x < 60; x++)
            {
                before[(z * 60) + x] = t.Sample(x + 0.5, z + 0.5);
            }
        }

        var h0 = t.Sample(center.X, center.Z);
        CellRect? seen = null;
        world.TerrainEdited += r => seen = r;
        world.RaiseTerrain(center, radius, 1.5);
        Assert.Equal(h0, t.Sample(center.X, center.Z)); // pas encore appliqué
        world.Step();

        Assert.Equal(h0 + 1.5, t.Sample(center.X, center.Z), 9);
        Assert.NotNull(seen);
        var zone = seen!.Value.Expand(2, t.CellsX, t.CellsZ);
        Assert.True(zone.MaxX * t.CellSize <= center.X + radius + (3 * t.CellSize));
        Assert.True(zone.MinX * t.CellSize >= center.X - radius - (3 * t.CellSize));
        for (var z = 0; z < 60; z++)
        {
            for (var x = 0; x < 60; x++)
            {
                if (!zone.Contains((int)(x / t.CellSize), (int)(z / t.CellSize)))
                {
                    Assert.Equal(before[(z * 60) + x], t.Sample(x + 0.5, z + 0.5));
                }
            }
        }
    }

    [Fact]
    public void Raise_ClampsDeltaAndHeight()
    {
        var world = NewWorld();
        var t = world.Terrain!;
        var c = new Point2(30.0, 30.0);

        world.RaiseTerrain(c, 4.0, 100.0);
        world.Step();
        Assert.Equal(TerrainGrid.MaxRaiseDelta, t.Heights.GetCornerHeight(15, 15), 9);

        for (var i = 0; i < 20; i++)
        {
            world.RaiseTerrain(c, 4.0, 2.0);
        }

        world.Step();
        Assert.Equal(TerrainGrid.MaxHeight, t.Heights.GetCornerHeight(15, 15), 9);
        for (var i = 0; i < 20; i++)
        {
            world.RaiseTerrain(c, 4.0, -2.0);
            world.Step();
        }

        Assert.Equal(TerrainGrid.MinHeight, t.Heights.GetCornerHeight(15, 15), 9);
    }

    [Fact]
    public void Edits_AreDeterministic()
    {
        ulong Run()
        {
            var w = NewWorld(77);
            w.SetLoadMode(5);
            w.Step(10);
            w.PaintSurface(new Point2(20, 20), 5, Surface.Water);
            w.Step(5);
            w.RaiseTerrain(new Point2(40, 30), 8, 1.0);
            w.Step(40);
            return w.Checksum();
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void RaiseOnly_DoesNotRebuildFlowFields_ButPaintDoes()
    {
        var world = NewWorld();
        var flow = world.Flow!;
        var before = flow.Get(new Point2(10, 10));

        world.RaiseTerrain(new Point2(30, 30), 6, 1.0);
        world.Step();
        Assert.Same(before, flow.Get(new Point2(10, 10)));

        world.PaintSurface(new Point2(30, 30), 6, Surface.Water);
        world.Step();
        Assert.NotSame(before, flow.Get(new Point2(10, 10)));
    }

    [Fact]
    public void PaintingTeeGreenOrHoleOffTheirSurface_DoesNotThrow()
    {
        var world = NewWorld();
        world.SpawnGroup();
        var t = world.Terrain!;
        world.PaintSurface(t.Tee, 4, Surface.Water);
        world.PaintSurface(t.Hole, 4, Surface.Trees);

        world.Step(200);

        Assert.Equal(200, world.Tick);
    }

    [Fact]
    public void PaintWater_OverWholeMap_IsIgnored()
    {
        var world = NewWorld();
        world.SetLoadMode(3);

        world.PaintSurface(new Point2(30, 30), 100, Surface.Water);
        world.Step();

        var t = world.Terrain!;
        Assert.Equal(Surface.Fairway, t.SurfaceAt(30, 30));
        Assert.Equal(1, world.Tick);
    }

    [Fact]
    public void PendingEdit_ChangesChecksum()
    {
        var a = NewWorld();
        var b = NewWorld();

        a.RaiseTerrain(new Point2(10, 10), 4, 1.0);

        Assert.NotEqual(a.Checksum(), b.Checksum());
    }
}
