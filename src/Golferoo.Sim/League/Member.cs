using Golferoo.Sim.Golfers;
using Golferoo.Sim.Rating;
using Golferoo.Sim.Save;

namespace Golferoo.Sim.League;

/// <summary>Carte de ligue d'un membre : brut, et écart avec la note du parcours du jour (nul s'il n'y en avait pas).</summary>
public readonly record struct LeagueCard(int Round, int Gross, double? Differential);

/// <summary>Membre de la ligue (ou votre pro). Un membre est un visiteur du vivier devenu membre (C16) : <see cref="VisitorId"/> le désigne.</summary>
public sealed class Member
{
    private readonly List<LeagueCard> _cards = [];

    public Member(int id, string name, double startHandicap, bool isPro = false, Archetype profile = Archetype.Neutral, int visitorId = -1)
    {
        Id = id;
        Profile = profile;
        Name = name;
        StartHandicap = startHandicap;
        IsPro = isPro;
        VisitorId = visitorId;
    }

    public int Id { get; }

    public string Name { get; }

    public double StartHandicap { get; }

    /// <summary>Niveau de jeu du membre (<see cref="Golfer.Skill"/>).</summary>
    public double Skill => SkillLevels.FromHandicap(StartHandicap);

    /// <summary>Style de jeu du membre, stable d'une partie à l'autre (celui de son visiteur, <see cref="GolferProfile.ForMember"/> pour le pro).</summary>
    public Archetype Profile { get; }

    public bool IsPro { get; }

    /// <summary>Numéro du visiteur du vivier que ce membre incarne (−1 : votre pro).</summary>
    public int VisitorId { get; internal set; }

    /// <summary>A démissionné : la ligue ne le compte plus et ne le tire plus au sort (sa carte et ses points restent).</summary>
    public bool Resigned { get; internal set; }

    public IReadOnlyList<LeagueCard> Cards => _cards;

    public double SeasonPoints { get; internal set; }

    /// <summary>Ce qui ne change pas d'une saison à l'autre, plus le lien au visiteur et la démission (v15).</summary>
    internal void WriteRoster(SaveWriter w)
    {
        w.Int(Id);
        w.String(Name);
        w.Double(StartHandicap);
        w.Byte((byte)Profile);
        w.Bool(IsPro);
        w.Int(VisitorId);
        w.Bool(Resigned);
    }

    internal static Member ReadRoster(SaveReader r)
    {
        var (id, name, handicap, profile, isPro, visitorId, resigned) = (r.Int(), r.String(), r.Double(), (Archetype)r.Byte(), r.Bool(), r.Int(), r.Bool());
        return new Member(id, name, handicap, isPro, profile, visitorId) { Resigned = resigned };
    }

    internal void Write(SaveWriter w)
    {
        w.Double(SeasonPoints);
        w.List(_cards, c =>
        {
            w.Int(c.Round);
            w.Int(c.Gross);
            w.Bool(c.Differential is not null);
            w.Double(c.Differential ?? 0.0);
        });
    }

    /// <summary>Relit l'état changeant. <paramref name="legacy"/> : avant la v15, il commence par l'opinion fixe de C06, qui n'existe plus.</summary>
    internal void ReadState(SaveReader r, bool legacy)
    {
        if (legacy)
        {
            _ = r.Int();
        }

        SeasonPoints = r.Double();
        _cards.Clear();
        _cards.AddRange(r.List(() =>
        {
            var (round, gross, has, differential) = (r.Int(), r.Int(), r.Bool(), r.Double());
            return new LeagueCard(round, gross, has ? differential : null);
        }));
    }

    internal void AddCard(LeagueCard card) => _cards.Add(card);

    /// <summary>Index d'après les cartes de ligue ; nul avant 3 écarts connus.</summary>
    public double? Index(int courseHoles) =>
        Handicap.Index([.. _cards.Where(c => c.Differential is not null).Select(c => c.Differential!.Value)], courseHoles);
}
