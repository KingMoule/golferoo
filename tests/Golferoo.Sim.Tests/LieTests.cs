using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Xunit.Abstractions;

namespace Golferoo.Sim.Tests;

/// <summary>C25 lot 2 : pénalité de lie et backspin selon le club et le lie, pour tous les golfeurs.</summary>
public class LieTests(ITestOutputHelper output)
{
    private static readonly Point2 Start = new(20, 240);

    private static double Dist(Point2 a, Point2 b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));

    [Fact]
    public void Backspin_IsStrongestAtTheWedge_FallsUpTheIrons_AndIsNearlyNilFromTheLongIrons()
    {
        var spins = new[] { Clubs.Wedge, Clubs.Iron9, Clubs.Iron7, Clubs.Iron5, Clubs.Wood3, Clubs.Driver }.Select(c => c.Backspin).ToArray();
        for (var i = 1; i < spins.Length; i++)
        {
            Assert.True(spins[i] < spins[i - 1], $"le backspin doit décroître : {string.Join(", ", spins)}");
        }

        Assert.True(Clubs.Iron5.Backspin <= 0.05);
        Assert.True(Clubs.Driver.Backspin <= Clubs.Iron5.Backspin);
    }

    [Fact]
    public void LieParams_RoughAndBunkerCostMore_ForAHigherHandicap_AndNothingElseDoes()
    {
        var p = LieParams.Default;
        foreach (var lie in new[] { Surface.Rough, Surface.Bunker })
        {
            var (scratch, bogey) = (p.At(lie, 0.0), p.At(lie, 36.0));
            Assert.True(scratch.Speed < 1.0 && scratch.Sigma > 1.0 && scratch.Backspin < 1.0);
            Assert.True(bogey.Speed < scratch.Speed);
            Assert.True(bogey.Sigma > scratch.Sigma);
        }

        Assert.True(p.At(Surface.Bunker, 20.0).Speed < p.At(Surface.Rough, 20.0).Speed);
        foreach (var lie in new[] { Surface.Fairway, Surface.Tee, Surface.Path, Surface.Green })
        {
            Assert.Equal(LieEffect.None, p.At(lie, 36.0));
        }
    }

    [Theory]
    [InlineData(Surface.Rough, 2.1)]
    [InlineData(Surface.Bunker, 1.0)]
    public void OrdinaryGolfer_LosesSpeedAndGainsDispersionFromTheLie(Surface surface, double skill)
    {
        var terrain = ShotTests.Uniform(surface, cells: 120);
        var resolver = new PhysicalShotResolver();
        (double Carry, double Lateral) Mean(LieEffect? lie)
        {
            var rng = new DeterministicRng(5);
            double carry = 0, lateral = 0;
            const int N = 1500;
            for (var i = 0; i < N; i++)
            {
                var r = resolver.Execute(Start, Clubs.Iron7, 1.0, 0.0, 1.0, terrain, rng, skill, lie: lie);
                carry += r.Ball.X - Start.X;
                lateral += Math.Abs(r.Ball.Z - Start.Z);
            }

            return (carry / N, lateral / N);
        }

        var clean = Mean(LieEffect.None);
        var lied = Mean(null);
        output.WriteLine($"{surface} niveau {skill} : portée {clean.Carry:F1} -> {lied.Carry:F1} m ; latéral {clean.Lateral:F2} -> {lied.Lateral:F2} m");
        Assert.True(lied.Carry < clean.Carry - 2.0);

        // Écart latéral rapporté à la portée (la balle va moins loin) : l'angle de départ s'ouvre.
        Assert.True(lied.Lateral / lied.Carry > 1.05 * clean.Lateral / clean.Carry);
    }

    [Fact]
    public void Backspin_ShortensTheRollOfAWedge_ForAnOrdinaryGolfer_AndLessSoFromTheRough()
    {
        var wedgeNoSpin = Clubs.Wedge with { Backspin = 0.0 };
        double Total(Club club, Surface from)
        {
            var terrain = ShotTests.Uniform(Surface.Fairway, cells: 120);
            var rng = new DeterministicRng(3);
            var resolver = new PhysicalShotResolver();

            // Même départ, même sol : seul le backspin change (vitesse et dispersion du lie neutralisées).
            var effect = from == Surface.Fairway ? LieEffect.None : LieParams.Default.At(from, 0.0) with { Speed = 1.0, Sigma = 1.0 };
            double sum = 0;
            for (var i = 0; i < 400; i++)
            {
                sum += Dist(Start, resolver.Execute(Start, club, 1.0, 0.0, 1.0, terrain, rng, 1.0, lie: effect).Ball);
            }

            return sum / 400;
        }

        var (none, fairway, rough) = (Total(wedgeNoSpin, Surface.Fairway), Total(Clubs.Wedge, Surface.Fairway), Total(Clubs.Wedge, Surface.Rough));
        output.WriteLine($"wedge, portée totale : sans backspin {none:F1} ; fairway {fairway:F1} ; rough {rough:F1} m");
        Assert.True(fairway < none - 1.0);
        Assert.True(rough > fairway);
        Assert.True(rough < none + 0.5);
    }

    [Fact]
    public void Planning_SeesTheSpeedLostFromTheLie_SoTheExecutedShotLandsWhereItWasPlanned()
    {
        var terrain = ShotTests.Uniform(Surface.Bunker, cells: 120, hole: new Point2(110, 240));
        var resolver = new PhysicalShotResolver();
        var request = new ShotRequest(Start, new Point2(110, 240), 1, 1.0);
        resolver.Resolve(request, terrain, new DeterministicRng(1), out var planned);
        var plan = planned!.Value;
        var rng = new DeterministicRng(9);
        double sum = 0;
        const int N = 600;
        for (var i = 0; i < N; i++)
        {
            sum += Dist(Start, resolver.ExecutePlan(request, plan, terrain, rng).Ball);
        }

        var (expected, mean) = (Dist(Start, plan.Landing), sum / N);
        output.WriteLine($"prévu {expected:F1} m ; exécuté en moyenne {mean:F1} m ({plan.Club.Name}, force {plan.Force:F2})");
        Assert.InRange(mean, expected - 3.0, expected + 3.0);
    }
}
