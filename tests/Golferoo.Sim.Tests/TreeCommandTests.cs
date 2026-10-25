using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Tests;

/// <summary>Commandes d'arbres de C02 lot 4 : abattre, défricher, planter, faire tomber, dégager, coûts, déterminisme.</summary>
public class TreeCommandTests
{
    private static TerrainGrid Woods()
    {
        var t = PathingTests.Flat(40, 40, 4.0);
        for (var z = 5; z < 25; z++)
        {
            for (var x = 5; x < 25; x++)
            {
                t.PaintCell(x, z, Surface.Trees);
            }
        }

        t.PaintCell(35, 35, Surface.Water);
        t.PaintCell(34, 35, Surface.Green);
        t.PaintCell(33, 35, Surface.Bunker);
        t.PaintCell(32, 35, Surface.Tee);
        t.PaintCell(31, 35, Surface.Path);
        return t;
    }

    private static World Open(ulong seed = 3) => new(seed, PathingTests.Flat(40, 40, 4.0));

    private static Tree Isolated(World w, double x, double z, TreeSpeciesId species = TreeSpeciesId.Oak, int ageMonths = 600) =>
        w.Trees.Add(x, z, species, ageMonths);

    [Fact]
    public void RazeAndPlant_OfIsolatedTree_RebuildNoFlowField()
    {
        var w = Open();
        var tree = Isolated(w, 40, 40);
        var dest = new Point2(100, 100);
        var field = w.Flow!.Get(dest);
        var version = w.Terrain!.SurfaceVersion;

        w.RazeTree(tree.Id);
        w.PlantTree(100, 40, TreeSpeciesId.Birch, TreePlantStage.Sapling);
        w.Step();

        // Le plant est bien planté, le champ de flux est resté le même après l'abattage et la plantation.
        Assert.False(w.Trees.TryGet(tree.Id, out _));
        Assert.Equal(1, w.Trees.Count);
        Assert.Equal(TreeSpeciesId.Birch, w.Trees.Trees[0].Species);
        Assert.Equal(version, w.Terrain.SurfaceVersion);
        Assert.Same(field, w.Flow.Get(dest));
    }

    [Fact]
    public void RazeArea_TurnsUnderbrushToRough_AndKeepsRemarkableWhenAsked()
    {
        var w = new World(4, Woods());
        var terrain = w.Terrain!;
        var centre = new Point2(40, 40);
        Assert.Equal(Surface.Trees, terrain.SurfaceAt(40, 40));
        var old = w.Trees.Add(41, 41, TreeSpeciesId.Oak, 50 * 12 * 2, remarkable: true);
        var before = w.Trees.Count;
        var near = new List<Tree>();
        w.Trees.Query(40, 40, 15, near);
        Assert.True(near.Count > 3);
        var version = terrain.SurfaceVersion;

        w.RazeArea(centre, 15, keepRemarkable: true);
        w.Step();

        near.Clear();
        w.Trees.Query(40, 40, 15, near);
        Assert.Contains(near, t => t.Id == old.Id);
        Assert.All(near, t => Assert.True(t.Remarkable));
        Assert.True(w.Trees.Count < before);
        Assert.Equal(Surface.Rough, terrain.SurfaceAt(40, 40));
        Assert.True(terrain.SurfaceVersion > version);

        w.RazeArea(centre, 15, keepRemarkable: false);
        w.Step();
        near.Clear();
        w.Trees.Query(40, 40, 15, near);
        Assert.Empty(near);
    }

