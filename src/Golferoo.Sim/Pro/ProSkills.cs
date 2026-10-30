using Golferoo.Sim.Golfers;
using Golferoo.Sim.Save;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Pro;

/// <summary>Habiletés de votre pro (C25). Les valeurs entrent dans la somme de contrôle : ne pas réordonner.</summary>
public enum ProSkill : byte
{
    // Les clubs.
    Driver = 0,
    Wood = 1,
    LongIrons = 2,
    MidIrons = 3,
    ShortIrons = 4,
    Wedge = 5,
    Putter = 6,

    // Les qualités du coup.
    Precision = 7,
    Dosage = 8,
    Distance = 9,

    // Les trajectoires.
    Draw = 10,
    Fade = 11,
    Chip = 12,

    // Les situations. Sortie de rough et de bunker : pénalité de lie (lot 2) ; Vent et Green : au lot 3.
    RoughExit = 13,
    BunkerExit = 14,
    Wind = 15,
    GreenReading = 16,
}

/// <summary>Coup choisi par le joueur pour votre pro : club, point visé, trajectoire et force (0 à 1).</summary>
public readonly record struct ProShot(Club Club, Point2 Target, ShotShape Shape, double Force);

/// <summary>Biais de l'aperçu d'un coup (C25) : le vent que le pro croit voir, et la rotation et l'échelle de la pente qu'il croit lire.</summary>
public readonly record struct ProBias(Vec3 Wind, double SlopeAngle, double SlopeScale);

/// <summary>
/// Expérience de votre pro, habileté par habileté (C25). Chaque coup nourrit les habiletés qu'il a sollicitées, selon sa qualité
/// (<see cref="ProQuality"/>). L'XP est gardée en centièmes entiers (somme de contrôle exacte). Un coup de ligue compte double, sans plafond ;
/// à l'entraînement, chaque habileté gagne au plus <see cref="ProgressionParams.TrainingCapPerYear"/> XP par année (remise à zéro au 1er avril).
/// Niveaux de 0 à 50 ; chaque niveau retire un pas fixe de handicap équivalent (0,8) : niveau 0 = golfeur 36, niveau 50 = golfeur +4.
/// </summary>
public sealed class ProSkills
{
    public const int Count = 17;

    /// <summary>Écart type de l'effet d'une trajectoire, par point de niveau (<see cref="SkillLevels"/>).</summary>
    public const double ShapeSigmaPerLevel = 0.15;

    public const int MaxLevel = ProgressionParams.MaxLevel;
    public const double StartHandicap = 36.0;

    /// <summary>Handicap équivalent retiré par niveau : 36 − 0,8 × 50 = −4.</summary>
    public const double HandicapPerLevel = 0.8;

    /// <summary>Perte de vitesse de balle par point de niveau au-delà de 1, comme les golfeurs ordinaires (<c>PhysicalShotResolver</c>).</summary>
    public const double PowerLossPerSkill = 0.05;

    private readonly ProgressionParams _params;
    private readonly long[] _xp = new long[Count];
    private readonly long[] _trainingThisYear = new long[Count];
    private int _trainingYear;

    /// <summary>XP cumulée en centièmes nécessaires pour atteindre chaque niveau : <c>_floor[n]</c> = XP du niveau n.</summary>
    private readonly long[] _floor;

    public ProSkills(ProgressionParams? progression = null)
    {
        _params = progression ?? ProgressionParams.Default;
        _floor = BuildFloors(_params);
    }

    public long Experience(ProSkill skill) => _xp[(int)skill];

    /// <summary>XP d'entraînement déjà gagnée cette année, en centièmes.</summary>
    public long TrainingThisYear(ProSkill skill, int year) => year == _trainingYear ? _trainingThisYear[(int)skill] : 0;

    /// <summary>XP d'entraînement au plus, par habileté et par année, en centièmes.</summary>
    public long TrainingCap => (long)Math.Round(_params.TrainingCapPerYear * 100.0);

    public int Level(ProSkill skill)
    {
        var level = 0;
        while (level < MaxLevel && _xp[(int)skill] >= _floor[level + 1])
        {
            level++;
        }

        return level;
    }

    /// <summary>Part du niveau en cours déjà gagnée, de 0 à 1 (1 au niveau maximal).</summary>
    public double LevelProgress(ProSkill skill)
    {
        var level = Level(skill);
        return level >= MaxLevel ? 1.0 : (double)(_xp[(int)skill] - _floor[level]) / (_floor[level + 1] - _floor[level]);
    }

