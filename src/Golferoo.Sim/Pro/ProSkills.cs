using Golferoo.Sim.Golfers;
using Golferoo.Sim.Save;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Pro;

/// <summary>Compétences de votre pro. Les valeurs entrent dans la somme de contrôle : ne pas réordonner.</summary>
public enum ProSkill : byte
{
    // Générales : chaque coup de leur famille de clubs.
    LongGame = 0,
    Irons = 1,
    ShortGame = 2,
    Putting = 3,

    // Spécialisées : seulement un coup réussi.
    Distance = 4,
    Precision = 5,
    Draw = 6,
    Fade = 7,
    Chip = 8,
}

/// <summary>Coup choisi par le joueur pour votre pro : club, point visé, trajectoire et force (0 à 1).</summary>
public readonly record struct ProShot(Club Club, Point2 Target, ShotShape Shape, double Force);

/// <summary>
/// Expérience de votre pro, compétence par compétence (C08). Un coup apporte un point à sa compétence générale, deux en ligue ;
/// un coup réussi en apporte autant à ses compétences spécialisées. À l'entraînement, chaque compétence gagne au plus
/// <see cref="TrainingCapPerYear"/> points par année de jeu ; la ligue n'a pas de plafond.
/// </summary>
public sealed class ProSkills
{
    public const int Count = 9;

    /// <summary>Points d'entraînement par compétence et par année au plus.</summary>
    public const int TrainingCapPerYear = 60;

    /// <summary>Échelle des rendements décroissants, en points : h = 2 + 34 × exp(−xp / Tau).</summary>
    public const double Tau = 110.0;

    public const double StartHandicap = 36.0;
    public const double BestHandicap = 2.0;

    /// <summary>Vitesse de balle gagnée au plus par la compétence distance.</summary>
    public const double MaxDistanceBonus = 0.08;

    /// <summary>Part de la dispersion retirée au plus par la compétence précision.</summary>
    public const double MaxPrecisionGain = 0.3;

    /// <summary>Écart type de l'effet d'une trajectoire, par point de niveau (<see cref="SkillLevels"/>).</summary>
    public const double ShapeSigmaPerLevel = 0.15;

    /// <summary>Distance à la cible d'un coup réussi, en part de la distance visée.</summary>
    public const double PrecisionTolerance = 0.08;

    /// <summary>Part de la portée du club qu'atteint un coup réussi en distance.</summary>
    public const double DistanceShare = 0.95;

    private readonly int[] _xp = new int[Count];
    private readonly int[] _trainingThisYear = new int[Count];
    private int _trainingYear;

    public int Experience(ProSkill skill) => _xp[(int)skill];

    /// <summary>Points d'entraînement déjà gagnés cette année.</summary>
    public int TrainingThisYear(ProSkill skill, int year) => year == _trainingYear ? _trainingThisYear[(int)skill] : 0;

    /// <summary>Handicap équivalent d'une compétence : 36 au départ, vers 2 avec l'expérience.</summary>
    public double Handicap(ProSkill skill) =>
        BestHandicap + ((StartHandicap - BestHandicap) * Math.Exp(-_xp[(int)skill] / Tau));

    /// <summary>Progrès d'une compétence, de 0 (départ) à 1 (handicap 2).</summary>
    public double Progress(ProSkill skill) => (StartHandicap - Handicap(skill)) / (StartHandicap - BestHandicap);

    /// <summary>Moyenne des compétences générales, pour l'affichage.</summary>
    public double OverallHandicap =>
        (Handicap(ProSkill.LongGame) + Handicap(ProSkill.Irons) + Handicap(ProSkill.ShortGame) + Handicap(ProSkill.Putting)) / 4.0;

    public static ProSkill General(Club club) =>
        club.IsPutter ? ProSkill.Putting
        : club == Clubs.Wedge ? ProSkill.ShortGame
        : club == Clubs.Driver || club == Clubs.Wood3 ? ProSkill.LongGame
        : ProSkill.Irons;

    /// <summary>Multiplicateur des écarts types d'un coup : niveau de la compétence générale, resserré par la précision.</summary>
    public double Dispersion(Club club) =>
        SkillLevels.FromHandicap(Handicap(General(club))) * (1.0 - (MaxPrecisionGain * Progress(ProSkill.Precision)));

