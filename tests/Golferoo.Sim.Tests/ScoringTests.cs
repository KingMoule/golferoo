using Golferoo.Sim.Scoring;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Tests;

public class ScoringTests
{
    private static ScoreCard Card(int[] pars, int[] strokes)
    {
        var c = new ScoreCard(pars);
        for (var h = 0; h < strokes.Length; h++)
        {
            c.Record(h, strokes[h]);
        }

        return c;
    }

    // Quatre trous : par 4, 3, 5, 4 ; birdie, bogey, double bogey, par.
    private static readonly int[] Pars = [4, 3, 5, 4];
    private static ScoreCard Sample() => Card(Pars, [3, 4, 7, 4]);

    [Fact]
    public void Gross_And_Net()
    {
        var c = Sample();

        Assert.Equal(18, c.Gross);
        Assert.Equal(16, c.TotalPar);
        Assert.Equal(14, c.Net(4));
    }

    [Fact]
    public void Stableford_Gross_And_Net()
    {
        var c = Sample();

        Assert.Equal(3 + 1 + 0 + 2, c.Stableford());
        // Handicap 5 sur 4 trous : un coup partout, un deuxième sur le premier trou.
        Assert.Equal(2, c.HandicapStrokes(0, 5));
        Assert.Equal(1, c.HandicapStrokes(3, 5));
        Assert.Equal(5 + 2 + 1 + 3, c.Stableford(5));
    }

    [Fact]
    public void Stableford_Scale_FromDoubleBogeyToAlbatross()
    {
        var c = Card([5, 5, 5, 5, 5, 5], [2, 3, 5, 6, 7, 12]);

        Assert.Equal([5, 4, 2, 1, 0, 0], Enumerable.Range(0, 6).Select(h => c.StablefordPoints(h)));
    }

    [Fact]
    public void VersusPar_CountsHolesWonAndLost()
    {
        var c = Sample();

        Assert.Equal(1, c.VersusParHole(0));
        Assert.Equal(-1, c.VersusParHole(1));
        Assert.Equal(-1, c.VersusParHole(2));
        Assert.Equal(0, c.VersusParHole(3));
        Assert.Equal(-1, c.VersusPar());
        Assert.Equal(1, c.VersusPar(5));
    }

    [Fact]
    public void PickedUpBall_CountsMaxStrokes()
    {
        var c = new ScoreCard([4]);

        c.PickUp(0);

        Assert.Equal(12, c.Strokes(0));
        Assert.Equal(World.MaxStrokes, c.Gross);
        Assert.Equal(0, c.StablefordPoints(0));
    }

    [Fact]
    public void MatchPlay_WonHalvedLost_AndWinner()
    {
        var a = Card(Pars, [3, 3, 6, 4]);
        var b = Card(Pars, [4, 3, 5, 5]);

        var m = MatchResult.Play(a, b);

        Assert.Equal(new MatchResult(2, 1, 1), m);
        Assert.Equal(MatchWinner.A, m.Winner);
        Assert.Equal(MatchWinner.B, MatchResult.Play(b, a).Winner);
        var halved = MatchResult.Play(Card(Pars, [4, 3, 5, 5]), Card(Pars, [5, 3, 5, 4]));
        Assert.Equal(new MatchResult(1, 2, 1), halved);
        Assert.Equal(MatchWinner.Halved, halved.Winner);
    }

    [Fact]
    public void Leaderboard_TiesShareRank_AndKeepInputOrder()
    {
        var players = new[] { ("a", 70), ("b", 68), ("c", 70), ("d", 75), ("e", 68), ("f", 70) };

        var ranked = Leaderboard.Rank(players, p => p.Item2);

        Assert.Equal(["b", "e", "a", "c", "f", "d"], ranked.Select(r => r.Item.Item1));
        Assert.Equal([1, 1, 3, 3, 3, 6], ranked.Select(r => r.Rank));
        Assert.Equal(["T1", "T1", "T3", "T3", "T3", "6"], ranked.Select(r => r.Label));
    }

    [Fact]
    public void Leaderboard_RanksOneTwoTwoFour_AndHigherIsBetter()
    {
        var ranked = Leaderboard.Rank([10, 8, 8, 5], p => p, higherIsBetter: true);

        Assert.Equal([1, 2, 2, 4], ranked.Select(r => r.Rank));
        Assert.Equal(["1", "T2", "T2", "4"], ranked.Select(r => r.Label));
    }

    [Fact]
    public void Par_ReadFromFile_AndDeducedFromDistance()
    {
        var proto = HoleLoader.Load(TerrainTests.HolePath(), 2);
        Assert.Equal(4, proto.Par);

        var t = new Point2(0, 0);
        Assert.Equal(3, TerrainGrid.DeducedPar(t, new Point2(230, 0)));
        Assert.Equal(4, TerrainGrid.DeducedPar(t, new Point2(231, 0)));
        Assert.Equal(4, TerrainGrid.DeducedPar(t, new Point2(430, 0)));
        Assert.Equal(5, TerrainGrid.DeducedPar(t, new Point2(431, 0)));
    }

    [Fact]
    public void Par_WithoutField_IsDeducedFromTeeToHole()
    {
        var json = File.ReadAllText(TerrainTests.HolePath()).Replace("\"par\": 4,", string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("\"par\"", json, StringComparison.Ordinal);

        Assert.Equal(4, HoleLoader.Parse(json, 2).Par);
    }
}