    /// <summary>XP cumulée, en centièmes, qu'il faut pour atteindre <paramref name="level"/>.</summary>
    public long XpForLevel(int level) => _floor[level];

    /// <summary>Handicap équivalent d'une habileté : 36 au niveau 0, −4 au niveau 50.</summary>
    public double Handicap(ProSkill skill) => HandicapOf(Level(skill));

    public static double HandicapOf(double level) => StartHandicap - (HandicapPerLevel * level);

    /// <summary>Niveau global (handicap affiché) : la moyenne des sept habiletés de club.</summary>
    public double OverallHandicap
    {
        get
        {
            var sum = 0;
            for (var i = (int)ProSkill.Driver; i <= (int)ProSkill.Putter; i++)
            {
                sum += Level((ProSkill)i);
            }

            return HandicapOf(sum / 7.0);
        }
    }

    public static ProSkill OfClub(Club club) =>
        club.IsPutter ? ProSkill.Putter
        : club == Clubs.Driver ? ProSkill.Driver
        : club == Clubs.Wood3 ? ProSkill.Wood
        : club == Clubs.Iron5 ? ProSkill.LongIrons
        : club == Clubs.Iron9 ? ProSkill.ShortIrons
        : club == Clubs.Wedge ? ProSkill.Wedge
        : ProSkill.MidIrons;

    /// <summary>Multiplicateur des écarts types donné par un niveau (éventuellement demi-niveau) : <see cref="SkillLevels.FromHandicap"/> du handicap équivalent.</summary>
    public static double Factor(double level) => SkillLevels.FromHandicap(HandicapOf(level));

    /// <summary>Multiplicateur de la vitesse de balle donné par Distance : celle d'un golfeur de même niveau, en ligne droite entre 0 et 50.</summary>
    public double SpeedFactor => SpeedFactorAt(Level(ProSkill.Distance));

    /// <summary>Multiplicateur de vitesse de balle au niveau <paramref name="level"/> de Distance.</summary>
    public static double SpeedFactorAt(double level) => 1.0 - (PowerLossPerSkill * (Factor(0) - 1.0) * (1.0 - (level / MaxLevel)));

    /// <summary>Multiplicateur des écarts types de direction d'un club : son niveau et celui de Précision à parts égales ; un chip y ajoute Chip (tiers égaux).</summary>
    public double DirectionFactor(Club club, ShotShape shape = ShotShape.Straight) =>
        club.IsPutter ? Factor(Level(ProSkill.Putter))
        : shape == ShotShape.Chip ? Factor((Level(OfClub(club)) + Level(ProSkill.Precision) + Level(ProSkill.Chip)) / 3.0)
        : Factor((Level(OfClub(club)) + Level(ProSkill.Precision)) / 2.0);

    /// <summary>Multiplicateur des écarts types de force : le niveau du club et celui de Dosage à parts égales pour un putt ou une force partielle, le club seul à pleine force ; un chip y ajoute Chip.</summary>
    public double ForceFactor(Club club, double force, ShotShape shape = ShotShape.Straight)
    {
        var dosed = club.IsPutter || force < _params.FullForce;
        if (shape == ShotShape.Chip && !club.IsPutter)
        {
            return dosed
                ? Factor((Level(OfClub(club)) + Level(ProSkill.Dosage) + Level(ProSkill.Chip)) / 3.0)
                : Factor((Level(OfClub(club)) + Level(ProSkill.Chip)) / 2.0);
        }

        return dosed ? Factor((Level(OfClub(club)) + Level(ProSkill.Dosage)) / 2.0) : Factor(Level(OfClub(club)));
    }

    /// <summary>Écart type de l'effet voulu (<see cref="ShotShapes.Launch"/>, amount) ; nul pour un coup droit. Pour le chip : <see cref="ProgressionParams.ChipSigma"/> au niveau 0, réduit comme la dispersion.</summary>
    public double ShapeSigma(ShotShape shape) => shape switch
    {
        ShotShape.Draw => ShapeSigmaPerLevel * Factor(Level(ProSkill.Draw)),
        ShotShape.Fade => ShapeSigmaPerLevel * Factor(Level(ProSkill.Fade)),
        ShotShape.Chip => _params.ChipSigma * Factor(Level(ProSkill.Chip)) / Factor(0),
        _ => 0.0,
    };

