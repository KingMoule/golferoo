using Golferoo.Sim.Economy;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Tests;

/// <summary>C14 lot 2 : annulation du dernier coup de pinceau (peinture et relief), avec remboursement.</summary>
public class StrokeUndoTests
{
    private static World Ready()
    {
        var world = new World(2, CourseTests.ThreeHoles());
        world.Step();
        return world;
    }

    /// <summary>Un coup de pinceau : deux peintures qui se chevauchent, du sous-bois et une bosse.</summary>
    internal static void Stroke(World world, Point2 at)
    {
        world.BeginStroke();
        world.PaintSurface(at, 8, Surface.Fairway);
        world.PaintSurface(new Point2(at.X + 5, at.Z), 8, Surface.Bunker);
        world.PaintSurface(new Point2(at.X, at.Z + 20), 6, Surface.Trees);
        world.RaiseTerrain(new Point2(at.X + 3, at.Z), 10, 1.5);
        world.Step();
    }

    private static Point2 Spot(World world) => new(world.Terrain!.Course[1].Tee.X + 30, world.Terrain.Course[1].Tee.Z + 30);

    [Fact]
    public void UndoRightAfterAStroke_GivesBackCellsHeightsAndMoney()
    {
        var world = Ready();
        var t = world.Terrain!;
        var (sum, balance, trees) = (t.Checksum(), world.Ledger.Balance, world.Trees.Trees.Count);
        Assert.False(world.CanUndoStroke);

        Stroke(world, Spot(world));
        Assert.True(world.CanUndoStroke);
        Assert.NotEqual(sum, t.Checksum());
        Assert.True(world.Trees.Trees.Count > trees);
        Assert.Equal(balance - world.StrokeCost, world.Ledger.Balance);
        Assert.True(world.Ledger.Current(LedgerCategory.Surfaces) < 0 && world.Ledger.Current(LedgerCategory.Relief) < 0);

        CellRect? edited = null;
        world.TerrainEdited += r => edited = r;
        world.UndoStroke();
        world.Step();

        Assert.Equal(sum, t.Checksum());
        Assert.Equal(balance, world.Ledger.Balance);
        Assert.Equal(0, world.Ledger.Current(LedgerCategory.Surfaces));
        Assert.Equal(0, world.Ledger.Current(LedgerCategory.Relief));
        Assert.Equal(trees, world.Trees.Trees.Count); // le sous-bois peint repart avec ses arbres
        Assert.NotNull(edited);
        Assert.False(world.CanUndoStroke);
        Assert.Equal(0, world.StrokeCost);
    }

    [Fact]
    public void OnlyTheLastStrokeIsUndone()
    {
        var world = Ready();
        var t = world.Terrain!;
        var spot = Spot(world);
        world.PaintSurface(spot, 8, Surface.Green);
        world.Step();
        var (sum, balance) = (t.Checksum(), world.Ledger.Balance);

        Stroke(world, new Point2(spot.X + 40, spot.Z));
        world.UndoStroke();
        world.UndoStroke(); // la seconde ne trouve plus rien
        world.Step();

        Assert.Equal(sum, t.Checksum());
        Assert.Equal(balance, world.Ledger.Balance);
    }

    [Theory]
    [InlineData("plant")]
    [InlineData("tee")]
    public void AfterAnotherTerrainCommand_TheStrokeCanNoLongerBeUndone(string command)
    {
        var world = Ready();
        var t = world.Terrain!;
        Stroke(world, Spot(world));
        if (command == "plant")
        {
            world.PlantTree(t.Course[1].Tee.X + 80, t.Course[1].Tee.Z, TreeSpeciesId.Birch, TreePlantStage.Sapling);
        }
        else
        {
            Assert.True(world.PlaceTee(1, t.Course[1].Tee));
        }

        world.Step();
        Assert.False(world.CanUndoStroke);
        var (sum, balance) = (t.Checksum(), world.Ledger.Balance);

        world.UndoStroke();
        world.Step();

        Assert.Equal(sum, t.Checksum());
        Assert.Equal(balance, world.Ledger.Balance);
    }
}