    [Fact]
    public void PlantTree_IsRefusedOnWaterGreenTeeBunkerPathUnderbrushAndTooCloseToATree()
    {
        var w = new World(5, Woods());
        w.Trees.RemoveWhere(_ => true);
        w.PlantTree((35 * 4) + 2, (35 * 4) + 2, TreeSpeciesId.Oak, TreePlantStage.Sapling);
        w.PlantTree((34 * 4) + 2, (35 * 4) + 2, TreeSpeciesId.Oak, TreePlantStage.Sapling);
        w.PlantTree((33 * 4) + 2, (35 * 4) + 2, TreeSpeciesId.Oak, TreePlantStage.Sapling);
        w.PlantTree((32 * 4) + 2, (35 * 4) + 2, TreeSpeciesId.Oak, TreePlantStage.Sapling);
        w.PlantTree((31 * 4) + 2, (35 * 4) + 2, TreeSpeciesId.Oak, TreePlantStage.Sapling);
        w.PlantTree(40, 40, TreeSpeciesId.Oak, TreePlantStage.Sapling); // sous-bois
        w.PlantTree(-5, 10, TreeSpeciesId.Oak, TreePlantStage.Sapling); // hors carte
        w.Step();
        Assert.Equal(0, w.Trees.Count);
        Assert.Equal(7, w.TreeCommandsRefused);
        Assert.Equal(0, w.TreeSpending);

        w.PlantTree(120, 120, TreeSpeciesId.Oak, TreePlantStage.Sapling);
        w.PlantTree(120.3, 120, TreeSpeciesId.Oak, TreePlantStage.Sapling); // dans le plancher d'espacement
        w.PlantTree(125, 120, TreeSpeciesId.Oak, TreePlantStage.Sapling);
        w.Step();
        Assert.Equal(2, w.Trees.Count);
        Assert.Equal(8, w.TreeCommandsRefused);
    }

    [Fact]
    public void Sapling_GrowsPastFourMetres_AndMoundIsTallerThanSapling()
    {
        var w = Open();
        w.PlantTree(30, 30, TreeSpeciesId.Oak, TreePlantStage.Sapling);
        w.PlantTree(80, 30, TreeSpeciesId.Oak, TreePlantStage.Mound);
        w.Step();
        var sapling = w.Trees.Trees[0];
        var mound = w.Trees.Trees[1];
        Assert.Equal(0, sapling.AgeMonths);
        Assert.InRange(mound.AgeMonths, 96, 120);
        Assert.True(mound.Height() > sapling.Height() + 3.0);
        Assert.True(sapling.Height() < 4.0);

        w.AdvanceTrees(5 * 12);
        foreach (var species in new[] { TreeSpeciesId.Oak, TreeSpeciesId.Birch, TreeSpeciesId.Fir })
        {
            Assert.True(Tree.Height(TreeSpeciesTable.Get(species), 5 * 12) > 4.0, species.ToString());
        }

        Assert.True(w.Trees.Trees[0].Height() > 4.0);
    }

    [Fact]
    public void FellTree_ChangesState_AndBallPassesWhereItWasStopped()
    {
        var terrain = PathingTests.Flat(50, 50, 4.0);
        var w = new World(6, terrain);
        var tree = Isolated(w, 120, 100, TreeSpeciesId.Oak, 100 * 12);
        Assert.True(w.Trees.MaxTopNear(120, 100, 1.0) > 15.0);
        Assert.True(Fly(terrain, w.Trees).FirstLanding.X < 120);

        w.FellTree(tree.Id, Math.PI / 2);
        w.Step();

        w.Trees.TryGet(tree.Id, out var fallen);
        Assert.Equal(TreeState.Fallen, fallen.State);
        Assert.Equal(Math.PI / 2, fallen.FallYaw, 1e-6);
        Assert.Equal(double.NegativeInfinity, w.Trees.MaxTopNear(120, 100, 1.0));
        Assert.True(Fly(terrain, w.Trees).FirstLanding.X > 120);
        Assert.Equal(1, w.Trees.Count);
    }

    private static SimulationResult Fly(TerrainGrid terrain, TreeStand stand) =>
        BallSimulator.Run(terrain, new Point2(100, 100), new Vec3(60, 0, 0), rolling: false, drag: 0.0, recordPath: false, startHeight: 1.5, trees: stand, season: Season.Summer, rng: new DeterministicRng(1));

