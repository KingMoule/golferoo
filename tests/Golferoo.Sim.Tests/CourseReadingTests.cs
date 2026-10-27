using Golferoo.Sim.Golfers;
using Golferoo.Sim.Pathing;
using Golferoo.Sim.Rating;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Xunit.Abstractions;

namespace Golferoo.Sim.Tests;

/// <summary>C09 lot 2 : le golfeur lit le trou par une carte de jeu au lieu de la distance à vol d'oiseau.</summary>
public class CourseReadingTests(ITestOutputHelper output)
{
    /// <summary>Part des drives scratch sur le fairway mesurée avant le lot (origine/main, 300 drives).</summary>
    private const double FairwayShareBefore = 0.05;

    private static (TerrainGrid Terrain, World World) Dogleg()
    {
        var terrain = HoleLoader.Load(Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "dogleg-par4.json"), 2);
        return (terrain, new World(1, terrain));
    }

    private static double FairwayShare(int drives = 300)
    {
        var (terrain, world) = Dogleg();
        var resolver = new PhysicalShotResolver { Trees = world.Trees, Season = world.Season };
        var fairway = 0;
        for (ulong seed = 1; seed <= (ulong)drives; seed++)
        {
            var r = resolver.Resolve(new ShotRequest(terrain.Tee, terrain.Hole, 0, SkillLevels.Scratch), terrain, new DeterministicRng(seed));
            fairway += r.Outcome == ShotOutcome.Fairway ? 1 : 0;
        }

        return (double)fairway / drives;
    }

    [Fact]
    public void Dogleg_ScratchDrivesFindTheFairwayCorner_TwentyPointsMoreThanBefore()
    {
        var share = FairwayShare();
        output.WriteLine($"drives scratch sur le fairway : avant {FairwayShareBefore:P1}, apres {share:P1}");

        Assert.True(share >= FairwayShareBefore + 0.20, $"{share:P1} contre {FairwayShareBefore:P1} avant");
    }

    [Fact]
    public void Dogleg_RatingDoesNotRise()
    {
        var (terrain, world) = Dogleg();
        var rating = CourseRater.Rate(terrain, 1, worldTrees: world.Trees.Trees);
        output.WriteLine($"note dogleg : scratch {rating.ScratchPerHole:F3} bogey {rating.BogeyPerHole:F3}");

        Assert.True(rating.ScratchPerHole <= RatingBefore.Scratch + 1e-9, $"scratch {rating.ScratchPerHole}");
        Assert.True(rating.BogeyPerHole <= RatingBefore.Bogey + 1e-9, $"bogey {rating.BogeyPerHole}");
    }

    private static class RatingBefore
    {
        public const double Scratch = 5.8;
        public const double Bogey = 6.6;
    }

    [Fact]
    public void PlayMap_OnTheDogleg_GoesAroundTheWoodsAndCostsMoreThanTheStraightLine()
    {
        var (terrain, world) = Dogleg();
        var map = PlayMap.Build(terrain, world.Trees, terrain.Hole);
        var straight = Math.Sqrt(((terrain.Tee.X - terrain.Hole.X) * (terrain.Tee.X - terrain.Hole.X)) + ((terrain.Tee.Z - terrain.Hole.Z) * (terrain.Tee.Z - terrain.Hole.Z)));
        var path = map.DistanceAt(terrain.Tee.X, terrain.Tee.Z, terrain.Hole);

        Assert.InRange(path, 1.1 * straight, 1.6 * straight);
        Assert.InRange(map.DistanceAt(terrain.Hole.X, terrain.Hole.Z, terrain.Hole), 0.0, 1.0);
    }

    [Fact]
    public void PlayMap_OnOpenGround_IsTheStraightLineWithinThreePercent()
    {
        var terrain = HoleLoader.Load(TerrainTests.HolePath(), 2);
        var map = PlayMap.Build(terrain, null, terrain.Hole);
        var from = new Point2(terrain.Hole.X - 60.0, terrain.Hole.Z - 25.0);
        var straight = Math.Sqrt((60.0 * 60.0) + (25.0 * 25.0));

        Assert.InRange(map.DistanceAt(from.X, from.Z, terrain.Hole), straight, 1.03 * straight);
    }

    [Fact]
    public void PlayMapCache_KeepsTheMap_UntilTheFlagMovesTheTerrainIsRepaintedOrTreesChange()
    {
        var (terrain, world) = Dogleg();
        var cache = new PlayMapCache();
        var first = cache.Get(terrain, world.Trees, terrain.Hole);

        Assert.Same(first, cache.Get(terrain, world.Trees, terrain.Hole));

        var moved = cache.Get(terrain, world.Trees, new Point2(terrain.Hole.X + 20.0, terrain.Hole.Z));
        Assert.NotSame(first, moved);

        var other = cache.Get(terrain, world.Trees, terrain.Hole);
        Assert.Same(other, cache.Get(terrain, world.Trees, terrain.Hole));

        terrain.PaintCell(5, 5, terrain.GetCellSurface(5, 5) == Surface.Rough ? Surface.Fairway : Surface.Rough);
        var repainted = cache.Get(terrain, world.Trees, terrain.Hole);
        Assert.NotSame(other, repainted);

        world.Trees.Add(10.0, 10.0, Trees.TreeSpeciesId.Oak, 600);
        Assert.NotSame(repainted, cache.Get(terrain, world.Trees, terrain.Hole));
    }
}
