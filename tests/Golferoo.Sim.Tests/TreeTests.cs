using Golferoo.Sim.Pathing;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Tests;

public class TreeTests
{
    private static TerrainGrid Woods(int n = 40, double cell = 4.0)
    {
        var t = PathingTests.Flat(n, n, cell);
        for (var z = 5; z < 25; z++)
        {
            for (var x = 5; x < 25; x++)
            {
                t.PaintCell(x, z, Surface.Trees);
            }
        }

        return t;
    }

    private static TerrainGrid Proto(int factor = 2) => HoleLoader.Load(TerrainTests.HolePath(), factor);

    [Fact]
    public void Stand_AddRemove_TracksMaxHeightPerCellAndVersion()
    {
        var stand = new TreeStand(100, 100);
        Assert.Equal(0.0, stand.MaxHeightAt(5, 5));
        var v0 = stand.Version;

        var young = stand.Add(5, 5, TreeSpeciesId.Deciduous, 0);
        var old = stand.Add(8, 9, TreeSpeciesId.Pine, 100 * 12);
        Assert.Equal(2, stand.Count);
        Assert.True(stand.Version > v0);
        Assert.Equal(20.0, stand.MaxHeightAt(5, 5), 1e-9);
        Assert.Equal(0.0, stand.MaxHeightAt(40, 40));

        var v1 = stand.Version;
        Assert.True(stand.Remove(old.Id));
        Assert.True(stand.Version > v1);
        Assert.Equal(young.Height(), stand.MaxHeightAt(5, 5), 1e-9);
        Assert.False(stand.Remove(old.Id));
        Assert.False(stand.TryGet(old.Id, out _));

        var v2 = stand.Version;
        Assert.False(stand.Remove(old.Id));
        Assert.Equal(v2, stand.Version);
    }

    [Fact]
    public void Stand_Query_FindsOnlyTreesInRadius()
    {
        var stand = new TreeStand(100, 100);
        stand.Add(10, 10, TreeSpeciesId.Pine, 0);
        stand.Add(30, 10, TreeSpeciesId.Pine, 0);
        var found = new List<Tree>();
        stand.Query(12, 10, 5, found);
        Assert.Single(found);
        found.Clear();
        stand.Query(20, 10, 15, found);
        Assert.Equal(2, found.Count);
    }

    [Fact]
    public void Height_IsIncreasingAndBoundedByAdult()
    {
        foreach (var id in new[] { TreeSpeciesId.Deciduous, TreeSpeciesId.Pine })
        {
            var sp = TreeSpeciesTable.Get(id);
            var last = 0.0;
            for (var age = 0; age <= sp.AdultAgeMonths * 2; age += 12)
            {
                var h = Tree.Height(sp, age);
                Assert.True(h >= last);
                Assert.True(h <= sp.AdultHeight + 1e-9);
                last = h;
            }

            Assert.Equal(sp.AdultHeight, Tree.Height(sp, sp.AdultAgeMonths), 1e-9);
            Assert.InRange(Tree.Height(sp, sp.AdultAgeMonths), 12.0, 25.0);
            Assert.Equal(sp.SaplingHeight, Tree.Height(sp, 0), 1e-9);
        }
    }

    [Fact]
    public void Woodland_SameSeed_SameTrees()
    {
        var a = new World(7, Woods());
        var b = new World(7, Woods());
        var c = new World(8, Woods());

        Assert.True(a.Trees.Count > 10);
        Assert.Equal(a.Trees.Trees, b.Trees.Trees);
        Assert.Equal(a.Checksum(), b.Checksum());
        Assert.NotEqual(a.Trees.Trees, c.Trees.Trees);
    }

    [Fact]
    public void Woodland_RespectsMinSpacing_AndStaysOnUnderbrush()
    {
        var world = new World(3, Proto());
        var terrain = world.Terrain!;
        var trees = world.Trees.Trees;
        Assert.NotEmpty(trees);
        foreach (var t in trees)
        {
            Assert.Equal(Surface.Trees, terrain.SurfaceAt(t.X, t.Z));
        }

        for (var i = 0; i < trees.Count; i++)
        {
            for (var j = i + 1; j < trees.Count; j++)
            {
                var d = Math.Sqrt(Math.Pow(trees[i].X - trees[j].X, 2) + Math.Pow(trees[i].Z - trees[j].Z, 2));
                var min = WoodlandGenerator.MinSpacingFactor * (trees[i].CrownRadius() + trees[j].CrownRadius());
                Assert.True(d >= min - 1e-9, $"arbres {trees[i].Id} et {trees[j].Id} à {d:0.00} m, minimum {min:0.00} m");
            }
        }
    }

