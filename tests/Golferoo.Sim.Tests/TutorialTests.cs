using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;
using Golferoo.Sim.Tutorial;

namespace Golferoo.Sim.Tests;

/// <summary>C07 lot 1 : la terre vierge, la validité du trou, la pose du départ et du trou, les étapes du tutoriel.</summary>
public class TutorialTests
{
    private static string HolesDir => Path.GetDirectoryName(TerrainTests.HolePath())!;

    private static TerrainGrid Load(string file) => HoleLoader.Load(Path.Combine(HolesDir, file), 2);

    private static World BlankWorld() => new(1, Load("blank-coude.json"));

    private static readonly Point2 TeeSpot = new(28, 244);
    private static readonly Point2 GreenSpot = new(268, 56);

    private static void Raze(World world)
    {
        foreach (var p in world.Terrain!.IsolatedTrees)
        {
            world.RazeTree(world.Trees.Trees.First(t => t.X == p.X && t.Z == p.Z).Id);
        }

        world.Step();
    }

    private static void Plant(World world)
    {
        world.PlantTree(64, 208, TreeSpeciesId.Oak, TreePlantStage.Sapling);
        world.Step();
    }

    private static void PaintTeeAndFairway(World world)
    {
        world.PaintSurface(TeeSpot, 6, Surface.Tee);
        foreach (var x in new[] { 100.0, 160.0, 220.0 })
        {
            world.PaintSurface(new Point2(x, 236), 10, Surface.Fairway);
        }

        world.Step();
    }

    private static void PaintGreen(World world)
    {
        world.PaintSurface(GreenSpot, 11, Surface.Green);
        world.Step();
    }

    /// <summary>Joue tout le tutoriel par commandes et rend la somme de contrôle finale.</summary>
    private static ulong PlayTutorial()
    {
        var world = BlankWorld();
        Raze(world);
        Plant(world);
        PaintTeeAndFairway(world);
        PaintGreen(world);
        Assert.True(world.PlaceTee(1, TeeSpot));
        Assert.True(world.PlaceHole(1, GreenSpot));
        world.Step();
        Assert.Equal(TutorialStep.Done, TutorialSteps.Current(world));
        Assert.True(world.HoleOpen);
        world.Step(40);
        return world.Checksum();
    }

    [Fact]
    public void BlankTerrain_LoadsClosedWithItsCorridorTrees()
    {
        var world = BlankWorld();

        Assert.True(world.Terrain!.Blank);
        Assert.False(world.HoleOpen);
        Assert.Equal(8, world.Terrain.IsolatedTrees.Count);
        Assert.Equal(8, TutorialSteps.CorridorTreesLeft(world, world.Terrain));
        Assert.Equal(0, HoleCheck.Area(world.Terrain, Surface.Green));
    }

    [Fact]
    public void ClosedHole_RefusesGolfersAndLeague()
    {
        var world = BlankWorld();

        Assert.Throws<InvalidOperationException>(() => world.SpawnGroup());
        Assert.Throws<ArgumentOutOfRangeException>(() => new World(1, Load("blank-coude.json"), league: true));
    }

    [Fact]
    public void PlaceTeeAndHole_AreRefusedOffTheirSurface()
    {
        var world = BlankWorld();

        Assert.False(world.PlaceTee(1, TeeSpot));
        Assert.False(world.PlaceHole(1, GreenSpot));
        Assert.Equal(0, world.PendingEdits);
    }

    [Fact]
    public void HoleCheck_ListsEveryMissingPieceOnTheBlankTerrain()
    {
        var issues = HoleCheck.Check(Load("blank-coude.json"));

        Assert.Equal(
            [HoleIssue.TeeTooSmall, HoleIssue.FairwayTooShort, HoleIssue.GreenTooSmall, HoleIssue.TeeNotPlaced, HoleIssue.HoleNotOnGreen],
            issues);
    }

    [Fact]
    public void HoleCheck_FlagsAHoleTooCloseToItsTee()
    {
        var world = BlankWorld();
        world.PaintSurface(TeeSpot, 6, Surface.Tee);
        world.PaintSurface(new Point2(60, 244), 11, Surface.Green);
        world.Step();
        world.PlaceTee(1, TeeSpot);
        world.PlaceHole(1, new Point2(60, 244));
        world.Step();

        Assert.Contains(HoleIssue.TooClose, HoleCheck.Check(world.Terrain!));
    }

