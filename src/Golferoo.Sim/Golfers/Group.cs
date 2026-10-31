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

    /// <summary>Groupe de visiteurs arrivés au pavillon (file, départ, sortie par la porte) ; les autres (ligue, touche G) n'y passent pas.</summary>
    public bool FromArrivals => Golfers[0].VisitorId >= 0;

    /// <summary>Un golfeur au moins marche vers la porte pour sortir.</summary>
    public bool Leaving => Golfers.Any(g => g.State == GolferState.Leaving);

    /// <summary>Le groupe a quitté le monde ce tick : il sort de la liste en fin de tick (jamais sauvé).</summary>
    internal bool Left { get; set; }

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
        w.Long(ArrivedTick);
    }

    /// <summary><paramref name="tick"/> : le tick de la sauvegarde, que prend l'arrivée d'un groupe sauvé avant la v14.</summary>
    internal static Group Read(SaveReader r, long tick)
    {
        var (id, round, release, started, hole, released) = (r.Int(), r.Int(), r.Long(), r.Bool(), r.Int(), r.Bool());
        var group = new Group(id, r.List(() => Golfer.Read(r))) { LeagueRound = round, ReleaseTick = release, Started = started, Hole = hole, Released = released };
        group.ArrivedTick = r.Version >= 14 ? r.Long() : tick;
        return group;
    }

    /// <summary>Tick où le groupe est entré dans la file de départ (C16 : l'attente au départ se mesure à partir de là).</summary>
    internal long ArrivedTick { get; set; }

    /// <summary>Les golfeurs qui jouent encore : ceux qui n'ont pas abandonné.</summary>
    internal IEnumerable<Golfer> Active => Golfers.Where(g => !g.Quit);

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
