using Golferoo.Sim.Buildings;
using Golferoo.Sim.Economy;
using Golferoo.Sim.Golfers;
using Golferoo.Sim.League;
using Golferoo.Sim.Objectives;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Tests;

/// <summary>C18 lot 2 : objectifs (un test par objectif, sur la vraie date de <see cref="Calendar"/>), faillite sur le vrai grand livre, bilan annuel, déterminisme.</summary>
public class ObjectiveTests
{
    private static readonly Date Day2 = new(2, 4, 1, Season.Spring, 0.0);

    private static ObjectiveDef Def(string id) => ObjectiveCatalog.Default.Single(o => o.Id == id);

    /// <summary>Une carrière sans terrain : le grand livre, le vivier et le calendrier suffisent aux objectifs qui ne comptent pas de trous.</summary>
    private static World Bare(DifficultyParams? difficulty = null, bool objectives = true) => new(1, difficulty: difficulty, objectives: objectives);

    private static World Career(TerrainGridFactory terrain, bool league = false) => new(1, terrain(), new StubShotResolver(), league: league, objectives: true);

    private delegate TerrainGrid TerrainGridFactory();

    /// <summary>Joue jusqu'au changement de jour, en notant les objectifs levés avec la date de jeu à ce moment.</summary>
    private static List<(string Id, Date At)> NextDay(World world)
    {
        var done = new List<(string, Date)>();
        world.ObjectiveCompleted += o => done.Add((o.Id, world.Date));
        var day = world.DayIndex;
        while (world.DayIndex == day)
        {
            world.Step();
        }

        return done;
    }

    private static void AddMember(World world, MemberLevel level) => world.Visitors.Add("Membre", 20.0, Archetype.Neutral, 0.5, level);

    private static void CloseMonths(World world, int months)
    {
        for (var i = 0; i < months; i++)
        {
            world.SkipToNextMonth();
        }
    }

    [Fact]
    public void Catalog_HasTheSevenAlphaObjectives_EachWithAMoneyReward()
    {
        Assert.Equal(["holes3", "holes6", "holes9", "members10", "gold", "positiveYear", "proDivision"], ObjectiveCatalog.Default.Select(o => o.Id));
        Assert.All(ObjectiveCatalog.Default, o => Assert.True(DifficultyParams.Facile.ObjectiveRewards[o.Id] > 0));
        Assert.All(ObjectiveCatalog.Default, o => Assert.False(string.IsNullOrWhiteSpace(o.Text)));
    }

    [Theory]
    [InlineData("""{ "objectives": [] }""")]
    [InlineData("""{ "objectives": [ { "id": "a", "condition": "openHoles", "threshold": 0, "title": "A", "text": "" } ] }""")]
    [InlineData("""{ "objectives": [ { "id": "a", "condition": "openHoles", "threshold": 1, "title": "A", "text": "" }, { "id": "a", "condition": "members", "threshold": 1, "title": "B", "text": "" } ] }""")]
    [InlineData("""{ "objectives": [ { "id": "a", "condition": "inconnue", "threshold": 1, "title": "A", "text": "" } ] }""")]
    public void Catalog_RefusesIncoherentFiles(string json) => Assert.Throws<InvalidDataException>(() => ObjectiveCatalog.Parse(json));

    [Fact]
    public void Holes3_FiresOnDayTwo_OnceOnly()
    {
        var world = Career(CourseTests.ThreeHoles);
        var before = world.Ledger.Balance;
        var done = NextDay(world);

        Assert.Equal([("holes3", Day2)], done);
        Assert.Equal(before + DifficultyParams.Facile.ObjectiveRewards["holes3"], world.Ledger.Balance);
        Assert.Equal(DifficultyParams.Facile.ObjectiveRewards["holes3"], world.Ledger.Current(LedgerCategory.Objectives));
        Assert.True(world.State.IsCompleted(0));

        Assert.Empty(NextDay(world));
        Assert.Equal(before + DifficultyParams.Facile.ObjectiveRewards["holes3"], world.Ledger.Balance);
    }

    [Fact]
    public void Holes6_Fires_WithSixOpenHoles_ButNotHoles9()
    {
        var world = Career(ClubhouseTests.LeNeuf);
        for (var i = 0; i < 3; i++)
        {
            Assert.True(world.RemoveLastHole());
            world.Step();
        }

        Assert.Equal(6, world.OpenHoles);
        Assert.Equal(["holes3", "holes6"], NextDay(world).Select(d => d.Id));
        Assert.False(world.State.IsCompleted(2));
    }

