namespace Golferoo.Sim.Scoring;

public enum MatchWinner
{
    Halved = 0,
    A = 1,
    B = 2,
}

/// <summary>Match play : deux cartes comparées trou par trou, du point de vue de A.</summary>
public readonly record struct MatchResult(int Won, int Halved, int Lost)
{
    public MatchWinner Winner => Won > Lost ? MatchWinner.A : Lost > Won ? MatchWinner.B : MatchWinner.Halved;

    public static MatchResult Play(ScoreCard a, ScoreCard b, int handicapA = 0, int handicapB = 0)
    {
        if (a.HoleCount != b.HoleCount)
        {
            throw new ArgumentException("Les deux cartes doivent compter autant de trous.");
        }

        int won = 0, halved = 0, lost = 0;
        for (var h = 0; h < a.HoleCount; h++)
        {
            var d = a.NetStrokes(h, handicapA) - b.NetStrokes(h, handicapB);
            if (d < 0)
            {
                won++;
            }
            else if (d > 0)
            {
                lost++;
            }
            else
            {
                halved++;
            }
        }

        return new MatchResult(won, halved, lost);
    }
}
