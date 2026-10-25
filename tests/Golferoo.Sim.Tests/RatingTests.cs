using Golferoo.Sim.Golfers;
using Golferoo.Sim.Rating;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Tests;

public class RatingTests
{
    private static TerrainGrid Hole() => HoleLoader.Load(TerrainTests.HolePath(), 2);

    [Fact]
    public void Rate_SameTerrainAndSeed_SameRating()
    {
        Assert.Equal(CourseRater.Rate(Hole(), 7, 15), CourseRater.Rate(Hole(), 7, 15));
    }

    [Fact]
    public void Rate_OnProtoPar4_GivesScratchNearParAndSlopeInRange()
    {
        var rating = CourseRater.Rate(Hole(), 1);

        Assert.InRange(rating.Slope, 55, 155);
        Assert.InRange(rating.ScratchPerHole - rating.Par, 0.0, 0.4);
        Assert.True(rating.BogeyStrokes > rating.ScratchStrokes);
    }

    [Fact]
    public void Rate_BunkerInFrontOfGreen_RaisesScratchRating()
    {
        var plain = CourseRater.Rate(Hole(), 1, 60);
        var terrain = Hole();
        var dx = terrain.Tee.X - terrain.Green.X;
        var dz = terrain.Tee.Z - terrain.Green.Z;
        var d = Math.Sqrt((dx * dx) + (dz * dz));
        var front = new Point2(terrain.Green.X + (dx / d * 18.0), terrain.Green.Z + (dz / d * 18.0));
        terrain.PaintDisc(front, 14.0, Surface.Bunker);

        Assert.True(CourseRater.Rate(terrain, 1, 60).ScratchStrokes > plain.ScratchStrokes);
    }

    [Fact]
    public void Rate_DoesNotTouchTheTerrainOrTheWorld()
    {
        var terrain = Hole();
        var world = new World(3, terrain);
        world.Step(50);
        var before = world.Checksum();
        var terrainBefore = terrain.Checksum();

        CourseRater.Rate(terrain, 3, 10);

        Assert.Equal(before, world.Checksum());
        Assert.Equal(terrainBefore, terrain.Checksum());
    }

    [Fact]
    public void SetCourseRating_IsQueuedAppliedNextTickAndReplayable()
    {
        var rating = new CourseRating(1, 4, 4.2, 5.3);
        ulong Run(bool withRating)
        {
            var w = new World(5, Hole());
            w.Step(10);
            if (withRating)
            {
                w.SetCourseRating(rating);
                Assert.Null(w.CourseRating);
            }

            w.Step(10);
            Assert.Equal(withRating ? rating : null, w.CourseRating);
            return w.Checksum();
        }

        Assert.Equal(Run(true), Run(true));
        Assert.NotEqual(Run(true), Run(false));
    }

    [Fact]
    public void SetCourseRating_PendingCommandEntersChecksum()
    {
        var a = new World(5, Hole());
        var b = new World(5, Hole());
        a.SetCourseRating(new CourseRating(1, 4, 4.2, 5.3));

        Assert.NotEqual(b.Checksum(), a.Checksum());
    }

    [Fact]
    public void Rate_WorldTreesInThePlayCorridor_RaiseScratchRating()
    {
        var terrain = Hole();
        var plain = CourseRater.Rate(terrain, 1, 60, worldTrees: []);
        var trees = new List<Tree>();
        foreach (var f in new[] { 0.35, 0.5, 0.65, 0.8 })
        {
            trees.Add(new Tree(trees.Count, terrain.Tee.X + ((terrain.Green.X - terrain.Tee.X) * f), terrain.Tee.Z + ((terrain.Green.Z - terrain.Tee.Z) * f), TreeSpeciesId.Oak, 60 * 12));
        }

        Assert.True(CourseRater.Rate(terrain, 1, 60, worldTrees: trees).ScratchStrokes > plain.ScratchStrokes);
    }

    private static int Gross(PhysicalShotResolver resolver, TerrainGrid terrain, double skill, ulong seed)
    {
        var rng = new DeterministicRng(seed);
        var ball = terrain.Tee;
        var strokes = 0;
        while (strokes < World.MaxStrokes)
        {
            var r = resolver.Resolve(new ShotRequest(ball, terrain.Hole, strokes, skill), terrain, rng);
            strokes += 1 + r.PenaltyStrokes;
            ball = r.Ball;
            if (r.Holed)
            {
                break;
            }
        }

        return Math.Min(strokes, World.MaxStrokes);
    }

    [Theory]
    [InlineData(10.0)]
    [InlineData(20.0)]
    public void Index_IsRecoveredWithinThreePoints(double h)
    {
        const int series = 40;
        var terrain = Hole();
        var rating = CourseRater.Rate(terrain, 1);
        var indexes = new double[series];
        Parallel.For(0, series, s =>
        {
            var resolver = new PhysicalShotResolver();
            var diffs = Enumerable.Range(0, 20)
                .Select(g => Handicap.Differential(Gross(resolver, terrain, SkillLevels.FromHandicap(h), 1000UL + (ulong)((s * 20) + g)), rating, 1))
                .ToArray();
            indexes[s] = Handicap.Index(diffs, 1)!.Value;
        });

        Assert.InRange(indexes.Average(), h - 3.0, h + 3.0);
    }
}
