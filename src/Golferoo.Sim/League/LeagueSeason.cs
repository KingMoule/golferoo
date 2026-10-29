using Golferoo.Sim.Golfers;
using Golferoo.Sim.Rating;
using Golferoo.Sim.Save;
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

    /// <summary>Jours d'une année de jeu : la saison suivante repart au 1er avril.</summary>
    public const int DaysPerYear = Calendar.DaysPerMonth * Calendar.MonthsPerYear;

    /// <summary>Les finalistes de chaque division.</summary>
    public const int FinalistsPerDivision = 4;

    /// <summary>Montées et descentes à chaque frontière de division.</summary>
    public const int MovesPerBoundary = 2;

    private readonly ulong _seed;
    private readonly Dictionary<int, Member> _byId;
    private int _next;
    private int _due;
    private bool _adjusted;
    private List<List<Member>> _finalists = [];

    public LeagueSeason(ulong seed, IReadOnlyList<Member> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        _seed = seed;
        Members = members;
        _byId = members.ToDictionary(m => m.Id);
        Divisions = BuildDivisions(Enrolled(members).ToList());
        (Rounds, Final) = NewRounds(0);
        _due = Rounds[0].NominalDay;
    }

    public IReadOnlyList<Member> Members { get; }

    /// <summary>Divisions de la première (la plus forte) à la dernière.</summary>
    public IReadOnlyList<IReadOnlyList<Member>> Divisions { get; internal set; }

    /// <summary>Année de la saison en cours, à partir de 0.</summary>
    public int Year { get; private set; }

    /// <summary>Les 5 journées de la saison en cours.</summary>
    public List<LeagueRound> Rounds { get; private set; }

    /// <summary>Finale de septembre (stroke play en brut), numérotée <see cref="RoundCount"/>.</summary>
    public LeagueRound Final { get; private set; }

    /// <summary>Classement de la finale, par division (identifiants de membre, le meilleur d'abord) ; vide avant la fin de la finale.</summary>
    public IReadOnlyList<IReadOnlyList<int>> FinalRanking { get; private set; } = [];

    /// <summary>Champion de la saison en cours (vainqueur de la finale de la division 1), nul avant.</summary>
    public LeagueChampion? Champion { get; private set; }

    /// <summary>Champions de toutes les saisons terminées ou en cours.</summary>
    public List<LeagueChampion> Champions { get; } = [];

    /// <summary>Journal des fins de saison (champion, montées, descentes), dans l'ordre.</summary>
    public List<LeagueEvent> Events { get; } = [];

    /// <summary>Les cinq journées sont closes (la finale n'a pas forcément eu lieu).</summary>
    public bool Finished => _next >= RoundCount && Rounds.All(r => r.Closed);

    /// <summary>La finale et l'ajustement ont eu lieu : la saison attend le 1er avril.</summary>
    public bool Adjusted => _adjusted;

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
            members.Add(new Member(i, name, handicap, (int)(rng.NextUInt64() % 101), profile: GolferProfile.ForMember(seed, i)));
        }

        members.Add(new Member(ClubMembers, "Votre pro", 36, 100, isPro: true, GolferProfile.ForMember(seed, ClubMembers)));
        return members;
    }

    private (List<LeagueRound> Rounds, LeagueRound Final) NewRounds(int year)
    {
        var start = year * DaysPerYear;
        var rounds = new List<LeagueRound>(RoundCount);
        for (var i = 0; i < RoundCount; i++)
        {
            rounds.Add(new LeagueRound(i, start + NominalDay(i)));
        }

        return (rounds, new LeagueRound(RoundCount, start + NominalDay(RoundCount), LeagueFormat.StrokePlay));
    }

    private LeagueRound RoundAt(int n) => n < RoundCount ? Rounds[n] : Final;

    private static IEnumerable<Member> Enrolled(IEnumerable<Member> members) =>
        members.Where(m => !m.IsPro && m.Opinion >= MinOpinion).OrderBy(m => m.StartHandicap).ThenBy(m => m.Id)
            .Concat(members.Where(m => m.IsPro));

    /// <summary>Divisions de <c>floor(inscrits / 8)</c> (au moins une) sur la liste déjà rangée.</summary>
    private static List<IReadOnlyList<Member>> BuildDivisions(List<Member> ordered)
    {
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
        if (_adjusted && day >= (Year + 1) * DaysPerYear)
        {
            NewSeason();
        }
        else if (!_adjusted && _next > RoundCount && Final.Closed && day > Final.StartDay)
        {
            Adjust();
        }

        // Garde-fou (C21) : une journée ne commence jamais avant la fin de la précédente, même si elle déborde ; la suivante attend.
        if (_next > RoundCount || day < _due || (_next == RoundCount && !Rounds.All(r => r.Closed)) || (_next > 0 && _next <= RoundCount && !RoundAt(_next - 1).Closed))
        {
            return null;
        }

        if (storm)
        {
            _due = day + 1;
            return null;
        }

        var round = RoundAt(_next++);
        round.StartDay = day;
        round.StartTick = tick;
        round.Pairs = round.Number == RoundCount ? DrawFinal() : Draw(round);
        round.Pending = round.Pairs.Count;
        _due = _next <= RoundCount ? Math.Max(RoundAt(_next).NominalDay, day + 1) : 0;
        return round;
    }

    /// <summary>Les quatre premiers de chaque division aux points de saison ; égalité : meilleur brut cumulé, puis identifiant.</summary>
    internal List<List<Member>> Finalists() => [.. Divisions.Select(d => d
        .OrderByDescending(m => m.SeasonPoints)
        .ThenBy(m => Rounds.Sum(r => r.Cards.TryGetValue(m.Id, out var c) ? c.Gross : 0))
        .ThenBy(m => m.Id)
        .Take(FinalistsPerDivision).ToList())];

    private List<int[]> DrawFinal()
    {
        var groups = new List<int[]>();
        var finalists = Finalists();
        for (var d = finalists.Count - 1; d >= 0; d--)
        {
            var ids = finalists[d].Where(m => !m.IsPro).Select(m => m.Id).ToArray();
            for (var i = 0; i < ids.Length; i += 2)
            {
                groups.Add(i + 1 < ids.Length ? [ids[i], ids[i + 1]] : [ids[i]]);
            }

            groups.AddRange(finalists[d].Where(m => m.IsPro).Select(m => new[] { m.Id }));
        }

        _finalists = finalists;
        return groups;
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
        var round = RoundAt(roundNumber);
        foreach (var (id, card) in cards)
        {
            // Une carte sans aucun trou joué (tous fermés) ne dit rien du niveau : pas d'écart, plutôt qu'une exception de Differential.
            var differential = rating is null || !PlayedAny(card) ? (double?)null : Handicap.Differential(card, rating);
            _byId[id].AddCard(new LeagueCard(round.Number, card.Gross, differential));
            round.Cards[id] = card;
        }

        if (--round.Pending == 0)
        {
            round.EndTick = tick;
            Close(round);
        }
    }

    private static bool PlayedAny(ScoreCard card)
    {
        for (var h = 0; h < card.HoleCount; h++)
        {
            if (card.Strokes(h) > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void Close(LeagueRound round)
    {
        if (round.Number == RoundCount)
        {
            CloseFinal();
            return;
        }

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

    private void CloseFinal()
    {
        // Brut ; à égalité, l'ordre de qualification (points de saison).
        FinalRanking = [.. _finalists.Select(f => (IReadOnlyList<int>)[.. f.OrderBy(m => Final.Cards[m.Id].Gross).Select(m => m.Id)])];
        var winner = FinalRanking[0][0];
        var champion = new LeagueChampion(Year, winner, Final.Cards[winner].Gross);
        Champion = champion;
        Champions.Add(champion);
        Events.Add(new LeagueEvent(Year, LeagueEventType.Champion, winner, 0));
    }

    /// <summary>
    /// Classement final de chaque division (points de saison, puis finale, puis identifiant) : les deux premiers de chaque
    /// division hors la première montent, les deux derniers de chaque division hors la dernière descendent.
    /// </summary>
    private void Adjust()
    {
        _adjusted = true;
        var last = Divisions.Count - 1;
        if (last == 0)
        {
            return;
        }

        var standings = Divisions.Select((d, i) => d
            .OrderByDescending(m => m.SeasonPoints)
            .ThenBy(m => FinalPlace(i, m))
            .ThenBy(m => m.Id).ToList()).ToList();
        var next = new List<IReadOnlyList<Member>>(Divisions.Count);
        for (var d = 0; d <= last; d++)
        {
            var arriving = d > 0 ? standings[d - 1].TakeLast(MovesPerBoundary).ToList() : [];
            var rising = d < last ? standings[d + 1].Take(MovesPerBoundary).ToList() : [];
            var staying = standings[d].Skip(d > 0 ? MovesPerBoundary : 0).SkipLast(d < last ? MovesPerBoundary : 0);
            next.Add([.. arriving, .. staying, .. rising]);
            Events.AddRange(rising.Select(m => new LeagueEvent(Year, LeagueEventType.Promoted, m.Id, d)));
            Events.AddRange(arriving.Select(m => new LeagueEvent(Year, LeagueEventType.Relegated, m.Id, d)));
        }

        Divisions = next;
    }

    /// <summary>Place de <paramref name="m"/> à la finale de sa division ; les non-finalistes après les finalistes.</summary>
    private int FinalPlace(int division, Member m)
    {
        for (var k = 0; k < FinalRanking[division].Count; k++)
        {
            if (FinalRanking[division][k] == m.Id)
            {
                return k;
            }
        }

        return int.MaxValue;
    }

    /// <summary>1er avril : mêmes divisions rangées par l'ajustement, nouveaux inscrits et pro en bas, points remis à zéro.</summary>
    private void NewSeason()
    {
        var kept = Divisions.SelectMany(d => d).Where(m => !m.IsPro && m.Opinion >= MinOpinion).ToList();
        var known = kept.ToHashSet();
        var ordered = kept.Concat(Enrolled(Members).Where(m => !m.IsPro && !known.Contains(m)))
            .Concat(Members.Where(m => m.IsPro)).ToList();
        Divisions = BuildDivisions(ordered);
        Year++;
        foreach (var m in Members)
        {
            m.SeasonPoints = 0;
        }

        (Rounds, Final) = NewRounds(Year);
        FinalRanking = [];
        Champion = null;
        _finalists = [];
        _next = 0;
        _adjusted = false;
        _due = Rounds[0].NominalDay;
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

    internal void Write(SaveWriter w)
    {
        w.Int(Year);
        w.Int(_next);
        w.Int(_due);
        w.Bool(_adjusted);
        foreach (var m in Members)
        {
            m.Write(w);
        }

        w.List(Divisions, d => w.List(d, m => w.Int(m.Id)));
        foreach (var round in Rounds.Append(Final))
        {
            round.Write(w);
        }

        w.List(FinalRanking, ids => w.List(ids, w.Int));
        w.List(_finalists, f => w.List(f, m => w.Int(m.Id)));
        w.Bool(Champion is not null);
        WriteChampion(w, Champion ?? default);
        w.List(Champions, c => WriteChampion(w, c));
        w.List(Events, e =>
        {
            w.Int(e.Year);
            w.Byte((byte)e.Type);
            w.Int(e.MemberId);
            w.Int(e.Division);
        });
    }

    private static void WriteChampion(SaveWriter w, LeagueChampion c)
    {
        w.Int(c.Year);
        w.Int(c.MemberId);
        w.Int(c.Gross);
    }

    private static LeagueChampion ReadChampion(SaveReader r) => new(r.Int(), r.Int(), r.Int());

    /// <summary>Saison relue : les membres sont ceux de la graine, l'état qui change (année, journées, divisions, classements) vient du fichier.</summary>
    internal static LeagueSeason Read(SaveReader r, ulong seed)
    {
        var season = new LeagueSeason(seed, GenerateMembers(seed));
        season.Year = r.Int();
        season._next = r.Int();
        season._due = r.Int();
        season._adjusted = r.Bool();
        foreach (var m in season.Members)
        {
            m.ReadState(r);
        }

        List<Member> Group(List<int> ids) => [.. ids.Select(id => season._byId[id])];
        season.Divisions = [.. r.List(() => (IReadOnlyList<Member>)Group(r.List(r.Int)))];
        (season.Rounds, season.Final) = season.NewRounds(season.Year);
        foreach (var round in season.Rounds.Append(season.Final))
        {
            round.ReadState(r);
        }

        season.FinalRanking = [.. r.List(() => (IReadOnlyList<int>)r.List(r.Int))];
        season._finalists = [.. r.List(() => Group(r.List(r.Int)))];
        var hasChampion = r.Bool();
        var champion = ReadChampion(r);
        season.Champion = hasChampion ? champion : null;
        season.Champions.AddRange(r.List(() => ReadChampion(r)));
        season.Events.AddRange(r.List(() => new LeagueEvent(r.Int(), (LeagueEventType)r.Byte(), r.Int(), r.Int())));
        return season;
    }

    internal void MixInto(ref ulong hash)
    {
        World.Mix(ref hash, ((ulong)(uint)_next << 32) | (uint)_due);
        World.Mix(ref hash, ((ulong)(uint)Year << 1) | (_adjusted ? 1UL : 0UL));
        foreach (var division in Divisions)
        {
            World.Mix(ref hash, (ulong)division.Count);
            foreach (var m in division)
            {
                World.Mix(ref hash, (ulong)(uint)m.Id);
            }
        }

        foreach (var c in Champions)
        {
            World.Mix(ref hash, ((ulong)(uint)c.Year << 40) | ((ulong)(uint)c.MemberId << 20) | (uint)c.Gross);
        }

        foreach (var e in Events)
        {
            World.Mix(ref hash, ((ulong)(uint)e.Year << 40) | ((ulong)(uint)e.Type << 32) | ((ulong)(uint)e.MemberId << 8) | (uint)e.Division);
        }

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

        foreach (var r in Rounds.Append(Final))
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
