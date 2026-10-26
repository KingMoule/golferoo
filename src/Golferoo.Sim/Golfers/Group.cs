namespace Golferoo.Sim.Golfers;

/// <summary>Une paire de golfeurs qui jouent ensemble.</summary>
public sealed class Group
{
    internal Group(int id, IReadOnlyList<Golfer> golfers)
    {
        Id = id;
        Golfers = golfers;
    }

    public int Id { get; }

    /// <summary>Journée de ligue de ce groupe (−1 hors ligue).</summary>
    public int LeagueRound { get; internal init; } = -1;

    public IReadOnlyList<Golfer> Golfers { get; }

    /// <summary>Le groupe a joué son premier coup (le groupe précédent avait quitté le green).</summary>
    public bool Started { get; internal set; }

    /// <summary>Tous ses golfeurs ont quitté le green.</summary>
    public bool Finished
    {
        get
        {
            foreach (var g in Golfers)
            {
                if (g.State != GolferState.Done)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
