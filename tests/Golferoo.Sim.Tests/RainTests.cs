using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;
using Golferoo.Sim.Trees;
using Golferoo.Sim.Weather;
using Xunit.Abstractions;

namespace Golferoo.Sim.Tests;

public class RainTests(ITestOutputHelper output)
{
    private static TerrainGrid Flat(Surface surface)
    {
        var grid = new TerrainGrid(new HeightField(150, 150, 4.0), "plat", new Point2(8, 300), new Point2(592, 8), new Point2(592, 8));
        for (var cz = 0; cz < 150; cz++)
        {
            for (var cx = 0; cx < 150; cx++)
            {
                grid.PaintCell(cx, cz, surface);
            }
        }

        return grid;
    }

    /// <summary>Impose le ciel <paramref name="sky"/> pour la journée en cours.</summary>
    private static void Force(World world, Sky sky)
    {
        while (world.Weather.Sky != sky)
        {
            world.CycleSky();
        }
    }

    [Fact]
    public void Approach_OnWetGreen_RollsAtLeastThirtyPercentLess()
    {
        double Roll(double wetness)
        {
            var terrain = Flat(Surface.Green);
            terrain.Soil.Wetness = wetness;
            var club = Clubs.Iron9;
            var v = new Vec3(club.LaunchCos * club.Speed * 0.8, club.LaunchSin * club.Speed * 0.8, 0);
            var sim = BallSimulator.Run(terrain, new Point2(20, 300), v, rolling: false, BallSimulator.DefaultDrag, recordPath: false);
            return sim.Shot.Ball.X - sim.FirstLanding.X;
        }

        var dry = Roll(0);
        var wet = Roll(1);
        output.WriteLine($"roulement sur le green : sec {dry:F1} m, mouillé {wet:F1} m");
        Assert.True(wet <= dry * 0.7);
    }

    [Fact]
    public void SoilWetness_FollowsTodayAndYesterday()
    {
        var world = new World(4, Flat(Surface.Fairway)) { Climate = Climate.Calm };
        Assert.Equal(0.0, world.Terrain!.Soil.Wetness);
        Force(world, Sky.Rain);
        Assert.Equal(1.0, world.Terrain.Soil.Wetness);
        Force(world, Sky.HeavyRain);
        world.Step(Calendar.TicksPerDay);
        Assert.Equal(0.5, world.Terrain.Soil.Wetness);
        world.Step(Calendar.TicksPerDay);
        Assert.Equal(0.0, world.Terrain.Soil.Wetness);
    }

    private static World Forest(ulong seed, Climate climate, int count = 5000)
    {
        var world = new World(seed, Flat(Surface.Rough)) { Climate = climate };
        var rng = new DeterministicRng(seed + 100);
        for (var i = 0; i < count; i++)
        {
            world.Trees.Add(10 + (rng.NextDouble() * 580), 10 + (rng.NextDouble() * 580), TreeSpeciesId.Oak, 40 * 12);
        }

        return world;
    }

    private static int Fallen(World w) => w.Trees.Trees.Count(t => t.State == TreeState.Fallen);

    [Fact]
    public void StormDay_FellsAFewTrees_CalmDayNone()
    {
        var calm = Forest(1, Climate.Calm);
        calm.Step(Calendar.TicksPerDay * 3);
        Assert.Equal(0, Fallen(calm));

        // Vingt journées de tempête sur 5 000 chênes adultes : 2,5 chutes par jour en moyenne attendues.
        var storm = Forest(2, Climate.Calm);
        var perDay = new List<int>();
        for (var day = 0; day < 20; day++)
        {
            var before = Fallen(storm);
            Force(storm, Sky.HeavyRain);
            storm.Step(Calendar.TicksPerDay);
            perDay.Add(Fallen(storm) - before);
        }

        output.WriteLine($"chutes par jour de tempête : {string.Join(" ", perDay)}");
        Assert.All(perDay, n => Assert.InRange(n, 0, 10));
        Assert.InRange(perDay.Average(), 1.0, 5.0);
    }

    [Fact]
    public void StormFalls_AreDeterministic()
    {
        ulong Run()
        {
            var w = Forest(3, Climate.Calm, 2000);
            for (var day = 0; day < 5; day++)
            {
                Force(w, Sky.HeavyRain);
                w.Step(Calendar.TicksPerDay);
            }

            return w.Checksum();
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void RainySpringMonth_YoungTreesGrowAnExtraMonth()
    {
        var world = new World(5, Flat(Surface.Rough)) { Climate = Climate.Calm };
        var young = world.Trees.Add(100, 100, TreeSpeciesId.Birch, 0);
        var adult = world.Trees.Add(200, 200, TreeSpeciesId.Oak, 40 * 12);
        for (var day = 0; day < Calendar.DaysPerMonth; day++)
        {
            if (day < World.SpringRainDays)
            {
                Force(world, Sky.Rain);
            }

            world.Step(Calendar.TicksPerDay);
        }

        world.Trees.TryGet(young.Id, out var y);
        world.Trees.TryGet(adult.Id, out var a);
        Assert.Equal(2 * Calendar.TreeMonthsPerMonth, y.AgeMonths);
        Assert.Equal((40 * 12) + Calendar.TreeMonthsPerMonth, a.AgeMonths);
    }
}
