using Golferoo.Sim.Golfers;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Tests;

/// <summary>C12 lot 4 : planification des coups étalée sur plusieurs ticks, retouche sans pic.</summary>
public class PlanBudgetTests
{
    private static TerrainGrid Dogleg() =>
        HoleLoader.Load(Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "dogleg-par4.json"), 2);

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(40)]
    public void SlicedPlan_GivesTheSamePlanAsOneShot(int budget)
    {
        var terrain = Dogleg();
        var world = new World(1, terrain);
        var resolver = new PhysicalShotResolver { Trees = world.Trees, Season = world.Season };
        foreach (var profile in Enum.GetValues<Archetype>())
        {
            var request = new ShotRequest(terrain.Tee, terrain.Hole, 0, SkillLevels.Bogey, profile);
            var whole = new PlanJob();
            Assert.True(resolver.Advance(whole, request, terrain, int.MaxValue).Done);

            var sliced = new PlanJob();
            var calls = 0;
            while (!resolver.Advance(sliced, request, terrain, budget).Done)
            {
                calls++;
            }

            Assert.Equal(whole.Result, sliced.Result);
            Assert.Equal(whole.Candidates, sliced.Candidates);
            Assert.True(calls > 0);
        }
    }

    [Fact]
    public void Retouch_RebuildsFlowFieldsAFewPerTick_WalkersWaitMeanwhile()
    {
        var world = new World(3, Dogleg());
        world.SetLoadMode(40);
        world.Step(200);
        var cached = world.Flow!.Count;
        Assert.True(cached > Pathing.FlowFieldCache.RebuildsPerTick + 1, $"seulement {cached} champs en cache");

        world.PaintSurface(new Point2(150, 70), 12, Surface.Bunker);
        world.Step();
        Assert.NotEmpty(world.Flow.Pending);
        var waiting = world.Flow.Pending.Count;
        Assert.True(waiting < cached);

        world.Step(60);
        Assert.True(world.Flow.Pending.Count < waiting);
    }
}
