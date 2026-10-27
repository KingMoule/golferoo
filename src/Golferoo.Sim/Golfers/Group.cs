using Golferoo.Sim.Save;

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

    /// <summary>Ligue : tick à partir duquel le groupe peut jouer son premier coup, sans attendre le groupe précédent.</summary>
    internal long ReleaseTick { get; set; }

    /// <summary>Le groupe a joué son premier coup (le groupe précédent avait quitté le green).</summary>
    public bool Started { get; internal set; }

    internal void Write(SaveWriter w)
    {
        w.Int(Id);
        w.Int(LeagueRound);
        w.Long(ReleaseTick);
        w.Bool(Started);
        w.List(Golfers, g => g.Write(w));
    }

    internal static Group Read(SaveReader r)
    {
        var (id, round, release, started) = (r.Int(), r.Int(), r.Long(), r.Bool());
        return new Group(id, r.List(() => Golfer.Read(r))) { LeagueRound = round, ReleaseTick = release, Started = started };
    }

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
