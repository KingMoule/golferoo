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

    /// <summary>Le groupe a joué son premier coup.</summary>
    public bool Started { get; internal set; }

    /// <summary>Numéro (1 à N) du trou que joue le groupe.</summary>
    public int Hole { get; internal set; } = 1;

    /// <summary>Le groupe est parti de la file de départ : il marche et joue. Avant, ses golfeurs attendent à l'entrée du départ du trou 1.</summary>
    public bool Released { get; internal set; }

    internal void Write(SaveWriter w)
    {
        w.Int(Id);
        w.Int(LeagueRound);
        w.Long(ReleaseTick);
        w.Bool(Started);
        w.Int(Hole);
        w.Bool(Released);
        w.List(Golfers, g => g.Write(w));
    }

    internal static Group Read(SaveReader r)
    {
        var (id, round, release, started, hole, released) = (r.Int(), r.Int(), r.Long(), r.Bool(), r.Int(), r.Bool());
        return new Group(id, r.List(() => Golfer.Read(r))) { LeagueRound = round, ReleaseTick = release, Started = started, Hole = hole, Released = released };
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