    [Fact]
    public void Proto_AtDefaultFactor_HasRealisticTreeCount()
    {
        for (ulong seed = 1; seed <= 5; seed++)
        {
            var world = new World(seed, Proto());
            Assert.InRange(world.Trees.Count, 80, 180);
        }
    }

    [Fact]
    public void MatureTree_Is12To25Meters()
    {
        var world = new World(1, Proto());
        var mature = world.Trees.Trees.Where(t => t.AgeMonths >= TreeSpeciesTable.Get(t.Species).AdultAgeMonths).ToList();
        Assert.NotEmpty(mature);
        Assert.All(mature, t => Assert.InRange(t.Height(), 12.0, 25.0));
    }

    [Fact]
    public void Paint_AddsAndRemovesTrees()
    {
        var world = new World(5, PathingTests.Flat(40, 40, 4.0));
        Assert.Equal(0, world.Trees.Count);
        var v0 = world.Trees.Version;

        world.PaintSurface(new Point2(80, 80), 30, Surface.Trees);
        world.Step();
        var planted = world.Trees.Count;
        Assert.True(planted > 5);
        Assert.True(world.Trees.Version > v0);
        foreach (var t in world.Trees.Trees)
        {
            Assert.Equal(Surface.Trees, world.Terrain!.SurfaceAt(t.X, t.Z));
        }

        world.PaintSurface(new Point2(80, 80), 12, Surface.Fairway);
        world.Step();
        Assert.True(world.Trees.Count < planted);
        Assert.True(world.Trees.Count > 0);
        foreach (var t in world.Trees.Trees)
        {
            Assert.Equal(Surface.Trees, world.Terrain!.SurfaceAt(t.X, t.Z));
        }
    }

    [Fact]
    public void Snapshot_CopiesTreesOnlyWhenVersionChanges()
    {
        var world = new World(5, Woods());
        var snap = world.Snapshot();
        Assert.Equal(world.Trees.Count, snap.Trees.Count);
        Assert.Equal(world.Trees.Version, snap.TreesVersion);

        world.Trees.Add(1, 1, TreeSpeciesId.Pine, 0);
        Assert.NotEqual(world.Trees.Version, snap.TreesVersion);
        world.Snapshot(snap);
        Assert.Equal(world.Trees.Count, snap.Trees.Count);
    }

    [Fact]
    public void AddingTrees_DoesNotChangeFlowField()
    {
        var world = new World(5, Woods());
        var terrain = world.Terrain!;
        var dest = new Point2(100, 100);
        var before = world.Flow!.Get(dest);
        var surfaceVersion = terrain.SurfaceVersion;
        var distances = Distances(before);

        for (var i = 0; i < 30; i++)
        {
            world.Trees.Add(10 + (i * 3), 12, TreeSpeciesId.Deciduous, 100);
        }

        Assert.Equal(surfaceVersion, terrain.SurfaceVersion);
        Assert.Same(before, world.Flow.Get(dest)); // aucune reconstruction du champ en cache
        Assert.Equal(distances, Distances(FlowField.Build(terrain, before.DestX, before.DestZ)));
    }

    private static float[] Distances(FlowField f)
    {
        var d = new float[f.CellsX * f.CellsZ];
        for (var z = 0; z < f.CellsZ; z++)
        {
            for (var x = 0; x < f.CellsX; x++)
            {
                d[(z * f.CellsX) + x] = f.DistanceAt(x, z);
            }
        }

        return d;
    }

    [Fact]
    public void HoleLoader_ReadsIsolatedTrees()
    {
        var json = File.ReadAllText(TerrainTests.HolePath());
        var withTrees = json.TrimEnd().TrimEnd('}').TrimEnd() + ", \"trees\": [{\"x\": 10, \"z\": 8, \"species\": \"pine\", \"age\": 30}] }";
        var terrain = HoleLoader.Parse(withTrees, 2);

        var p = Assert.Single(terrain.IsolatedTrees);
        Assert.Equal(80.0, p.X, 1e-9);
        Assert.Equal(TreeSpeciesId.Pine, p.Species);
        Assert.Equal(360, p.AgeMonths);
        Assert.Contains(new World(1, terrain).Trees.Trees, t => t.X == 80.0 && t.Z == 64.0);
        Assert.Throws<HoleDataException>(() => HoleLoader.Parse(withTrees.Replace("pine", "baobab"), 2));
    }
}