    /// <summary>Multiplicateur de force donné par la compétence distance.</summary>
    public double ForceBonus => 1.0 + (MaxDistanceBonus * Progress(ProSkill.Distance));

    /// <summary>Écart type de l'effet voulu (<see cref="ShotShapes.Launch"/>, amount) ; nul pour un coup droit.</summary>
    public double ShapeSigma(ShotShape shape) => shape switch
    {
        ShotShape.Draw => ShapeSigmaPerLevel * SkillLevels.FromHandicap(Handicap(ProSkill.Draw)),
        ShotShape.Fade => ShapeSigmaPerLevel * SkillLevels.FromHandicap(Handicap(ProSkill.Fade)),
        ShotShape.Chip => ShapeSigmaPerLevel * SkillLevels.FromHandicap(Handicap(ProSkill.Chip)),
        _ => 0.0,
    };

    /// <summary>
    /// Réussite d'un coup hors green : la balle s'arrête sans pénalité sur la surface du point visé, fairway ou green, à moins de 8 % de la distance
    /// visée de ce point.
    /// </summary>
    public static bool Succeeded(TerrainGrid terrain, Point2 start, in ProShot shot, in ShotResult result)
    {
        if (shot.Club.IsPutter || result.PenaltyStrokes > 0)
        {
            return false;
        }

        if (result.Holed)
        {
            return true;
        }

        var aimed = Dist(start, shot.Target);
        var lie = terrain.SurfaceAt(shot.Target.X, shot.Target.Z);
        return lie is Surface.Fairway or Surface.Green
            && terrain.SurfaceAt(result.Ball.X, result.Ball.Z) == lie
            && Dist(result.Ball, shot.Target) <= PrecisionTolerance * aimed;
    }

    /// <summary>Expérience d'un coup joué : la compétence générale toujours, les spécialisées s'il est réussi.</summary>
    public void Record(TerrainGrid terrain, Point2 start, in ProShot shot, in ShotResult result, bool league, int year)
    {
        Gain(General(shot.Club), league, year);
        if (!Succeeded(terrain, start, shot, result))
        {
            return;
        }

        Gain(ProSkill.Precision, league, year);
        switch (shot.Shape)
        {
            case ShotShape.Draw:
                Gain(ProSkill.Draw, league, year);
                break;
            case ShotShape.Fade:
                Gain(ProSkill.Fade, league, year);
                break;
            case ShotShape.Chip:
                Gain(ProSkill.Chip, league, year);
                break;
            default:
                break;
        }

        if ((shot.Club == Clubs.Driver || shot.Club == Clubs.Wood3 || shot.Club == Clubs.Iron5)
            && Dist(start, result.Ball) >= DistanceShare * shot.Club.TargetRange)
        {
            Gain(ProSkill.Distance, league, year);
        }
    }

    /// <summary>Deux points en ligue sans plafond ; un à l'entraînement, dans la limite de l'année (remise à zéro au 1er avril).</summary>
    public void Gain(ProSkill skill, bool league, int year)
    {
        if (league)
        {
            _xp[(int)skill] += 2;
            return;
        }

        if (year != _trainingYear)
        {
            _trainingYear = year;
            Array.Clear(_trainingThisYear);
        }

        if (_trainingThisYear[(int)skill] < TrainingCapPerYear)
        {
            _trainingThisYear[(int)skill]++;
            _xp[(int)skill]++;
        }
    }

    internal void Write(SaveWriter w)
    {
        w.Int(_trainingYear);
        foreach (var v in _xp)
        {
            w.Int(v);
        }

        foreach (var v in _trainingThisYear)
        {
            w.Int(v);
        }
    }

    internal static ProSkills Read(SaveReader r)
    {
        var skills = new ProSkills { _trainingYear = r.Int() };
        for (var i = 0; i < Count; i++)
        {
            skills._xp[i] = r.Int();
        }

        for (var i = 0; i < Count; i++)
        {
            skills._trainingThisYear[i] = r.Int();
        }

        return skills;
    }

    internal ulong Hash()
    {
        var h = (ulong)_trainingYear;
        for (var i = 0; i < Count; i++)
        {
            h = (h * 1099511628211UL) ^ (ulong)_xp[i];
            h = (h * 1099511628211UL) ^ (ulong)_trainingThisYear[i];
        }

        return h;
    }

    private static double Dist(Point2 a, Point2 b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));
}
