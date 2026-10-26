using Golferoo.Sim.Rating;
using Golferoo.Sim.Scoring;
using Golferoo.Sim.Time;

namespace Golferoo.Sim.League;

/// <summary>
/// Saison de ligue : inscrits, divisions, calendrier, formats et points. Sans Godot et sans tick :
/// le monde l'appelle au changement de jour et quand une paire a fini.
/// </summary>
public sealed class LeagueSeason
{
    /// <summary>Une journée par mois, d'avril à août.</summary>
    public const int RoundCount = 5;

    /// <summary>Jour du mois (à partir de 1) où tombe la journée de ligue.</summary>
    public const int RoundDayOfMonth = 5;

    /// <summary>Les formats tournent en cycle de quatre.</summary>
    public const int FormatCycle = 4;
    public const int MinOpinion = 60;
    public const int DivisionSize = 8;
    public const int ClubMembers = 40;

    private static readonly string[] FirstNames =
    [
        "Anne", "Bruno", "Claire", "David", "Elsa", "Franck", "Gaëlle", "Hugo", "Inès", "Jules", "Karine", "Louis",
        "Manon", "Nicolas", "Océane", "Paul", "Quentin", "Romane", "Sam", "Thomas", "Ulysse", "Vera", "Yann", "Zoé",
    ];

    private readonly ulong _seed;
    private readonly Dictionary<int, Member> _byId;
    private int _next;
    private int _due;

    public LeagueSeason(ulong seed, IReadOnlyList<Member> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        _seed = seed;
        Members = members;
        _byId = members.ToDictionary(m => m.Id);
        Divisions = BuildDivisions(members);
        for (var i = 0; i < RoundCount; i++)
        {
            Rounds.Add(new LeagueRound(i, NominalDay(i)));
        }

        _due = Rounds[0].NominalDay;
    }

    public IReadOnlyList<Member> Members { get; }

    /// <summary>Divisions de la première (la plus forte) à la dernière.</summary>
    public IReadOnlyList<IReadOnlyList<Member>> Divisions { get; }

    public List<LeagueRound> Rounds { get; } = [];

    public bool Finished => _next >= RoundCount && Rounds.All(r => r.Closed);

    /// <summary>Jour prévu de la journée <paramref name="round"/> : le 5 de chaque mois, à partir d'avril.</summary>
    public static int NominalDay(int round) => (round * Calendar.DaysPerMonth) + RoundDayOfMonth - 1;

    /// <summary>40 membres tirés de la graine (handicaps de 2 à 36), puis votre pro (handicap 36, opinion 100).</summary>
    public static List<Member> GenerateMembers(ulong seed)
    {
        var rng = new DeterministicRng(seed ^ 0x4D454D4245525355UL);
        var members = new List<Member>(ClubMembers + 1);
        for (var i = 0; i < ClubMembers; i++)
        {
            var name = $"{FirstNames[(int)(rng.NextUInt64() % (ulong)FirstNames.Length)]} {(char)('A' + (int)(rng.NextUInt64() % 26))}.";
            var handicap = 2 + (int)(rng.NextUInt64() % 35);
            members.Add(new Member(i, name, handicap, (int)(rng.NextUInt64() % 101)));
        }

        members.Add(new Member(ClubMembers, "Votre pro", 36, 100, isPro: true));
        return members;
    }

    private static List<IReadOnlyList<Member>> BuildDivisions(IReadOnlyList<Member> members)
    {
        var ordered = members.Where(m => !m.IsPro && m.Opinion >= MinOpinion)
            .OrderBy(m => m.StartHandicap).ThenBy(m => m.Id)
            .Concat(members.Where(m => m.IsPro)).ToList();
        var count = Math.Max(1, ordered.Count / DivisionSize);
        var divisions = new List<IReadOnlyList<Member>>(count);
        var start = 0;
        for (var d = 0; d < count; d++)
        {
            var size = (ordered.Count / count) + (d < ordered.Count % count ? 1 : 0);
            divisions.Add(ordered.GetRange(start, size));
            start += size;
        }

        return divisions;
    }

    /// <summary>
    /// Au changement du jour <paramref name="day"/> : démarre la journée due, sauf sous l'orage (elle glisse au lendemain).
    /// Rend la journée et ses groupes dans l'ordre de départ (dernière division d'abord), ou nul.
    /// </summary>
    public LeagueRound? OnNewDay(int day, bool storm, long tick)
    {
        if (_next >= RoundCount || day < _due)
        {
            return null;
        }

        if (storm)
        {
            _due = day + 1;
            return null;
        }

        var round = Rounds[_next++];
        round.StartDay = day;
        round.StartTick = tick;
        round.Pairs = Draw(round);
        round.Pending = round.Pairs.Count;
        _due = _next < RoundCount ? Math.Max(Rounds[_next].NominalDay, day + 1) : 0;
        return round;
    }

