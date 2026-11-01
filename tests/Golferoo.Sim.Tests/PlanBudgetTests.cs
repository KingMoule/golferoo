using Golferoo.Sim.Golfers;
using Golferoo.Sim.Pathing;
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

    /// <summary>
    /// C21 lot 3, critère 11 (côté sim) : pinceau tenu, la carte change à chaque tick ; aucun champ de flux n'attend plus de 2 s de jeu
    /// (40 ticks), même quand les marcheurs de devant redemandent les leurs à chaque tick.
    /// </summary>
    [Fact]
    public void BrushHeld_NoWalkerWaitsMoreThanTwoSecondsForItsFlowField()
    {
        var terrain = Dogleg();
        var cache = new Pathing.FlowFieldCache(terrain);
        var destinations = new List<Point2>();
        for (var x = 6; destinations.Count < 32 && x < terrain.CellsX - 2; x += 5)
        {
            for (var z = 6; destinations.Count < 32 && z < terrain.CellsZ - 2; z += 7)
            {
                if (GridQueries.TryNearestWalkable(terrain, x, z, out var nx, out var nz) && nx == x && nz == z)
                {
                    destinations.Add(new Point2((x + 0.5) * terrain.CellSize, (z + 0.5) * terrain.CellSize));
                    cache.Get(destinations[^1]);
                }
            }
        }

        Assert.True(destinations.Count >= 24, $"seulement {destinations.Count} destinations");
        var waited = new int[destinations.Count];
        var worst = 0;
        for (var tick = 0; tick < 400; tick++)
        {
            terrain.PaintCell(1, 1, tick % 2 == 0 ? Surface.Bunker : Surface.Rough);   // un coup de pinceau par tick
            cache.BeginTick();
            for (var i = 0; i < destinations.Count; i++)
            {
                waited[i] = cache.TryGet(destinations[i]) is null ? waited[i] + 1 : 0;
                worst = Math.Max(worst, waited[i]);
            }
        }

        Assert.True(worst <= 2 * World.TicksPerSecond, $"un champ a attendu {worst} ticks");
    }

    /// <summary>
    /// Banc C12 : des destinations jamais vues, demandées ensemble par des marcheurs, ne donnent pas plus de <see cref="Pathing.FlowFieldCache.RebuildsPerTick"/> champs par tick ;
    /// les autres attendent (entrent en attente, comme après une retouche) et sont tous servis, dans l'ordre, sans qu'un seul calcul manque.
    /// </summary>
    [Fact]
    public void NewDestinations_AreCappedPerTick_AndAllServedInOrder()
    {
        var terrain = Dogleg();
        var cache = new Pathing.FlowFieldCache(terrain);
        var destinations = new List<Point2>();
        for (var x = 6; destinations.Count < 10 && x < terrain.CellsX - 2; x += 5)
        {
            if (GridQueries.TryNearestWalkable(terrain, x, 6, out var nx, out var nz) && nx == x && nz == 6)
            {
                destinations.Add(new Point2((x + 0.5) * terrain.CellSize, 6.5 * terrain.CellSize));
            }
        }

        Assert.True(destinations.Count >= 6, $"seulement {destinations.Count} destinations");
        for (var tick = 0; tick < destinations.Count + 2; tick++)
        {
            var before = cache.Count;
            cache.BeginTick();
            var ready = destinations.Count(d => cache.TryGet(d) is not null);
            Assert.True(cache.Count - before <= Pathing.FlowFieldCache.RebuildsPerTick, $"tick {tick} : {cache.Count - before} champs calculés");
            Assert.Equal(Math.Min(destinations.Count, (tick + 1) * Pathing.FlowFieldCache.RebuildsPerTick), ready);
        }

        Assert.Empty(cache.Pending);
        Assert.NotNull(cache.Get(destinations[^1]));
    }
}