    /// <summary>
    /// Joue le coup <paramref name="shot"/> avec les niveaux du pro. Le résolveur n'a qu'un facteur de dispersion pour la direction et la
    /// vitesse : on lui donne le plus petit et l'écart à l'autre s'ajoute ici en quadrature (somme de deux lois normales). Sans
    /// <paramref name="rng"/>, coup prévu : aucun hasard.
    /// </summary>
    internal ShotResult Execute(PhysicalShotResolver resolver, TerrainGrid terrain, Point2 ball, in ProShot shot, DeterministicRng? rng, Point2 pin, ProBias? bias = null)
    {
        var club = shot.Club;
        var lie = club.IsPutter ? LieEffect.None : LieOf(terrain.SurfaceAt(ball.X, ball.Z));
        var direction = DirectionFactor(club, shot.Shape) * lie.Sigma;
        var force = ForceFactor(club, shot.Force, shot.Shape) * lie.Sigma;
        var dx = shot.Target.X - ball.X;
        var dz = shot.Target.Z - ball.Z;
        var d = Math.Sqrt((dx * dx) + (dz * dz));
        var (ux, uz) = d < 1e-9 ? (1.0, 0.0) : (dx / d, dz / d);
        var common = Math.Min(direction, force);
        var speed = shot.Force * SpeedFactor * lie.Speed;
        var speedNoise = 0.0;
        if (rng is not null)
        {
            var extraDirection = Math.Sqrt((direction * direction) - (common * common));
            var extraForce = Math.Sqrt((force * force) - (common * common));
            if (extraDirection > 0.0)
            {
                // Même écart type de base que le résolveur (pour un putt, avec son terme par mètre) : le latéral ne dépend que de la direction.
                var eps = PhysicalShotResolver.DirectionSigmaAt(club, d) * extraDirection * Gauss(rng);
                (ux, uz) = ((ux * Math.Cos(eps)) - (uz * Math.Sin(eps)), (ux * Math.Sin(eps)) + (uz * Math.Cos(eps)));
            }

            if (extraForce > 0.0)
            {
                speedNoise = club.SpeedSigma * extraForce * Gauss(rng);
                speed *= 1.0 + speedNoise;
            }
        }

        if (club.IsPutter)
        {
            // La vitesse d'un putt vient de la distance visée (v² = 2 r d) : un écart de force s'obtient en allongeant ou raccourcissant la cible.
            var aimed = d * (1.0 + speedNoise) * (1.0 + speedNoise);
            return resolver.PuttTo(ball, new Point2(ball.X + (ux * aimed), ball.Z + (uz * aimed)), terrain, rng, common, pin, bias?.SlopeAngle ?? 0.0, bias?.SlopeScale ?? 1.0);
        }

        return resolver.ExecuteShaped(ball, club, ux, uz, speed, shot.Shape, terrain, rng, common, 1.0, ShapeSigma(shot.Shape), pin, lie);
    }

    /// <summary>
    /// Pénalité du lie de départ pour le pro (C25 lot 2) : celle d'un golfeur du handicap équivalent de son habileté Sortie de rough ou
    /// Sortie de bunker (36 au niveau 0, scratch au niveau 50). Les autres lies ne coûtent rien.
    /// </summary>
    public LieEffect LieOf(Surface lie) => lie switch
    {
        Surface.Rough => LieParams.Default.At(lie, Handicap(ProSkill.RoughExit)),
        Surface.Bunker => LieParams.Default.At(lie, Handicap(ProSkill.BunkerExit)),
        _ => LieEffect.None,
    };

    /// <summary>Amplitude du biais de l'aperçu pour un niveau : 1 au niveau 0, décroît linéairement jusqu'à 0 au niveau 50.</summary>
    public static double BiasAmplitude(int level) => Math.Clamp(1.0 - (level / (double)MaxLevel), 0.0, 1.0);

