using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;
using Golferoo.Sim.Trees;
using Golferoo.Sim.Turf;
using Golferoo.Sim.Weather;

namespace Golferoo.Sim.Tests;

public class TurfWearTests
{
    private static TerrainGrid Flat(Surface surface, bool green = false)
    {
        var grid = new TerrainGrid(new HeightField(150, 150, 4.0), "plat", new Point2(8, 300), new Point2(300, 300), new Point2(592, 8));
        for (var cz = 0; cz < 150; cz++)
        {
            for (var cx = 0; cx < 150; cx++)
            {
                grid.PaintCell(cx, cz, green && cx is >= 50 and < 70 && cz is >= 50 and < 70 ? Surface.Green : surface);
            }
        }

        return grid;
    }

    [Fact]
    public void ManyPasses_WearMoreThanHalf_PathNone()
    {
        var terrain = Flat(Surface.Fairway);
        terrain.PaintCell(11, 10, Surface.Path);
        for (var pass = 0; pass < 2000; pass++)
        {
            terrain.Turf.AddWear(10, 10, TurfGrid.StepWear);
            terrain.Turf.AddWear(11, 10, TurfGrid.StepWear);
        }

        Assert.True(terrain.Turf.Wear(10, 10) > 127);
        Assert.Equal(0, terrain.Turf.Wear(11, 10));
    }

    [Fact]
    public void WeekWithoutTraffic_Repairs_RainRepairsFaster()
    {
        var terrain = Flat(Surface.Fairway);
        var turf = terrain.Turf;
        turf.AddWear(5, 5, 7 * TurfGrid.RepairWear);
        turf.AddWear(6, 5, 7 * TurfGrid.RepairWear);

        turf.NextDay(Season.Summer, true, 1);
        Assert.Equal(5 * TurfGrid.RepairWear, turf.Wear(5, 5));
        for (var day = 2; day <= 7; day++)
        {
            turf.NextDay(Season.Summer, false, day);
        }

        Assert.Equal(0, turf.Wear(5, 5));
        Assert.Equal(0, turf.Wear(6, 5));

        var wet = Flat(Surface.Fairway).Turf;
        var dry = Flat(Surface.Fairway).Turf;
        wet.AddWear(0, 0, 40);
        dry.AddWear(0, 0, 40);
        wet.NextDay(Season.Summer, true, 1);
        dry.NextDay(Season.Summer, false, 1);
        Assert.True(wet.Wear(0, 0) < dry.Wear(0, 0));
    }

    [Fact]
    public void Wear_SaturatesAt255_AndLeavesWalkCostAndSoilAlone()
    {
        var world = new World(1, Flat(Surface.Fairway)) { Climate = Climate.Calm };
        var terrain = world.Terrain!;
        var field = world.Flow!.Get(new Point2(100, 100));
        var version = terrain.Turf.Version;
        terrain.Turf.AddWear(3, 3, 1000);
        Assert.Equal(255, terrain.Turf.Wear(3, 3));
        Assert.Same(field, world.Flow.Get(new Point2(100, 100)));
        Assert.Equal(version, terrain.Turf.Version);
    }

    [Fact]
    public void PlayingGroup_WearsTeeFairwayAndGreen()
    {
        var world = new World(3, Flat(Surface.Fairway, green: true)) { Climate = Climate.Calm };
        world.SpawnGroup();
        world.Step(4000);
        var terrain = world.Terrain!;
        var total = 0;
        for (var cz = 0; cz < 150; cz++)
        {
            for (var cx = 0; cx < 150; cx++)
            {
                total += terrain.Turf.Wear(cx, cz);
            }
        }

        Assert.True(total > 0);
    }

    [Fact]
    public void Wear_SameCommands_SameChecksum_WearChangesIt()
    {
        ulong Run(bool wear)
        {
            var w = new World(2, Flat(Surface.Fairway)) { Climate = Climate.Calm };
            if (wear)
            {
                w.Terrain!.Turf.AddWear(1, 1, 4);
            }

            w.Step(10);
            return w.Checksum();
        }

        Assert.Equal(Run(true), Run(true));
        Assert.NotEqual(Run(true), Run(false));
    }
}

public class PinTests
{
    private static TerrainGrid Course()
    {
        var grid = new TerrainGrid(new HeightField(150, 150, 4.0), "plat", new Point2(8, 300), new Point2(240, 240), new Point2(242, 242));
        for (var cz = 0; cz < 150; cz++)
        {
            for (var cx = 0; cx < 150; cx++)
            {
                grid.PaintCell(cx, cz, cx is >= 50 and < 70 && cz is >= 50 and < 70 ? Surface.Green : Surface.Fairway);
            }
        }

        return grid;
    }

    [Fact]
    public void SameSeedAndDay_SamePin_AlwaysThreeMetersInsideGreen()
    {
        var terrain = Course();
        var moved = new HashSet<Point2>();
        for (var day = 1; day <= 40; day++)
        {
            var a = PinPicker.Pick(terrain, 7, day)!.Value;
            Assert.Equal(a, PinPicker.Pick(terrain, 7, day)!.Value);
            Assert.InRange(a.X, (50 * 4) + 3, (70 * 4) - 3);
            Assert.InRange(a.Z, (50 * 4) + 3, (70 * 4) - 3);
            moved.Add(a);
        }

        Assert.True(moved.Count > 10);
    }

    [Fact]
    public void Day0KeepsFilePosition_ThenPinMovesEachDay_AndNoGreenKeepsIt()
    {
        var world = new World(7, Course()) { Climate = Climate.Calm };
        var file = world.Terrain!.Hole;
        world.Step(Calendar.TicksPerDay - 1);
        Assert.Equal(file, world.Terrain.Hole);
        world.Step();
        var day1 = world.Terrain.Hole;
        Assert.NotEqual(file, day1);
        Assert.Equal(Surface.Green, world.Terrain.SurfaceAt(day1.X, day1.Z));

        var bare = new World(7, new TerrainGrid(new HeightField(20, 20, 4.0), "nu", new Point2(8, 8), new Point2(40, 40), new Point2(40, 40))) { Climate = Climate.Calm };
        bare.Step(Calendar.TicksPerDay);
        Assert.Equal(new Point2(40, 40), bare.Terrain!.Hole);
    }
}
