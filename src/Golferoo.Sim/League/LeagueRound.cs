using Golferoo.Sim.Save;
using Golferoo.Sim.Scoring;

namespace Golferoo.Sim.League;

/// <summary>Format d'une journée, en cycle de quatre. Tous se jouent en brut.</summary>
public enum LeagueFormat
{
    StrokePlay = 0,
    Stableford = 1,
    MatchPlay = 2,
    VersusPar = 3,
}

/// <summary>Une journée de ligue (ou la finale, numéro <see cref="LeagueSeason.RoundCount"/>) : format, paires, cartes rendues et points marqués.</summary>
public sealed class LeagueRound
{
    internal LeagueRound(int number, int nominalDay, LeagueFormat? format = null)
    {
        Number = number;
        NominalDay = nominalDay;
        Format = format ?? (LeagueFormat)(number % LeagueSeason.FormatCycle);
    }

    /// <summary>Rang de la journée dans la saison, à partir de 0.</summary>
    public int Number { get; }

    public LeagueFormat Format { get; }

    /// <summary>Jour prévu (rang depuis le 1er avril, à partir de 0).</summary>
    public int NominalDay { get; }

    /// <summary>Jour où la journée a commencé (−1 tant qu'elle n'a pas eu lieu) : plus tard que prévu si un orage l'a reportée.</summary>
    public int StartDay { get; internal set; } = -1;

    public long StartTick { get; internal set; }

    /// <summary>Tick où la dernière paire a fini (−1 tant que la journée n'est pas close).</summary>
    public long EndTick { get; internal set; } = -1;

    public bool Closed => EndTick >= 0;

    /// <summary>Groupes dans l'ordre de départ : une paire, ou un joueur seul (le pro, ou le reste d'une division impaire).</summary>
    public IReadOnlyList<int[]> Pairs { get; internal set; } = [];

    public Dictionary<int, ScoreCard> Cards { get; } = [];

    /// <summary>Points de saison marqués ce jour-là, par identifiant de membre.</summary>
    public Dictionary<int, double> Points { get; } = [];

    internal int Pending { get; set; }

    internal void Write(SaveWriter w)
    {
        w.Int(StartDay);
        w.Long(StartTick);
        w.Long(EndTick);
        w.Int(Pending);
        w.List(Pairs, p => w.List(p, w.Int));
        w.List(Cards, c =>
        {
            w.Int(c.Key);
            c.Value.Write(w);
        });
        w.List(Points, p =>
        {
            w.Int(p.Key);
            w.Double(p.Value);
        });
    }

    /// <summary>Relit l'état changeant ; numéro, format et jour prévu viennent de la construction.</summary>
    internal void ReadState(SaveReader r)
    {
        StartDay = r.Int();
        StartTick = r.Long();
        EndTick = r.Long();
        Pending = r.Int();
        Pairs = [.. r.List(() => r.List(r.Int).ToArray())];
        Cards.Clear();
        foreach (var (id, card) in r.List(() => (r.Int(), ScoreCard.Read(r))))
        {
            Cards[id] = card;
        }

        Points.Clear();
        foreach (var (id, points) in r.List(() => (r.Int(), r.Double())))
        {
            Points[id] = points;
        }
    }
}

/// <summary>Champion du club d'une saison : vainqueur de la finale de la division 1, avec son brut.</summary>
public readonly record struct LeagueChampion(int Year, int MemberId, int Gross);

public enum LeagueEventType
{
    Champion = 0,
    Promoted = 1,
    Relegated = 2,
}

/// <summary>Événement de fin de saison ; <paramref name="Division"/> est la division (à partir de 0) du champion, ou de destination du membre promu ou relégué.</summary>
public readonly record struct LeagueEvent(int Year, LeagueEventType Type, int MemberId, int Division);