    [Fact]
    public void Holes9_Fires_WithNineOpenHoles()
    {
        var world = Career(ClubhouseTests.LeNeuf);
        var done = NextDay(world);

        Assert.Equal(["holes3", "holes6", "holes9"], done.Select(d => d.Id));
        Assert.All(done, d => Assert.Equal(Day2, d.At));
        Assert.Equal(3, world.State.CompletedCount);
    }

    [Fact]
    public void Members10_Fires_WhenTheTenthMemberJoins_AndGivesASnackBarVoucher()
    {
        var world = Bare();
        for (var i = 0; i < 9; i++)
        {
            AddMember(world, MemberLevel.Member);
        }

        AddMember(world, MemberLevel.Visitor); // un visiteur n'est pas un membre
        Assert.Empty(NextDay(world));

        AddMember(world, MemberLevel.Silver);
        Assert.Equal([("members10", new Date(3, 4, 1, Season.Spring, 0.0))], NextDay(world));
        Assert.Equal(1, world.State.Vouchers(BuildingKind.SnackBar));
        Assert.Equal(DifficultyParams.Facile.ObjectiveRewards["members10"], world.Ledger.Current(LedgerCategory.Objectives));
    }

    [Fact]
    public void Gold_Fires_WhenAMemberReachesGold_AndGivesAProShopVoucher()
    {
        var world = Bare();
        AddMember(world, MemberLevel.Silver);
        Assert.Empty(NextDay(world));

        AddMember(world, MemberLevel.Gold);
        Assert.Equal([("gold", new Date(3, 4, 1, Season.Spring, 0.0))], NextDay(world));
        Assert.Equal(1, world.State.Vouchers(BuildingKind.ProShop));
    }

    [Fact]
    public void ProDivision_Fires_OnlyWhenTheProIsPromoted_NotAnotherMember()
    {
        var world = Career(ClubhouseTests.LeNeuf, league: true);
        var other = world.League!.Members.First(m => !m.IsPro).Id;
        world.League.Events.Add(new LeagueEvent(0, LeagueEventType.Promoted, other, 0));
        world.League.Events.Add(new LeagueEvent(0, LeagueEventType.Relegated, world.ProMemberId, 1));
        Assert.DoesNotContain("proDivision", NextDay(world).Select(d => d.Id));

        world.League.Events.Add(new LeagueEvent(0, LeagueEventType.Promoted, world.ProMemberId, 0));
        Assert.Equal([("proDivision", new Date(3, 4, 1, Season.Spring, 0.0))], NextDay(world));
        Assert.Equal(1, world.State.Vouchers(BuildingKind.MaintenanceShed));
    }

    [Fact]
    public void PositiveYear_Fires_AfterADecemberClosedInTheGreen_NotInTheRed()
    {
        var red = Bare();
        red.Ledger.Post(LedgerCategory.Maintenance, -1_000_000);
        CloseMonths(red, 9);
        Assert.Single(red.State.Reports);
        Assert.True(red.State.Reports[0].Result < 0);
        Assert.DoesNotContain("positiveYear", NextDay(red).Select(d => d.Id));

        var green = Bare();
        green.Ledger.Post(LedgerCategory.GreenFees, 1_000_000);
        CloseMonths(green, 9);
        Assert.True(green.State.Reports[0].Result > 0);
        Assert.Contains("positiveYear", NextDay(green).Select(d => d.Id));
        Assert.Equal(DifficultyParams.Facile.ObjectiveRewards["positiveYear"], green.Ledger.Current(LedgerCategory.Objectives));
    }

    [Fact]
    public void DemoCourse_WithoutObjectives_CompletesNothing()
    {
        var world = Bare(objectives: false);
        for (var i = 0; i < 12; i++)
        {
            AddMember(world, MemberLevel.Gold);
        }

        Assert.Empty(NextDay(world));
        Assert.Equal(0UL, world.State.Completed);
    }

    [Fact]
    public void Voucher_PaysTheBuildingOnce_ButNotTheClearing()
    {
        var world = new World(1, ClubhouseTests.LeNeuf(), new StubShotResolver(), league: false, placeClubhouse: true, objectives: true);
        world.State.AddVoucher(BuildingKind.SnackBar);
        var cost = world.BuildingCost(BuildingKind.SnackBar)!.Value;

        var before = world.Ledger.Balance;
        VisitTests.Place(world, BuildingKind.SnackBar, world.ExitPointOf(5));
        Assert.True(before - world.Ledger.Balance < cost);
        Assert.Equal(0, world.State.Vouchers(BuildingKind.SnackBar));

        before = world.Ledger.Balance;
        VisitTests.Place(world, BuildingKind.SnackBar, world.ExitPointOf(5));
        Assert.True(before - world.Ledger.Balance >= cost);
    }

