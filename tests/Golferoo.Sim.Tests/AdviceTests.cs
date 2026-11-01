using Golferoo.Sim.Economy;
using Golferoo.Sim.Golfers;
using Golferoo.Sim.Pro;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;

namespace Golferoo.Sim.Tests;

/// <summary>C19 lot 2 : les quatre conseils du pro, chacun une fois, la première année seulement, évalués au changement de jour.</summary>
public class AdviceTests
{
    private static World Club() =>
        new(1, ClubhouseTests.LeNeuf(), new StubShotResolver(), placeClubhouse: true, arrivals: ArrivalParams.Default with { GroupsPerDay = 0 });

    /// <summary>Joue jusqu'au jour suivant et rend les conseils donnés (identifiant, trou).</summary>
    private static List<(string Id, int Hole)> NextDay(World world)
    {
        var given = new List<(string, int)>();
        void Take(AdviceDef a, int hole) => given.Add((a.Id, hole));
        world.AdviceGiven += Take;
        var day = world.DayIndex;
        while (world.DayIndex == day)
        {
            world.Step();
        }

        world.AdviceGiven -= Take;
        return given;
    }

    private static void Wear(World world, int amount)
    {
        var t = world.Terrain!;
        for (var z = 0; z < t.CellsZ; z++)
        {
            for (var x = 0; x < t.CellsX; x++)
            {
                if (t.GetCellSurface(x, z) is Surface.Fairway or Surface.Green)
                {
                    t.Turf.AddWear(x, z, amount);
                }
            }
        }
    }

    [Fact]
    public void Catalog_HasTheFourAdvicesInTheirDataOrder_WithFrenchTexts()
    {
        Assert.Equal(["firstWalkout", "lowCash", "unplayedHole", "wornTurf"], AdviceCatalog.Default.Select(a => a.Id));
        Assert.All(AdviceCatalog.Default, a => Assert.Contains(' ', a.Text));
        Assert.Throws<InvalidDataException>(() => AdviceCatalog.Parse("""{ "advice": [] }"""));
    }

    [Fact]
    public void QuietClub_GivesNoAdviceOnTheFirstDay() => Assert.Empty(NextDay(Club()));

    [Fact]
    public void FirstWalkout_IsGivenOnceAWalkoutHappened()
    {
        var world = Club();
        world.State.Walkouts = 1;

        Assert.Equal([("firstWalkout", 0)], NextDay(world));
        Assert.Empty(NextDay(world));
    }

    [Fact]
    public void LowCash_IsGivenWhenTheBalanceFallsUnderTheThreshold_OnceOnly()
    {
        var world = Club();
        var threshold = AdviceCatalog.Default.Single(a => a.Condition == AdviceCondition.LowCash).Threshold;
        world.Ledger.Post(LedgerCategory.Buildings, -(world.Ledger.Balance - threshold + 1));
        Assert.Equal([("lowCash", 0)], NextDay(world));

        world.Ledger.Post(LedgerCategory.Buildings, threshold * 10);
        world.Ledger.Post(LedgerCategory.Buildings, -(world.Ledger.Balance - threshold + 1));
        Assert.Empty(NextDay(world));
    }

    [Fact]
    public void UnplayedHole_IsGivenAfterTheThresholdDays_WithoutAFinishedGame()
    {
        var world = Club();
        var days = (int)AdviceCatalog.Default.Single(a => a.Condition == AdviceCondition.UnplayedHole).Threshold;
        for (var d = 1; d < days; d++)
        {
            Assert.Empty(NextDay(world));
        }

        Assert.Equal([("unplayedHole", 1)], NextDay(world));
    }

    [Fact]
    public void WornTurf_IsGivenWhenEnoughFairwayAndGreenIsWorn_NotBefore()
    {
        var world = Club();
        Wear(world, 20);
        Assert.Empty(NextDay(world));

        Wear(world, 250);
        Assert.Equal([("wornTurf", 0)], NextDay(world));
    }

    [Fact]
    public void Advice_StopsAfterTheFirstYear()
    {
        var world = Club();
        for (var i = 0; i < 9; i++)
        {
            world.SkipToNextMonth(); // avril à décembre : la fin de l'année 1
        }

        world.SkipToNextMonth();
        Assert.Equal(2, world.Date.Year);
        world.State.Walkouts = 1;

        Assert.Empty(NextDay(world));
        Assert.Equal(0UL, world.State.AdviceGiven);
    }

    [Fact]
    public void ListeningToAdvice_DoesNotChangeTheChecksum()
    {
        var quiet = Club();
        var heard = Club();
        heard.AdviceGiven += (_, _) => { };
        quiet.State.Walkouts = heard.State.Walkouts = 1;
        for (var i = 0; i < 4; i++)
        {
            NextDay(quiet);
            NextDay(heard);
        }

        Assert.NotEqual(0UL, quiet.State.AdviceGiven);
        Assert.Equal(quiet.Checksum(), heard.Checksum());
    }
}
