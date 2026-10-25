using Golferoo.Sim.Time;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Tests;

public class FoliageTests
{
    [Fact]
    public void Position_CountsMonthsFromJanuaryFirst()
    {
        Assert.Equal(8.5, Foliage.Position(new Date(6, 9, 1, Season.Autumn, 0)), 1e-12);
        Assert.Equal(3.05, Foliage.Position(new Date(1, 4, 1, Season.Spring, 0.5)), 1e-12);
    }

    [Theory]
    [InlineData(7.0, 0, 0)] // juillet : vert, toutes les feuilles
    [InlineData(8.5, 0.5, 0)] // mi-septembre : à mi-chemin du roux
    [InlineData(10.2, 1, 2)] // novembre : roux, moitié des feuilles
    [InlineData(0.5, 2, 3)] // janvier : brun terne, un quart des feuilles
    [InlineData(2.5, 2.5, 3)] // mi-mars : du brun vers le vert, sans roux
    [InlineData(4.5, 0, 0)] // mai : vert, toutes les feuilles revenues
    public void Year_FollowsTheSeasons(double position, double colour, int stage)
    {
        Assert.Equal(colour, Foliage.Colour(position), 1e-9);
        Assert.Equal(stage, Foliage.Stage(position));
    }

    [Fact]
    public void EveryDay_ChangesLittle_AndStagesStepOneAtATime()
    {
        var day = 1.0 / Calendar.DaysPerMonth;
        for (var p = 0.0; p < Calendar.MonthsPerYear; p += day)
        {
            var change = Math.Abs(Foliage.Colour(p + day) - Foliage.Colour(p));
            Assert.InRange(Math.Min(change, Foliage.ColourCycle - change), 0, 0.15);
            Assert.InRange(Math.Abs(Foliage.Stage(p + day) - Foliage.Stage(p)), 0, 1);
        }

        Assert.Equal(Foliage.Colour(0.3), Foliage.Colour(12.3), 1e-12);
    }

    [Fact]
    public void Steady_InSummerAndWinter_NotInAutumnOrSpring()
    {
        Assert.True(Foliage.Steady(6.5, 0.4));
        Assert.True(Foliage.Steady(0.5, 0.4));
        Assert.False(Foliage.Steady(8.5, 0.4));
        Assert.False(Foliage.Steady(7.8, 0.4)); // un arbre en avance commence à rougir
        Assert.False(Foliage.Steady(2.0, 0.4));
    }
}
