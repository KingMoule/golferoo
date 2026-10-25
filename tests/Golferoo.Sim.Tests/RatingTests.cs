using Golferoo.Sim.Rating;
using Golferoo.Sim.Terrain;

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
        var plain = CourseRater.Rate(Hole(), 1, 100);
        var terrain = Hole();
        var dx = terrain.Tee.X - terrain.Green.X;
        var dz = terrain.Tee.Z - terrain.Green.Z;
        var d = Math.Sqrt((dx * dx) + (dz * dz));
        var front = new Point2(terrain.Green.X + (dx / d * 18.0), terrain.Green.Z + (dz / d * 18.0));
        terrain.PaintDisc(front, 14.0, Surface.Bunker);

        Assert.True(CourseRater.Rate(terrain, 1, 100).ScratchStrokes > plain.ScratchStrokes);
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
}
