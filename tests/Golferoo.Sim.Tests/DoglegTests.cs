using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Tests;

/// <summary>C08 lot 1 : le vrai trou (dogleg par 4) et la partie d'entraînement du pro.</summary>
public class DoglegTests
{
    private static TerrainGrid Dogleg() =>
        HoleLoader.Load(Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "dogleg-par4.json"), 2);

    private static double Dist(Point2 a, Point2 b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));

    [Fact]
    public void Dogleg_IsAPar4WithAFewHundredTrees()
    {
        var world = new World(1, Dogleg());

        Assert.Equal(4, world.Terrain!.Par);
        Assert.InRange(world.Trees.Count, 150, 1000);
    }

    [Fact]
    public void Dogleg_TheStraightLineCrossesTheCornerWoods_TheFairwayGoesAround()
    {
        var t = Dogleg();
        var corner = new Point2(31.0 * 8, 30.0 * 8);
        var woods = 0;
        for (var i = 1; i < 100; i++)
        {
            var x = t.Tee.X + ((t.Hole.X - t.Tee.X) * i / 100.0);
            var z = t.Tee.Z + ((t.Hole.Z - t.Tee.Z) * i / 100.0);
            woods += t.SurfaceAt(x, z) == Surface.Trees ? 1 : 0;
        }

        Assert.True(woods > 30, $"{woods} points sur 99 dans les bois");
        Assert.Equal(Surface.Fairway, t.SurfaceAt(corner.X, corner.Z));
        Assert.InRange(Dist(t.Tee, corner) + Dist(corner, t.Hole), 360.0, 430.0);
    }

    [Fact]
    public void StartProRound_ProPlaysAloneOutsideTheLeague_HisCardIsNotKept()
    {
        var world = new World(3, Dogleg(), league: true);
        var pro = world.League!.Members.First(m => m.IsPro);

        var group = world.StartProRound();
        for (var i = 0; i < 40_000 && !group.Finished; i++)
        {
            world.Step();
        }

        Assert.True(group.Finished);
        Assert.Equal(pro.Id, Assert.Single(group.Golfers).MemberId);
        Assert.Equal(-1, group.LeagueRound);
        Assert.Empty(pro.Cards);
    }
}