    [Fact]
    public void ClearFallen_TurnsLogIntoStump_OnlyFromFallen()
    {
        var w = Open();
        var tree = Isolated(w, 40, 40);
        w.ClearFallen(tree.Id); // debout : refusé
        w.Step();
        Assert.Equal(TreeState.Standing, w.Trees.Trees[0].State);
        Assert.Equal(1, w.TreeCommandsRefused);

        w.FellTree(tree.Id, 0.0);
        w.ClearFallen(tree.Id);
        w.Step();
        Assert.Equal(TreeState.Stump, w.Trees.Trees[0].State);
        Assert.Equal(double.NegativeInfinity, w.Trees.MaxTopAt(40, 40));
        Assert.Equal(1, w.TreeCommandsRefused);
        Assert.Equal(TreeCosts.ClearFallen, w.TreeSpending);
    }

    [Fact]
    public void Commands_OnGoneOrWrongStateTrees_AreRefusedOnceAndCostRight()
    {
        var w = Open();
        var a = Isolated(w, 40, 40);
        var b = Isolated(w, 100, 40);
        var c = Isolated(w, 40, 100);
        w.RazeTree(a.Id);
        w.RazeTree(a.Id); // déjà rasé
        w.FellTree(b.Id, 0.0);
        w.RazeTree(b.Id); // tombé : refusé
        w.FellTree(c.Id, 0.0);
        w.ClearFallen(c.Id); // même tick : accepté
        w.Step();
        Assert.Equal(2, w.TreeCommandsRefused);
        Assert.Equal(TreeCosts.Raze + TreeCosts.ClearFallen, w.TreeSpending);
    }

    [Fact]
    public void RazeArea_CostsClearingForLogAndNothingForStump()
    {
        var w = Open();
        var log = Isolated(w, 40, 40);
        var stump = Isolated(w, 100, 100);
        w.FellTree(log.Id, 0.0);
        w.FellTree(stump.Id, 0.0);
        w.ClearFallen(stump.Id);
        w.Step();
        var before = w.TreeSpending;
        w.RazeArea(new Point2(40, 40), 5, keepRemarkable: false);
        w.Step();
        Assert.Equal(before + TreeCosts.ClearFallen, w.TreeSpending);
        w.RazeArea(new Point2(100, 100), 5, keepRemarkable: false);
        w.Step();
        Assert.Equal(before + TreeCosts.ClearFallen, w.TreeSpending);
        Assert.Equal(0, w.Trees.Count);
    }

    [Fact]
    public void PlantTree_IsRefusedNearStumpAndOnFallenLog()
    {
        var w = Open();
        var stump = Isolated(w, 40, 40);
        var log = Isolated(w, 100, 100);
        w.FellTree(stump.Id, 0.0);
        w.ClearFallen(stump.Id);
        w.FellTree(log.Id, 0.0);
        w.Step();
        w.PlantTree(41, 40, TreeSpeciesId.Birch, TreePlantStage.Sapling); // 1 m de la souche
        w.PlantTree(110, 100.5, TreeSpeciesId.Birch, TreePlantStage.Sapling); // sur le tronc couché
        w.Step();
        Assert.Equal(2, w.TreeCommandsRefused);
        w.PlantTree(42, 40, TreeSpeciesId.Birch, TreePlantStage.Sapling);
        w.Step();
        Assert.Equal(2, w.TreeCommandsRefused);
        Assert.Equal(3, w.Trees.Count);
    }

