using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;
using Golferoo.Sim.Trees;
using Golferoo.Sim.Turf;
using Golferoo.Sim.Weather;

namespace Golferoo.Sim.Tests;

public class TurfTests
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

    private static World Calm(Surface surface, TurfSettings? settings = null)
    {
        var world = new World(1, Flat(surface)) { Climate = Climate.Calm };
        world.SetTurfSettings(settings ?? TurfSettings.Normal);
        world.Step(); // la consigne est une commande : appliquée au début du tick
        return world;
    }

    private static double Roll(TerrainGrid terrain, double speed)
    {
        var start = new Point2(20, 300);
        return BallSimulator.Run(terrain, start, new Vec3(speed, 0, 0), rolling: true, BallSimulator.DefaultDrag, recordPath: false).Shot.Ball.X - start.X;
    }

    private static double DriveRoll(TerrainGrid terrain)
    {
        var d = Clubs.Driver;
        var v = new Vec3(d.LaunchCos * d.Speed, d.LaunchSin * d.Speed, 0);
        var sim = BallSimulator.Run(terrain, new Point2(20, 300), v, rolling: false, BallSimulator.DefaultDrag, recordPath: false);
        return sim.Shot.Ball.X - sim.FirstLanding.X;
    }

    [Fact]
    public void Growth_WithoutMowing_ExceedsTargetInSpringWeek_WithMowingReturns()
    {
        var terrain = Flat(Surface.Rough);
        var turf = terrain.Turf;
        for (var day = 1; day <= 6; day++)
        {
            turf.NextDay(Season.Spring, false, day); // jours 1 à 6 : pas de tonte du rough
        }

        Assert.True(turf.HeightMm(0, 0) > 50.0);
        turf.NextDay(Season.Spring, false, 7); // jour 7 : tonte hebdomadaire
        Assert.Equal(50.0, turf.HeightMm(0, 0));
    }

    [Fact]
    public void Winter_GrassDoesNotGrow()
    {
        var terrain = Flat(Surface.Fairway);
        for (var day = 1; day <= 5; day++)
        {
            terrain.Turf.NextDay(Season.Winter, false, day);
        }

        Assert.Equal(15.0, terrain.Turf.HeightMm(0, 0));
    }

    [Fact]
    public void RainYesterday_GrowsOneAndAHalfTimesMore()
    {
        var dry = Flat(Surface.Rough);
        var wet = Flat(Surface.Rough);
        dry.Turf.NextDay(Season.Spring, false, 1);
        wet.Turf.NextDay(Season.Spring, true, 1);
        Assert.Equal(1.5, (wet.Turf.HeightMm(0, 0) - 50.0) / (dry.Turf.HeightMm(0, 0) - 50.0), 6);
    }

    [Fact]
    public void FastGreen_RollsAtLeastTenPercentFurther_SlowAtLeastTenPercentLess()
    {
        double Putt(GreenSpeed speed)
        {
            var world = Calm(Surface.Green, TurfSettings.Normal with { GreenSpeed = speed });
            world.Step(6 * Calendar.TicksPerDay); // le lent repousse de 4 par jour jusqu'à 80
            return Roll(world.Terrain!, 2.0);
        }

        var normal = Putt(GreenSpeed.Normal);
        Assert.True(Putt(GreenSpeed.Fast) >= 1.10 * normal);
        Assert.True(Putt(GreenSpeed.Slow) <= 0.90 * normal);
    }

    [Fact]
    public void ThickRough_StopsBallSoonerThanShortRough()
    {
        double Run(RoughLength length)
        {
            var world = Calm(Surface.Rough, TurfSettings.Normal with { RoughLength = length });
            world.Step(7 * Calendar.TicksPerDay); // le rough se tond au jour 7
            return Roll(world.Terrain!, 10.0);
        }

        Assert.True(Run(RoughLength.Thick) < Run(RoughLength.Short));
    }

    [Fact]
    public void FirmFairway_RollsDriveFurtherThanSoft()
    {
        double Drive(FairwayFirmness firmness)
        {
            var terrain = Flat(Surface.Fairway);
            terrain.Turf.Settings = TurfSettings.Normal with { FairwayFirmness = firmness };
            return DriveRoll(terrain);
        }

        Assert.True(Drive(FairwayFirmness.Firm) > Drive(FairwayFirmness.Normal));
        Assert.True(Drive(FairwayFirmness.Normal) > Drive(FairwayFirmness.Soft));
    }

    [Fact]
    public void Stimp_NormalGreen_IsBetweenTenAndTwelveFeet()
    {
        var (meters, feet) = Flat(Surface.Green).Turf.Stimp();
        Assert.InRange(feet, 10.0, 12.0);
        Assert.Equal(3.4, meters, 1);
    }

    [Fact]
    public void Stimp_FastGreen_IsLongerThanSlow()
    {
        var fast = Flat(Surface.Green);
        fast.Turf.Settings = TurfSettings.Normal with { GreenSpeed = GreenSpeed.Fast };
        var slow = Flat(Surface.Green);
        slow.Turf.Settings = TurfSettings.Normal with { GreenSpeed = GreenSpeed.Slow };
        for (var d = 1; d <= 6; d++)
        {
            fast.Turf.NextDay(Season.Spring, false, d);
            slow.Turf.NextDay(Season.Spring, false, d);
        }

        Assert.True(fast.Turf.Stimp().Feet > slow.Turf.Stimp().Feet);
    }

    [Fact]
    public void SameSeedAndCommands_SameHeightsAndChecksum_SettingsChangeIt()
    {
        ulong Run(bool command)
        {
            var w = Calm(Surface.Green);
            w.Step(Calendar.TicksPerDay / 2);
            if (command)
            {
                w.SetTurfSettings(TurfSettings.Normal with { GreenSpeed = GreenSpeed.Fast });
            }

            w.Step(2 * Calendar.TicksPerDay);
            return w.Checksum();
        }

        Assert.Equal(Run(true), Run(true));
        Assert.NotEqual(Run(true), Run(false));
    }
}
