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

/// <summary>Une journée de ligue : format, paires, cartes rendues et points marqués.</summary>
public sealed class LeagueRound
{
    internal LeagueRound(int number, int nominalDay)
    {
        Number = number;
        NominalDay = nominalDay;
        Format = (LeagueFormat)(number % LeagueSeason.FormatCycle);
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
}