    [Fact]
    public void Spending_IsAccumulated_AndRemarkableCostsDouble()
    {
        var w = new World(8, Woods());
        w.Trees.RemoveWhere(_ => true);
        var a = Isolated(w, 120, 120);
        var b = w.Trees.Add(20, 130, TreeSpeciesId.Oak, 100 * 12, remarkable: true);
        var c = Isolated(w, 60, 130);
        w.PlantTree(140, 40, TreeSpeciesId.Fir, TreePlantStage.Sapling);
        w.PlantTree(140, 100, TreeSpeciesId.Fir, TreePlantStage.Mound);
        w.RazeTree(a.Id);
        w.RazeTree(b.Id);
        w.FellTree(c.Id, 1.0);
        w.ClearFallen(c.Id);
        w.Step();
        Assert.Equal(TreeCosts.Sapling + TreeCosts.Mound + TreeCosts.Raze + (TreeCosts.Raze * TreeCosts.RemarkableMultiplier) + TreeCosts.ClearFallen, w.TreeSpending);

        // Défrichage : 5 par m² de sous-bois converti.
        var before = w.TreeSpending;
        var trees = w.Terrain!.CellSize;
        var count = 0;
        for (var cz = 0; cz < w.Terrain.CellsZ; cz++)
        {
            for (var cx = 0; cx < w.Terrain.CellsX; cx++)
            {
                var cc = w.Terrain.CellCenter(cx, cz);
                if (w.Terrain.GetCellSurface(cx, cz) == Surface.Trees && ((cc.X - 60) * (cc.X - 60)) + ((cc.Z - 60) * (cc.Z - 60)) <= 100)
                {
                    count++;
                }
            }
        }

        w.RazeArea(new Point2(60, 60), 10, keepRemarkable: false);
        w.Step();
        Assert.True(count > 0);
        Assert.Equal(before + (count * trees * trees * TreeCosts.ClearPerSquareMeter), (double)w.TreeSpending, 1e-9);
    }

    [Fact]
    public void SameSeedAndCommands_GiveSameChecksum_AndCommandsChangeIt()
    {
        static ulong Run(bool commands)
        {
            var w = new World(11, Woods());
            if (commands)
            {
                w.RazeArea(new Point2(40, 40), 12, keepRemarkable: true);
                w.PlantTree(120, 120, TreeSpeciesId.Birch, TreePlantStage.Mound);
                w.Step(2);
                w.FellTree(w.Trees.Trees[^1].Id, 0.7);
                w.ClearFallen(w.Trees.Trees[^1].Id);
            }

            w.Step(5);
            return w.Checksum();
        }

        Assert.Equal(Run(true), Run(true));
        Assert.NotEqual(Run(true), Run(false));
    }

    [Fact]
    public void Woodland_MarksTheOldestTreesAsRemarkable()
    {
        var w = new World(12, Woods());
        Assert.Contains(w.Trees.Trees, t => t.Remarkable);
        Assert.Contains(w.Trees.Trees, t => !t.Remarkable);
        foreach (var t in w.Trees.Trees)
        {
            Assert.Equal(Tree.IsRemarkableAge(TreeSpeciesTable.Get(t.Species), t.AgeMonths), t.Remarkable);
        }
    }

    // --- Mineurs de l'audit du lot 3 ---

    [Fact]
    public void LoweringTheGround_LowersTreeTops()
    {
        var w = Open();
        Isolated(w, 40, 40, TreeSpeciesId.Oak, 100 * 12);
        w.RaiseTerrain(new Point2(40, 40), 20, 1.0);
        w.Step();
        var raised = w.Trees.MaxTopAt(40, 40);
        var raisedNear = w.Trees.MaxTopNear(40, 40, 1.0);
        Assert.True(raised > 17.5);

        w.RaiseTerrain(new Point2(40, 40), 20, -1.0);
        w.Step();
        Assert.True(w.Trees.MaxTopAt(40, 40) < raised);
        Assert.True(w.Trees.MaxTopNear(40, 40, 1.0) < raisedNear);
    }

    [Fact]
    public void RemovingTheOnlyTree_BringsMaxTopNearBackToNegativeInfinity()
    {
        var stand = new TreeStand(100, 100);
        var tree = stand.Add(50, 50, TreeSpeciesId.Fir, 100 * 12);
        Assert.True(stand.MaxTopNear(50, 50, 5.0) > 0);
        Assert.True(stand.Remove(tree.Id));
        Assert.Equal(double.NegativeInfinity, stand.MaxTopNear(50, 50, 5.0));
        Assert.Equal(double.NegativeInfinity, stand.MaxTopAt(50, 50));
    }
}