    // Faillite

    private static World Broke(int extra = 0)
    {
        var world = Bare();
        world.Ledger.Post(LedgerCategory.Maintenance, -(world.Ledger.Balance + world.Economy.BankruptcyThreshold + 1 + extra));
        return world;
    }

    [Fact]
    public void Bankruptcy_Fires_AtTheSixthMonthClosedUnderTheThreshold_Once()
    {
        var world = Broke();
        var raised = 0;
        world.Bankrupt += () => raised++;

        CloseMonths(world, (int)world.Economy.BankruptcyMonths - 1);
        Assert.Equal(0, raised);
        Assert.False(world.State.Bankrupt);
        Assert.Equal(5, world.State.MonthsBelow);

        CloseMonths(world, 1);
        Assert.Equal(1, raised);
        Assert.True(world.State.Bankrupt);

        CloseMonths(world, 3);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Bankruptcy_CounterGoesBackToZero_WhenAMonthEndsAboveTheThreshold()
    {
        var world = Broke();
        var raised = 0;
        world.Bankrupt += () => raised++;
        CloseMonths(world, 4);
        Assert.Equal(4, world.State.MonthsBelow);

        world.Ledger.Post(LedgerCategory.GreenFees, 50_000_000);
        CloseMonths(world, 1);
        Assert.Equal(0, world.State.MonthsBelow);

        world.Ledger.Post(LedgerCategory.Maintenance, -(world.Ledger.Balance + world.Economy.BankruptcyThreshold + 1));
        CloseMonths(world, 5);
        Assert.Equal(5, world.State.MonthsBelow);
        Assert.Equal(0, raised);
        Assert.False(world.State.Bankrupt);
    }

    [Fact]
    public void ContinueAfterBankruptcy_IsKept_AndTheScreenDoesNotComeBack()
    {
        var world = Broke();
        var raised = 0;
        world.Bankrupt += () => raised++;
        world.ContinueAfterBankruptcy();
        Assert.False(world.State.ContinuedAfterBankruptcy); // sans faillite, rien à continuer

        CloseMonths(world, (int)world.Economy.BankruptcyMonths);
        Assert.True(world.State.Bankrupt);
        world.ContinueAfterBankruptcy();
        Assert.True(world.State.ContinuedAfterBankruptcy);

        CloseMonths(world, 12);
        Assert.Equal(1, raised);
        Assert.True(world.State.Bankrupt && world.State.ContinuedAfterBankruptcy);
    }

    [Fact]
    public void Bankruptcy_IsReachable_WithTheCurrentOverdraftLimit_BecauseUpkeepAndInterestIgnoreIt()
    {
        // Le neuf sans bâtiment ni arrivée : 9 trous coûtent 9 900 $ par mois, le solde passe sous -100 000 $ au 25e mois (mesure de C18 lot 2).
        var world = new World(1, ClubhouseTests.LeNeuf(), new StubShotResolver(), placeClubhouse: true, arrivals: ArrivalParams.Default with { GroupsPerDay = 0 });
        var raised = 0;
        world.Bankrupt += () => raised++;
        CloseMonths(world, 24);
        Assert.True(world.Ledger.Balance > -world.Economy.BankruptcyThreshold);
        CloseMonths(world, 1);
        Assert.True(world.Ledger.Balance < -world.Economy.BankruptcyThreshold);
        CloseMonths(world, 5);
        Assert.Equal(1, raised);
    }

    // Bilan annuel

    [Fact]
    public void YearReport_EqualsTheSumOfTheClosedMonthsOfTheYear()
    {
        var world = Bare();
        YearReport? closed = null;
        world.YearClosed += r => closed = r;
        var monthly = new List<long[]>();
        world.Ledger.MonthClosed += m => monthly.Add([.. Enum.GetValues<LedgerCategory>().Select(c => m.Total(c))]);
        for (var i = 0; i < 9; i++)
        {
            world.Ledger.Post(LedgerCategory.GreenFees, 100_000 * (i + 1));
            world.Ledger.Post(LedgerCategory.Maintenance, -30_000);
            world.Ledger.Post(LedgerCategory.Surfaces, -(i * 1_000));
            Assert.Null(closed);
            world.SkipToNextMonth();
        }

        Assert.NotNull(closed);
        Assert.Equal(1, closed.Year);
        Assert.Equal(9, monthly.Count);
        Assert.Equal(Enum.GetValues<LedgerCategory>().Select(c => monthly.Sum(m => m[(int)c])), Enum.GetValues<LedgerCategory>().Select(closed.Total));
        Assert.Equal(world.Ledger.Months.Sum(m => m.Result), closed.Result);
        Assert.Equal(closed.ClosingBalance - closed.OpeningBalance, closed.Result);
        Assert.Equal(world.Economy.StartingMoney, closed.OpeningBalance);
        Assert.Equal(world.Ledger.Balance, closed.ClosingBalance);
        Assert.Equal(closed.Income + closed.Expenses, closed.Result);
        Assert.Equal(world.State.YearStartBalance, closed.ClosingBalance);

        // L'année suivante : douze mois, janvier à décembre seulement.
        world.Ledger.Post(LedgerCategory.GreenFees, 77_000);
        CloseMonths(world, 12);
        Assert.Equal(2, world.State.Reports.Count);
        Assert.Equal(2, world.State.Reports[1].Year);
        Assert.Equal(world.Ledger.Months.TakeLast(12).Sum(m => m.Result), world.State.Reports[1].Result);
        Assert.Equal(world.State.Reports[0].ClosingBalance, world.State.Reports[1].OpeningBalance);
    }

    [Fact]
    public void YearReport_KeepsGolfersMembersAndMood_AndStartsOverEachYear()
    {
        var world = Bare();
        AddMember(world, MemberLevel.Member);
        AddMember(world, MemberLevel.Gold);
        world.State.RecordExit(4, 10);
        world.State.RecordExit(2, 8);
        world.State.Complete(0);

        CloseMonths(world, 9);

        var report = world.State.Reports.Single();
        Assert.Equal(6, report.Golfers);
        Assert.Equal(2, report.Members);
        Assert.Equal(3.0, report.AverageMood);
        Assert.Equal(1, report.ObjectivesDone);
        Assert.Equal((0, 0L, 0), (world.State.YearGolfers, world.State.YearMoodTotal, world.State.YearObjectives));
        Assert.Equal(6, world.State.TotalGolfers);
    }

    // Déterminisme

    [Fact]
    public void SameSeedAndSameCommands_GiveTheSameChecksum_AfterTenDays()
    {
        static World Run()
        {
            var world = Career(CourseTests.ThreeHoles);
            world.Step(10 * Calendar.TicksPerDay);
            return world;
        }

        var a = Run();
        var b = Run();
        Assert.Equal(a.Checksum(), b.Checksum());
        Assert.True(a.State.IsCompleted(0));
    }

    // Difficulté branchée

    [Fact]
    public void MoodDrain_MultipliesTheDrops_AndLeavesTheGainsAlone()
    {
        static int Delta(double drain)
        {
            var scripted = new MoodTests.Scripted((r, t) => t.SurfaceAt(r.Ball.X, r.Ball.Z) == Surface.Green && r.StrokeNumber < 3
                ? new ShotResult(new Point2(r.Hole.X + 1.0, r.Hole.Z), false)
                : null);
            var world = new World(1, ClubhouseTests.LeNeuf(), scripted, league: false, placeClubhouse: true, difficulty: DifficultyParams.Facile with { MoodDrain = drain });
            var changes = new List<MoodChange>();
            world.MoodChanged += changes.Add;
            for (var i = 0; i < 4 * Calendar.TicksPerDay && !changes.Any(c => c.Event == MoodEvent.EasyPuttMissed); i++)
            {
                world.Step();
            }

            return changes.First(c => c.Event == MoodEvent.EasyPuttMissed).Delta;
        }

        var normal = Delta(1.0);
        Assert.True(normal < 0);
        Assert.Equal(normal * 2, Delta(2.0));
    }

    [Fact]
    public void GolferDemand_RaisesTheMoodNeededForAPerfectExit()
    {
        var mood = MoodParams.Default;
        Assert.Equal(mood.ExitScore(5), mood.ExitScore(5, 1.0));
        Assert.True(mood.ExitScore(2, 2.0) < mood.ExitScore(2, 1.0));
        Assert.True(mood.ExitScore(2, 0.5) > mood.ExitScore(2, 1.0));
        Assert.Equal(1.0, mood.ExitScore(MoodParams.Max, 0.0));
    }
}
