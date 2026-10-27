using Golferoo.Sim.Shots;

namespace Golferoo.Sim.Golfers;

/// <summary>Style de jeu d'un golfeur. Les valeurs entrent dans la somme de contrôle : ne pas réordonner.</summary>
public enum Archetype : byte
{
    /// <summary>Profil de référence, attribué à personne : sert à la note du parcours (<see cref="Rating.CourseRater"/>).</summary>
    Neutral = 0,

    /// <summary>Pénalités, lies difficiles et flancs (aussi trop long et trop court) pèsent plus ; vise le centre du green.</summary>
    Cautious = 1,

    /// <summary>Driver et bois 3 10 % plus loin, fers 9 et wedge 5 % moins loin ; préfère le driver au départ.</summary>
    LongHitter = 2,

    /// <summary>Essaie le draw à chaque coup plein : avec, 5 % plus loin et 20 % moins dispersé ; droit, 5 % plus dispersé.</summary>
    Drawer = 3,

    /// <summary>Comme le joueur de draw, avec le fade.</summary>
    Fader = 4,

    /// <summary>Joue mieux dans un bel endroit (<see cref="Terrain.BeautyMap"/>) et y vise volontiers.</summary>
    Artist = 5,

    /// <summary>Pénalités pèsent moins et aucun flanc n'est testé : vise le drapeau, coupe le coin.</summary>
    Charger = 6,
}

/// <summary>
/// Réglages d'un archétype. Le niveau (<see cref="Golfer.Skill"/>) reste le handicap ; le profil change la manière.
/// </summary>
/// <param name="Risk">Poids des pénalités et des lies difficiles dans le coût d'un point de chute (1 : neutre).</param>
/// <param name="Flank">Écart des coups décalés d'un côté et de l'autre testés au choix final, en écarts types du club (1,5 : neutre) :
/// plus il est large, plus un club long et dispersé est jugé risqué.</param>
/// <param name="Shape">Effet essayé à chaque coup plein en plus du coup droit (<see cref="ShotShape.Straight"/> : aucun).</param>
/// <param name="Long">Longueur du long frappeur (<see cref="DistanceFactor"/>).</param>
/// <param name="Artist">Sensible à la beauté du lieu.</param>
/// <param name="Safe">Vise le centre du green plutôt que le drapeau, et teste aussi le coup trop long et trop court au choix final.</param>
public sealed record GolferProfile(Archetype Archetype, double Risk, double Flank, ShotShape Shape = ShotShape.Straight, bool Long = false, bool Artist = false, bool Safe = false)
{
    /// <summary>Distance gagnée avec son effet par un joueur de draw ou de fade.</summary>
    public const double ShapedDistance = 1.03;

    /// <summary>Dispersion d'un joueur de draw ou de fade avec son effet, et en coup droit.</summary>
    public const double ShapedDispersion = 0.92;

    public const double StraightDispersion = 1.05;

    /// <summary>Coût retiré au driver du long frappeur au départ, en mètres : il le préfère.</summary>
    public const double TeeDriverBonus = 10.0;

    /// <summary>Dispersion retirée au plus par la beauté du point de départ (artiste) : × (1 − 0,25 × beauté).</summary>
    public const double BeautyDispersion = 0.25;

    /// <summary>Coût d'un point de chute retiré par sa beauté (artiste), en mètres : 5 m × beauté.</summary>
    public const double BeautyCost = 5.0;

    private static readonly GolferProfile[] All =
    [
        new(Archetype.Neutral, 1.0, 1.5),
        new(Archetype.Cautious, 1.6, 3.0, Safe: true),
        new(Archetype.LongHitter, 1.0, 1.5, Long: true),
        new(Archetype.Drawer, 1.0, 1.5, ShotShape.Draw),
        new(Archetype.Fader, 1.0, 1.5, ShotShape.Fade),
        new(Archetype.Artist, 1.0, 1.5, Artist: true),
        new(Archetype.Charger, 0.6, 0.0),
    ];

    /// <summary>Nombre d'archétypes attribuables (hors neutre).</summary>
    public const int Count = 6;

    public static GolferProfile Of(Archetype archetype) => All[(int)archetype];

    /// <summary>Profil d'un membre : hachage de la graine et de son id, hors du flux de <see cref="League.LeagueSeason.GenerateMembers"/>.</summary>
    public static Archetype ForMember(ulong seed, int memberId) =>
        Pick(new DeterministicRng(seed ^ 0x50524F46494C4553UL ^ ((ulong)(uint)memberId * 0x9E3779B97F4A7C15UL)));

    /// <summary>Un archétype tiré uniformément (golfeur ordinaire, au RNG du monde à son arrivée).</summary>
    public static Archetype Pick(DeterministicRng rng) => (Archetype)(1 + (int)(rng.NextUInt64() % Count));

    /// <summary>Facteur de vitesse de balle d'un coup <paramref name="shape"/> au club <paramref name="club"/> (portée ∝ vitesse²).</summary>
    public double Speed(Club club, ShotShape shape) => Math.Sqrt(DistanceFactor(club) * (IsShaped(shape) ? ShapedDistance : 1.0));

    /// <summary>Facteur de dispersion d'un coup <paramref name="shape"/> joué depuis un lieu de beauté <paramref name="beauty"/>.</summary>
    public double Dispersion(ShotShape shape, double beauty) =>
        (Shape == ShotShape.Straight ? 1.0 : shape == Shape ? ShapedDispersion : StraightDispersion) * BeautyFactor(beauty);

    /// <summary>Facteur de dispersion dû à la beauté du point de départ (artiste seulement).</summary>
    public double BeautyFactor(double beauty) => Artist ? 1.0 - (BeautyDispersion * beauty) : 1.0;

    private static bool IsShaped(ShotShape shape) => shape is ShotShape.Draw or ShotShape.Fade;

    /// <summary>Longueur d'un club pour ce profil (part de la portée normale), coup droit.</summary>
    public double DistanceFactor(Club club) =>
        !Long ? 1.0
        : club == Clubs.Driver || club == Clubs.Wood3 ? 1.10
        : club == Clubs.Iron9 || club == Clubs.Wedge ? 0.95
        : 1.0;
}
