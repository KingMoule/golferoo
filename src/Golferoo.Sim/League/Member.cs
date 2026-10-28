using Golferoo.Sim.Golfers;
using Golferoo.Sim.Rating;
using Golferoo.Sim.Save;

namespace Golferoo.Sim.League;

/// <summary>Carte de ligue d'un membre : brut, et écart avec la note du parcours du jour (nul s'il n'y en avait pas).</summary>
public readonly record struct LeagueCard(int Round, int Gross, double? Differential);

/// <summary>Membre du club (ou votre pro). Opinion du club de 0 à 100, fixe en C06.</summary>
public sealed class Member
{
    private readonly List<LeagueCard> _cards = [];

    public Member(int id, string name, double startHandicap, int opinion, bool isPro = false, Archetype profile = Archetype.Neutral)
    {
        Id = id;
        Profile = profile;
        Name = name;
        StartHandicap = startHandicap;
        Opinion = opinion;
        IsPro = isPro;
    }

    public int Id { get; }

    public string Name { get; }

    public double StartHandicap { get; }

    /// <summary>Niveau de jeu du membre (<see cref="Golfer.Skill"/>).</summary>
    public double Skill => SkillLevels.FromHandicap(StartHandicap);

    /// <summary>Style de jeu du membre, stable d'une partie à l'autre (<see cref="GolferProfile.ForMember"/>).</summary>
    public Archetype Profile { get; }

    public int Opinion { get; internal set; }

    public bool IsPro { get; }

    public IReadOnlyList<LeagueCard> Cards => _cards;

    public double SeasonPoints { get; internal set; }

    internal void Write(SaveWriter w)
    {
        w.Int(Opinion);
        w.Double(SeasonPoints);
        w.List(_cards, c =>
        {
            w.Int(c.Round);
            w.Int(c.Gross);
            w.Bool(c.Differential is not null);
            w.Double(c.Differential ?? 0.0);
        });
    }

    /// <summary>Relit l'état changeant ; nom, handicap et style se retrouvent par <see cref="LeagueSeason.GenerateMembers"/>.</summary>
    internal void ReadState(SaveReader r)
    {
        Opinion = r.Int();
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
