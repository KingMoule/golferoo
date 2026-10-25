using Golferoo.Sim;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Tests;

public class CalendarTests
{
    private const int Day = Calendar.TicksPerDay;

    [Fact]
    public void Start_IsAprilFirstYearOne_AtOpening()
    {
        var d = Calendar.At(0, 0);
        Assert.Equal((1, 4, 1, Season.Spring, 0.0), (d.Day, d.Month, d.Year, d.Season, d.DayFraction));
    }

    [Fact]
    public void DayFraction_GoesFromZeroToOne_ThenDayChanges()
    {
        Assert.Equal(1.0, Calendar.At(Day - 1, 0).DayFraction, 1e-12);
        Assert.InRange(Calendar.At(Day / 2, 0).DayFraction, 0.49, 0.51);
        var next = Calendar.At(Day, 0);
        Assert.Equal(2, next.Day);
        Assert.Equal(0.0, next.DayFraction);
    }

    [Fact]
    public void MonthYearAndSeason_Roll()
    {
        static (int, int, int, Season) At(long days)
        {
            var d = Calendar.At(days * Day, 0);
            return (d.Day, d.Month, d.Year, d.Season);
        }

        Assert.Equal((10, 4, 1, Season.Spring), At(9));
        Assert.Equal((1, 5, 1, Season.Spring), At(10));
        Assert.Equal((1, 6, 1, Season.Summer), At(20));
        Assert.Equal((1, 9, 1, Season.Autumn), At(50));
        Assert.Equal((1, 12, 1, Season.Winter), At(80));
        Assert.Equal((1, 1, 2, Season.Winter), At(90));
        Assert.Equal((1, 3, 2, Season.Spring), At(110));
        Assert.Equal((1, 4, 2, Season.Spring), At(120));
        Assert.Equal((3, 5, 1, Season.Spring), (Calendar.At(2 * Day, 10).Day, Calendar.At(2 * Day, 10).Month, 1, Calendar.At(2 * Day, 10).Season));
    }

    [Fact]
    public void MonthChange_GrowsEveryTreeOnce_ByFourMonths()
    {
        var world = new World(1);
        for (var i = 0; i < 5; i++)
        {
            world.Trees.Add(0.1 * i, 0.5, TreeSpeciesId.Birch, 0);
        }

        var v = world.Trees.Version;
        world.Step((10 * Day) - 1);
        Assert.Equal(v, world.Trees.Version);
        world.Step();
        Assert.Equal(v + 1, world.Trees.Version);
        Assert.All(world.Trees.Trees, t => Assert.Equal(4, t.AgeMonths));
        Assert.Equal(5, world.Date.Month);
        world.Step(Day);
        Assert.Equal(v + 1, world.Trees.Version);
    }

    [Fact]
    public void SkipToNextMonth_GoesToFirstOfNextMonth_AndGrowsOnce()
    {
        var world = new World(1);
        world.Trees.Add(0.5, 0.5, TreeSpeciesId.Oak, 0);
        world.Step((3 * Day) + 7);
        world.SkipToNextMonth();
        Assert.Equal((1, 5, 1), (world.Date.Day, world.Date.Month, world.Date.Year));
        Assert.Equal(4, world.Trees.Trees[0].AgeMonths);
        world.Step(20 * Day);
        Assert.Equal(Season.Summer, world.Season);
        Assert.Equal(12, world.Trees.Trees[0].AgeMonths);
    }

    [Fact]
    public void SameSeedSameCommands_SameChecksum()
    {
        static ulong Run(int skips)
        {
            var world = new World(5, HoleLoader.Load(TerrainTests.HolePath(), 2));
            world.Step(500);
            for (var i = 0; i < skips; i++)
            {
                world.SkipToNextMonth();
            }

            world.Step(500);
            return world.Checksum();
        }

        Assert.Equal(Run(3), Run(3));
        Assert.NotEqual(Run(3), Run(4));
    }
}