    private List<int[]> Draw(LeagueRound round)
    {
        var rng = new DeterministicRng(_seed ^ (0x51ED270B6C1E2A47UL * (ulong)(round.Number + 1)));
        var groups = new List<int[]>();
        for (var d = Divisions.Count - 1; d >= 0; d--)
        {
            var ids = Divisions[d].Where(m => !m.IsPro).Select(m => m.Id).ToArray();
            for (var i = ids.Length - 1; i > 0; i--)
            {
                var j = (int)(rng.NextUInt64() % (ulong)(i + 1));
                (ids[i], ids[j]) = (ids[j], ids[i]);
            }

            for (var i = 0; i + 1 < ids.Length; i += 2)
            {
                groups.Add([ids[i], ids[i + 1]]);
            }

            if (ids.Length % 2 == 1)
            {
                groups.Add([ids[^1]]);
            }

            groups.AddRange(Divisions[d].Where(m => m.IsPro).Select(m => new[] { m.Id }));
        }

        return groups;
    }

    /// <summary>Une paire a fini : rend la carte de chaque joueur (brut, écart avec la note si elle existe) ; la journée se clôt avec la dernière paire.</summary>
    public void GroupFinished(int roundNumber, IReadOnlyList<(int MemberId, ScoreCard Card)> cards, CourseRating? rating, long tick)
    {
        ArgumentNullException.ThrowIfNull(cards);
        var round = Rounds[roundNumber];
        foreach (var (id, card) in cards)
        {
            var differential = rating is null ? (double?)null : Handicap.Differential(card.Gross, rating, card.HoleCount);
            _byId[id].AddCard(new LeagueCard(round.Number, card.Gross, differential));
            round.Cards[id] = card;
        }

        if (--round.Pending == 0)
        {
            round.EndTick = tick;
            Close(round);
        }
    }

    private void Close(LeagueRound round)
    {
        var partner = new Dictionary<int, int>();
        foreach (var pair in round.Pairs.Where(p => p.Length == 2))
        {
            partner[pair[0]] = pair[1];
            partner[pair[1]] = pair[0];
        }

        foreach (var division in Divisions)
        {
            // Clé : plus petite = mieux.
            var keys = division.ToDictionary(m => m.Id, m => Key(round, m.Id, partner));
            foreach (var m in division)
            {
                var k = keys[m.Id];
                var behind = division.Count(o => keys[o.Id] > k);
                var tied = division.Count(o => o.Id != m.Id && keys[o.Id] == k);
                var points = behind + (0.5 * tied);
                round.Points[m.Id] = points;
                m.SeasonPoints += points;
            }
        }
    }

    private static int Key(LeagueRound round, int id, Dictionary<int, int> partner)
    {
        var card = round.Cards[id];
        switch (round.Format)
        {
            case LeagueFormat.StrokePlay:
                return card.Gross;
            case LeagueFormat.Stableford:
                return -card.Stableford();
            case LeagueFormat.VersusPar:
                return -card.VersusPar();
            default:
                // Vainqueurs devant, puis partagés (le joueur seul y compte), puis perdants ; départagés au brut.
                var category = 1;
                if (partner.TryGetValue(id, out var other))
                {
                    var r = MatchResult.Play(card, round.Cards[other]);
                    category = r.Won > r.Lost ? 0 : r.Won < r.Lost ? 2 : 1;
                }

                return (category * 1000) + card.Gross;
        }
    }

    internal void MixInto(ref ulong hash)
    {
        World.Mix(ref hash, ((ulong)(uint)_next << 32) | (uint)_due);
        foreach (var m in Members)
        {
            World.Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(m.SeasonPoints));
            World.Mix(ref hash, (ulong)m.Cards.Count);
            foreach (var c in m.Cards)
            {
                World.Mix(ref hash, ((ulong)(uint)c.Round << 32) | (uint)c.Gross);
                World.Mix(ref hash, c.Differential is { } d ? (ulong)BitConverter.DoubleToInt64Bits(d) : 0UL);
            }
        }

        foreach (var r in Rounds)
        {
            World.Mix(ref hash, ((ulong)(uint)r.StartDay << 32) | (uint)r.Pending);
            World.Mix(ref hash, (ulong)r.StartTick);
            World.Mix(ref hash, (ulong)r.EndTick);
            foreach (var pair in r.Pairs)
            {
                World.Mix(ref hash, pair.Length == 2 ? ((ulong)(uint)pair[0] << 32) | (uint)pair[1] : (uint)pair[0]);
            }
        }
    }
}
