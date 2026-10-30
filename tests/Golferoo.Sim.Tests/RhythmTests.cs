using Golferoo.Sim.Golfers;
using Xunit.Abstractions;

namespace Golferoo.Sim.Tests;

/// <summary>C15 lot 2b : rythme de jeu (marche des golfeurs IA) et critère 4 final des arrivées.</summary>
public class RhythmTests(ITestOutputHelper output)
{
    private const int Day = Time.Calendar.TicksPerDay;

    private static bool Walks(GolferState s) => s is GolferState.Arriving or GolferState.WalkingToBall or GolferState.ToNextTee or GolferState.Exiting or GolferState.Leaving;

    /// <summary>Part du temps passée à marcher par un groupe de 2 seul sur « Le neuf » (graine 1, résolveur physique).</summary>
    internal static (double Walk, double Flight, int Ticks) WalkShare(ArrivalParams? arrivals = null)
    {
        var world = new World(1, ClubhouseTests.LeNeuf(), league: false, arrivals: arrivals);
        var group = world.SpawnGroup();
        int walk = 0, flight = 0, total = 0;
        while (!group.Finished && total < 20 * Day)
        {
            world.Step();
            total++;

            // Temps du groupe : le vol d'une balle prime, puis la marche (tous les golfeurs marchent ou ont fini) ; le reste, ce sont les attentes et les coups.
            if (group.Golfers.Any(g => g.State == GolferState.BallInFlight))
            {
                flight++;
            }
            else if (group.Golfers.All(g => Walks(g.State) || g.State == GolferState.Done))
            {
                walk++;
            }
        }

        Assert.True(group.Finished, "le groupe n'a pas fini son tour");
        return (walk / (double)total, flight / (double)total, total);
    }

    [Fact]
    public void IsolatedGroup_WalksAboutHalfOfItsRound()
    {
        var before = WalkShare(ArrivalParams.Default with { AiWalkSpeedFactor = 1.0, AiFlightSpeedFactor = 1.0 });
        var after = WalkShare();
        output.WriteLine($"facteur 1 : marche {before.Walk:P0}, vol {before.Flight:P0}, {before.Ticks} ticks ; facteur {ArrivalParams.Default.AiWalkSpeedFactor} : marche {after.Walk:P0}, vol {after.Flight:P0}, {after.Ticks} ticks");

        Assert.InRange(after.Walk, 0.45, 0.55);
    }

    /// <summary>« Le neuf » ouvert, graine 1, 30 jours de printemps et d'été : départs de visiteurs par jour, part de refus, réputation finale.</summary>
    internal static (double DeparturesPerDay, double RefusedShare, double Reputation) Month(ArrivalParams arrivals, bool physical, int days = 30)
    {
        var world = ArrivalTests.NewWorld(1, arrivals, physical);
        int arrived = 0, refused = 0;
        world.GroupArrived += _ => arrived++;
        world.GroupTurnedAway += _ => refused++;
        var released = new HashSet<int>();
        for (var i = 0; i < days * Day; i++)
        {
            world.Step();
            foreach (var g in world.Groups.Where(g => g.FromArrivals && g.Released))
            {
                released.Add(g.Id);
            }
        }

        return (released.Count / (double)days, refused / (double)(arrived + refused), world.Reputation);
    }

    private void AssertFinalCriterion(bool physical)
    {
        var (departures, refused, reputation) = Month(ArrivalParams.Default, physical);
        output.WriteLine($"{(physical ? "physique" : "simplifié")} : {departures:0.0} départs par jour, {refused:P0} de refus, réputation {reputation:0.00}");

        Assert.InRange(departures, 5.0, 8.0);
        Assert.True(refused <= 0.10, $"{refused:P0} de refus");
        Assert.True(reputation > 0.8, $"réputation {reputation:0.00}");
    }

    /// <summary>Critère 4 final avec le résolveur simplifié (la CI).</summary>
    [Fact]
    public void LeNeuf_HoldsTheFinalCriterion() => AssertFinalCriterion(physical: false);
}
