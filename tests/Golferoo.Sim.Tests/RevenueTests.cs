using Golferoo.Sim.Economy;
using Golferoo.Sim.Golfers;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;
using Golferoo.Sim.Trees;
using Golferoo.Sim.Turf;
using Xunit.Abstractions;

namespace Golferoo.Sim.Tests;

/// <summary>C14 lot 2 : green fees, cotisations, test de rentabilité de « Le neuf ».</summary>
public class RevenueTests(ITestOutputHelper output)
{
    private const int MaxTicks = 400_000;

    /// <summary>Green fees encaissés depuis le début : mois clos et mois en cours.</summary>
    private static long GreenFees(World world) =>
        world.Ledger.Months.Sum(m => m.Total(LedgerCategory.GreenFees)) + world.Ledger.Current(LedgerCategory.GreenFees);

    private static void PlayOut(World world)
    {
        for (var i = 0; i < MaxTicks && !world.Groups.All(g => g.Finished && g.Golfers.All(o => o.State == GolferState.Done)); i++)
        {
            world.Step();
        }

        Assert.All(world.Groups, g => Assert.True(g.Finished, "la partie n'est pas finie"));
    }

    [Fact]
    public void Visitor_Pays15DollarsPerHolePlayed_AndAClosedHolePaysNothing()
    {
        var open = new World(3, CourseTests.ThreeHoles());
        open.SpawnGroup();
        PlayOut(open);
        Assert.Equal(2 * 3 * 1_500, GreenFees(open));

        var terrain = CourseTests.ThreeHoles();
        terrain.Blank = true;
        var h2 = terrain.Course[2];
        h2.MoveTee(new Point2(h2.Tee.X + 100, h2.Tee.Z)); // plus sur un départ : HoleCheck ferme le trou 2
        var closed = new World(3, terrain);
        closed.Step();
        Assert.False(closed.IsHoleOpen(2));
        closed.SpawnGroup();
        PlayOut(closed);
        Assert.Equal(2 * 2 * 1_500, GreenFees(closed));
    }

    [Fact]
    public void GreenFee_VisitorMemberAndPro()
    {
        var world = new World(1, CourseTests.ThreeHoles(), league: true);
        var g = world.SpawnGroup().Golfers[0];
        Assert.Equal(1_500, world.GreenFee(g));

        g.MemberId = world.League!.Members.First(m => !m.IsPro).Id;
        Assert.Equal(1_200, world.GreenFee(g));

        g.MemberId = world.League.Members.First(m => m.IsPro).Id;
        Assert.Equal(0, world.GreenFee(g));

        var changed = new World(1, CourseTests.ThreeHoles(), league: true, economy: EconomyTests.With(("greenFeeMember", 700)));
        var h = changed.SpawnGroup().Golfers[0];
        h.MemberId = 0;
        Assert.Equal(700, changed.GreenFee(h));
    }

    [Fact]
    public void MonthEnd_CollectsATwelfthOfEachMembersDues()
    {
        var world = new World(1, CourseTests.ThreeHoles(), league: true);
        Assert.Equal(40, world.League!.Members.Count(m => !m.IsPro));

        world.SkipToNextMonth();

        var line = world.Ledger.Months[0];
        Assert.Equal(40 * 3_000, line.Total(LedgerCategory.Dues));
        Assert.Equal(15_000_000 - 330_000 + 120_000, world.Ledger.Balance);

        // Sans ligue, aucun membre : aucune cotisation.
        var alone = new World(1, CourseTests.ThreeHoles());
        alone.SkipToNextMonth();
        Assert.Equal(0, alone.Ledger.Months[0].Total(LedgerCategory.Dues));
    }

    private static TerrainGrid LeNeuf() =>
        CourseLoader.Load(Path.Combine(Path.GetDirectoryName(TerrainTests.HolePath())!, "..", "courses", "le-neuf.json"), 2);

    /// <summary>Green fees d'une journée où <paramref name="pairs"/> paires de visiteurs arrivent, jouées jusqu'au bout.</summary>
    private static long DayOfPlay(int pairs)
    {
        if (pairs == 0)
        {
            return 0;
        }

        var world = new World(7, LeNeuf(), new StubShotResolver());
        for (var i = 0; i < pairs; i++)
        {
            world.SpawnGroup();
        }

        PlayOut(world);
        return GreenFees(world);
    }

    /// <summary>Entretien d'un mois de « Le neuf » (9 trous ouverts), lu dans la ligne du mois.</summary>
    private static long MonthlyMaintenance(bool demanding)
    {
        var world = new World(7, LeNeuf(), new StubShotResolver());
        if (demanding)
        {
            world.SetTurfSettings(TurfSettings.Normal with { GreenSpeed = GreenSpeed.Fast });
            world.Step();
        }

        world.SkipToNextMonth();
        return world.Ledger.Months[0].Total(LedgerCategory.Maintenance);
    }

    /// <summary>Résultat d'exploitation d'une année : une journée simulée par niveau d'affluence, multipliée par les jours de sa saison, moins douze mois d'entretien.</summary>
    private long Year(Func<Season, int> pairs, bool demanding)
    {
        var revenue = 0L;
        foreach (var level in Enumerable.Range(1, Calendar.MonthsPerYear).GroupBy(m => pairs(Calendar.SeasonOf(m))))
        {
            var day = DayOfPlay(level.Key);
            revenue += day * level.Count() * Calendar.DaysPerMonth;
            output.WriteLine($"{level.Key} paires : {Ledger.Dollars(day)} par jour, {level.Count() * Calendar.DaysPerMonth} jours");
        }

        var maintenance = MonthlyMaintenance(demanding) * Calendar.MonthsPerYear;
        output.WriteLine($"revenus {Ledger.Dollars(revenue)}, entretien {Ledger.Dollars(maintenance)}, résultat {Ledger.Dollars(revenue + maintenance)}");
        return revenue + maintenance;
    }

    /// <summary>
    /// Critère 8 : un 9 trous fréquenté (12 paires par jour l'été, 6 au printemps et à l'automne, 1 l'hiver) a un résultat annuel positif ;
    /// avec moitié moins de visiteurs et des consignes exigeantes, il perd de l'argent sans approcher le seuil de faillite de C18.
    /// </summary>
    [Fact]
    public void LeNeuf_Busy_IsProfitable_AndAPoorCourseLosesLessThanTheBankruptcyThreshold()
    {
        var busy = Year(s => s switch { Season.Summer => 12, Season.Winter => 1, _ => 6 }, demanding: false);
        Assert.True(busy > 0, $"résultat annuel {Ledger.Dollars(busy)}");

        var poor = Year(s => s switch { Season.Summer => 6, Season.Winter => 0, _ => 3 }, demanding: true);
        Assert.InRange(poor, -EconomyParams.Facile.BankruptcyThreshold + 1, -1);
    }
}
