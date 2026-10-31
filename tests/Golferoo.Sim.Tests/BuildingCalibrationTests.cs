using Golferoo.Sim.Buildings;
using Golferoo.Sim.Golfers;
using Xunit.Abstractions;

namespace Golferoo.Sim.Tests;

/// <summary>
/// Calage du lot 2 de C17 (critère 6) sur « Le neuf », résolveur physique, sans ligue. Les visites ne dépendent pas des prix ni des revenus : on balaie les portées et les
/// probabilités (les seules choses qui changent le comportement), on relève les visites par bâtiment, puis on fixe prix et revenus sur ces mesures
/// (<c>specs/C17-batiments-amenagements.md</c>, « Calage du lot 2 »).
/// </summary>
public class BuildingCalibrationTests(ITestOutputHelper output)
{
    /// <summary>Une partie du balayage : bâtiments posés et règles de visite.</summary>
    private sealed record Config(string Name, bool Equipped, bool SecondSnack, int SnackRange, int ProRange, double SnackChance, double ProChance);

    private sealed record Outcome(Config Config, ulong Seed, int Golfers, int Quits, double MeanExit, int SnackVisits, int SecondSnackVisits, int ProVisits, int DaysRun);

    private static Outcome Measure(Config c, ulong seed, int days)
    {
        var rules = BuildingParams.Default with
        {
            SnackBar = BuildingParams.Default.SnackBar with { Range = c.SnackRange },
            ProShop = BuildingParams.Default.ProShop with { Range = c.ProRange },
            SnackBarChance = VisitTests.All(c.SnackChance),
            ProShopChance = VisitTests.All(c.ProChance),
        };
        int[] visits = new int[3];
        var run = MoodRun.Go(days, physical: true, seed, buildings: rules, setup: world =>
        {
            if (!c.Equipped)
            {
                return;
            }

            VisitTests.Place(world, BuildingKind.ProShop, VisitTests.ClubDoor(world));
            VisitTests.Place(world, BuildingKind.SnackBar, world.ExitPointOf(5));
            if (c.SecondSnack)
            {
                VisitTests.Place(world, BuildingKind.SnackBar, world.ExitPointOf(5));
            }

            VisitTests.Place(world, BuildingKind.MaintenanceShed, VisitTests.ClubDoor(world));
            world.MoodChanged += m =>
            {
                if (m.Event == MoodEvent.ServiceVisited && world.PlacedBuildings.ToList().FindIndex(b => b.DoorCell == m.Cell) is >= 0 and var i)
                {
                    var b = world.PlacedBuildings[i];
                    var slot = b.Kind == BuildingKind.ProShop ? 2 : world.PlacedBuildings.Where(o => o.Kind == BuildingKind.SnackBar).ToList().IndexOf(b);
                    lock (visits)
                    {
                        visits[slot]++;
                    }
                }
            };
        });
        return new Outcome(c, seed, run.Golfers, run.Quits, run.MeanExit, visits[0], visits[1], visits[2], days);
    }

    /// <summary>Le calage complet (12 parties de 30 jours, plusieurs minutes) : lancé si <c>GOLFEROO_LONG</c> est défini, en <c>-c Release</c>, avec <c>--logger "console;verbosity=detailed"</c>.</summary>
    [Fact]
    public void Sweep_ThirtyDays_ThreeSeeds()
    {
        if (Environment.GetEnvironmentVariable("GOLFEROO_LONG") is null)
        {
            return;
        }

        var configs = new[]
        {
            new Config("sans bâtiment", false, false, 0, 0, 0, 0),
            new Config("S+P+H, chances de base", true, false, 24, 12, 0.5, 0.3),
            new Config("S+P+H, chances hautes", true, false, 24, 12, 0.8, 0.6),
            new Config("S+S2+P+H, chances hautes", true, true, 24, 12, 0.8, 0.6),
        };
        var jobs = configs.SelectMany(c => new ulong[] { 1, 2, 3 }.Select(s => (c, s))).ToList();
        var results = new Outcome[jobs.Count];
        Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i => results[i] = Measure(jobs[i].c, jobs[i].s, 30));
        foreach (var r in results)
        {
            output.WriteLine($"{r.Config.Name} | graine {r.Seed} | golfeurs {r.Golfers} | abandons {r.Quits} ({(r.Golfers == 0 ? 0 : 100.0 * r.Quits / r.Golfers):0.0} %) | sortie {r.MeanExit:0.00} | snack-bar {r.SnackVisits} | 2e snack-bar {r.SecondSnackVisits} | pro shop {r.ProVisits}");
        }
    }

    /// <summary>Version bornée pour la CI : 8 jours, graine 1, un snack-bar, un pro shop et un hangar. Il y a des visites, et les abandons restent sous 8 % (la borne de C16).</summary>
    [Fact]
    public void EightDays_PhysicalResolver_VisitsHappen_AndQuitsStayLow()
    {
        var o = Measure(new Config("CI", true, false, 24, 12, 0.5, 0.3), 1, 8);

        Assert.True(o.SnackVisits + o.ProVisits > 0, "des visites ont eu lieu");
        Assert.True(o.Golfers >= 20);
        Assert.True(o.Quits < 0.08 * o.Golfers, $"{o.Quits} abandons sur {o.Golfers}");
        Assert.InRange(o.MeanExit, 1.0, 10.0);
    }
}