    /// <summary>
    /// Biais de l'aperçu du coup numéro <paramref name="strokes"/> au tick <paramref name="tick"/> (C25) : vent faussé (force et direction) et
    /// pente lue faussée, d'une amplitude réglée par Jeu dans le vent et Lecture du green. Déterministe, tiré d'un hachage du tick et du coup :
    /// il ne touche ni au RNG du monde ni à l'état. Chaque tirage a une valeur absolue d'au moins la moitié de l'amplitude.
    /// </summary>
    public ProBias BiasFor(long tick, int strokes, Vec3 wind)
    {
        var h1 = Mix64(((ulong)tick * 0x9E3779B97F4A7C15UL) ^ ((ulong)strokes * 0xC2B2AE3D27D4EB4FUL));
        var h2 = Mix64(h1);
        var h3 = Mix64(h2);
        var h4 = Mix64(h3);
        var aw = BiasAmplitude(Level(ProSkill.Wind));
        var ag = BiasAmplitude(Level(ProSkill.GreenReading));
        var strength = Math.Sqrt((wind.X * wind.X) + (wind.Z * wind.Z));
        var seen = wind;
        if (strength > 1e-9 && aw > 0.0)
        {
            var speed = strength * (1.0 + (aw * _params.WindBiasForce * Signed(h1)));
            var angle = Math.Atan2(wind.Z, wind.X) + (aw * _params.WindBiasAngle * Signed(h2));
            seen = new Vec3(speed * Math.Cos(angle), wind.Y, speed * Math.Sin(angle));
        }

        return new ProBias(seen, ag * _params.SlopeBiasAngle * Signed(h3), 1.0 + (ag * _params.SlopeBiasScale * Signed(h4)));
    }

    private static ulong Mix64(ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        return x ^ (x >> 31);
    }

    /// <summary>Tirage signé de valeur absolue entre 0,5 et 1.</summary>
    private static double Signed(ulong h)
    {
        var magnitude = 0.5 + (0.5 * (h >> 11) * (1.0 / (1UL << 53)));
        return (h & 1UL) == 0UL ? magnitude : -magnitude;
    }

    /// <summary>XP d'un coup joué : chaque habileté sollicitée reçoit la sienne, selon la qualité (voir <see cref="ProQuality"/>).</summary>
    public void Record(TerrainGrid terrain, Point2 start, in ProShot shot, in ShotResult aimed, in ShotResult result, bool league, int year, double windSpeed = 0.0, Point2? pin = null)
    {
        foreach (var (skill, hundredths) in ProQuality.Awards(_params, terrain, start, shot, aimed, result, windSpeed, pin))
        {
            Gain(skill, hundredths, league, year);
        }
    }

    /// <summary>Double en ligue, sans plafond ; à l'entraînement, dans la limite de l'année (remise à zéro au 1er avril). En centièmes d'XP.</summary>
    public void Gain(ProSkill skill, long hundredths, bool league, int year)
    {
        if (league)
        {
            _xp[(int)skill] += 2 * hundredths;
            return;
        }

        if (year != _trainingYear)
        {
            _trainingYear = year;
            Array.Clear(_trainingThisYear);
        }

        var room = Math.Max(0, TrainingCap - _trainingThisYear[(int)skill]);
        var gain = Math.Min(hundredths, room);
        _trainingThisYear[(int)skill] += gain;
        _xp[(int)skill] += gain;
    }

    /// <summary>Met l'habileté au début de <paramref name="level"/> (tests).</summary>
    internal void SetLevel(ProSkill skill, int level) => _xp[(int)skill] = _floor[level];

    private static long[] BuildFloors(ProgressionParams p)
    {
        var floors = new long[MaxLevel + 1];
        for (var n = 0; n < MaxLevel; n++)
        {
            floors[n + 1] = floors[n] + p.CostHundredths(n);
        }

        return floors;
    }

    private static double Gauss(DeterministicRng rng)
    {
        var u = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u)) * Math.Cos(2.0 * Math.PI * rng.NextDouble());
    }

    internal void Write(SaveWriter w)
    {
        w.Int(_trainingYear);
        foreach (var v in _xp)
        {
            w.Long(v);
        }

        foreach (var v in _trainingThisYear)
        {
            w.Long(v);
        }
    }

    internal static ProSkills Read(SaveReader r, ProgressionParams progression)
    {
        var skills = new ProSkills(progression) { _trainingYear = r.Int() };
        for (var i = 0; i < Count; i++)
        {
            skills._xp[i] = r.Long();
        }

        for (var i = 0; i < Count; i++)
        {
            skills._trainingThisYear[i] = r.Long();
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
}