    [Fact]
    public void HoleCheck_FlagsAGreenCutOffByWater()
    {
        var world = BlankWorld();
        world.PaintSurface(TeeSpot, 6, Surface.Tee);
        world.PaintSurface(GreenSpot, 20, Surface.Water);
        world.Step();
        world.PaintSurface(GreenSpot, 11, Surface.Green);
        world.Step();
        world.PlaceTee(1, TeeSpot);
        world.PlaceHole(1, GreenSpot);
        world.Step();

        Assert.Contains(HoleIssue.NoPath, HoleCheck.Check(world.Terrain!));
        Assert.DoesNotContain(HoleIssue.TooClose, HoleCheck.Check(world.Terrain!));
    }

    [Fact]
    public void PlantTree_RefusedOnTheTeeDoesNotCountForTheSaplingStep()
    {
        var world = BlankWorld();
        world.PaintSurface(TeeSpot, 6, Surface.Tee);
        world.Step();
        world.PlantTree(TeeSpot.X, TeeSpot.Z, TreeSpeciesId.Oak, TreePlantStage.Sapling);
        world.Step();

        Assert.Equal(0, world.TreesPlanted);
        Assert.Equal(1, world.TreeCommandsRefused);
    }

    [Theory]
    [InlineData("dogleg-par4.json")]
    [InlineData("proto-par4.json")]
    public void HoleCheck_FindsNothingMissingOnTheRealHoles(string file) =>
        Assert.Empty(HoleCheck.Check(Load(file)));

    [Fact]
    public void RealHoles_AreOpenFromTheStart()
    {
        Assert.True(new World(1, Load("dogleg-par4.json")).HoleOpen);
        Assert.True(new World(1, null).HoleOpen);
    }

    [Fact]
    public void Steps_FollowTheStateOfTheWorld()
    {
        var world = BlankWorld();
        Assert.Equal(TutorialStep.ClearTrees, TutorialSteps.Current(world));

        Raze(world);
        Assert.Equal(TutorialStep.PlantSapling, TutorialSteps.Current(world));

        Plant(world);
        Assert.Equal(TutorialStep.PaintTee, TutorialSteps.Current(world));

        world.PaintSurface(TeeSpot, 6, Surface.Tee);
        world.Step();
        Assert.Equal(TutorialStep.PaintFairway, TutorialSteps.Current(world));

        PaintTeeAndFairway(world);
        Assert.Equal(TutorialStep.PaintGreen, TutorialSteps.Current(world));

        PaintGreen(world);
        Assert.Equal(TutorialStep.PlaceTeeAndHole, TutorialSteps.Current(world));
        Assert.False(world.HoleOpen);
    }

    [Fact]
    public void Steps_UndoingTheGreenBringsBackTheGreenStep()
    {
        var world = BlankWorld();
        Raze(world);
        Plant(world);
        PaintTeeAndFairway(world);
        PaintGreen(world);
        world.PlaceTee(1, TeeSpot);
        world.PlaceHole(1, GreenSpot);
        world.Step();
        Assert.Equal(TutorialStep.Done, TutorialSteps.Current(world));

        world.PaintSurface(GreenSpot, 15, Surface.Rough);
        world.Step();

        Assert.False(world.HoleOpen);
        Assert.Equal(TutorialStep.PaintGreen, TutorialSteps.Current(world));
    }

    [Fact]
    public void OpenHole_AcceptsGolfers()
    {
        var world = BlankWorld();
        Raze(world);
        Plant(world);
        PaintTeeAndFairway(world);
        PaintGreen(world);
        world.PlaceTee(1, TeeSpot);
        world.PlaceHole(1, GreenSpot);
        world.Step();

        Assert.Equal(2, world.SpawnGroup().Golfers.Count);
    }

    [Fact]
    public void WholeTutorial_IsDeterministic() =>
        Assert.Equal(PlayTutorial(), PlayTutorial());
}
