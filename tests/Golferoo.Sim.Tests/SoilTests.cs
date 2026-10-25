using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Tests;

public class SoilTests
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

    private static double DriveRoll(Season season)
    {
        var terrain = Flat(Surface.Fairway);
        terrain.Soil.Season = season;
        var driver = Clubs.Driver;
        var v = new Vec3(driver.LaunchCos * driver.Speed, driver.LaunchSin * driver.Speed, 0);
        var sim = BallSimulator.Run(terrain, new Point2(20, 300), v, rolling: false, BallSimulator.DefaultDrag, recordPath: false);
        return sim.Shot.Ball.X - sim.FirstLanding.X;
    }

    // Critère 1 : en été, trajectoires inchangées au bit près par rapport à C02 (référence figée avant F2).
    [Fact]
    public void Summer_FlightsMatchFrozenReference()
    {
        var terrain = HoleLoader.Load(TerrainTests.HolePath(), 2);
        ulong h = 1469598103934665603UL;
        foreach (var club in new[] { Clubs.Driver, Clubs.Putter })
        {
            for (var a = 0; a < 12; a++)
            {
                var angle = a * Math.PI / 6;
                var v = new Vec3(Math.Cos(angle) * club.LaunchCos * club.Speed, club.LaunchSin * club.Speed, Math.Sin(angle) * club.LaunchCos * club.Speed);
                var sim = BallSimulator.Run(terrain, new Point2(terrain.Tee.X, terrain.Tee.Z), v, rolling: false, BallSimulator.DefaultDrag, recordPath: false);
                foreach (var d in new[] { sim.FirstLanding.X, sim.FirstLanding.Z, sim.Duration, sim.MaxHeight, sim.ClosestApproach.X, sim.ClosestApproach.Z, sim.Shot.Ball.X, sim.Shot.Ball.Z })
                {
                    h = (h ^ (ulong)BitConverter.DoubleToInt64Bits(d)) * 1099511628211UL;
                }
            }
        }

        Assert.Equal(16144048904935526792UL, h);
    }

    // Critère 3
    [Fact]
    public void Drive_RollsTenToTwentyPercentLessInSpring()
    {
        var summer = DriveRoll(Season.Summer);
        var spring = DriveRoll(Season.Spring);

        Assert.InRange(spring / summer, 0.80, 0.90);
    }

    [Fact]
    public void Autumn_RollsLessThanSummer()
    {
        Assert.True(DriveRoll(Season.Autumn) < DriveRoll(Season.Summer));
    }

    [Fact]
    public void PaintingCell_UpdatesSoil()
    {
        var terrain = Flat(Surface.Fairway);
        var fairway = terrain.Soil.RollResistanceAt(10, 10);

        terrain.PaintCell(2, 2, Surface.Bunker);

        Assert.Equal(SurfaceTable.Get(Surface.Bunker).RollResistance, terrain.Soil.RollResistanceAt(10, 10));
        Assert.Equal(fairway, terrain.Soil.RollResistanceAt(100, 100));
    }

    [Fact]
    public void CalendarSeasonChange_ChangesSoil()
    {
        var terrain = Flat(Surface.Fairway);
        var world = new World(seed: 1, terrain);
        Assert.Equal(Season.Spring, world.Season);
        var spring = terrain.Soil.RollResistanceAt(10, 10);

        world.SkipToNextMonth();
        world.SkipToNextMonth();

        Assert.Equal(Season.Summer, world.Season);
        Assert.Equal(SurfaceTable.Get(Surface.Fairway).RollResistance, terrain.Soil.RollResistanceAt(10, 10));
        Assert.True(spring > terrain.Soil.RollResistanceAt(10, 10));
    }

    [Fact]
    public void SameSeedSameCommands_SameChecksum()
    {
        ulong Run()
        {
            var w = new World(seed: 7, HoleLoader.Load(TerrainTests.HolePath(), 2));
            w.Step(300);
            w.SkipToNextMonth();
            w.Step(300);
            return w.Checksum();
        }

        Assert.Equal(Run(), Run());
    }
}
