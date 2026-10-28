using Golferoo.Sim.Golfers;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Tests;

/// <summary>C12 lot 3 : le parcours « Le neuf » (data/courses/le-neuf.json) se charge, se vérifie et se joue.</summary>
public class LeNeufTests
{
    // Relevé : une paire finit les 9 trous en 34 200 à 36 500 ticks (graines 1 à 3) ; plafond avec marge.
    private const int MaxTicks = 60_000;

    private static TerrainGrid Load() =>
        CourseLoader.Load(Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "..", "courses", "le-neuf.json"), 2);

    [Fact]
    public void LeNeuf_HasNineHoles_ParThirtySix_AllChecked()
    {
        var terrain = Load();
        Assert.Equal(9, terrain.Course.Count);
        Assert.Equal(36, terrain.Course.Holes.Sum(h => h.Par));
        Assert.All(terrain.Course.Holes, h => Assert.Empty(HoleCheck.Check(terrain, h)));
    }

    [Fact]
    public void LeNeuf_ParFollowsLength()
    {
        var terrain = Load();
        foreach (var h in terrain.Course.Holes)
        {
            var d = Math.Sqrt(((h.Hole.X - h.Tee.X) * (h.Hole.X - h.Tee.X)) + ((h.Hole.Z - h.Tee.Z) * (h.Hole.Z - h.Tee.Z)));
            Assert.Equal(d < 230 ? 3 : d < 430 ? 4 : 5, h.Par);
        }
    }

    [Fact]
    public void LeNeuf_TeesAreNearThePreviousGreen()
    {
        var terrain = Load();
        for (var n = 2; n <= 9; n++)
        {
            var (a, b) = (terrain.Course[n - 1].Green, terrain.Course[n].Tee);
            Assert.InRange(Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z))), 0, 150);
        }
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void LeNeuf_APairFinishesAllNineHoles(ulong seed)
    {
        var world = new World(seed, Load());
        var group = world.SpawnGroup();
        var ticks = 0;
        while (ticks < MaxTicks && !(group.Finished && group.Golfers.All(g => g.State == GolferState.Done)))
        {
            world.Step();
            ticks++;
        }

        Assert.InRange(ticks, 1, MaxTicks - 1);
        Assert.All(group.Golfers, g =>
        {
            Assert.Equal(9, g.Card!.HoleCount);
            Assert.True(g.Card.IsComplete);
        });
    }
}
