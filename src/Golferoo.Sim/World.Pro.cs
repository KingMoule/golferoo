using Golferoo.Sim.Golfers;
using Golferoo.Sim.Pro;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;

namespace Golferoo.Sim;

/// <summary>Votre pro joué par le joueur (C08) : coups choisis, expérience par compétence.</summary>
public sealed partial class World
{
    private readonly Queue<ProShot> _proShots = new();
    private readonly int _proMemberId = -1;
    private bool _manualPro;

    /// <summary>Compétences de votre pro (nul sans ligue).</summary>
    public ProSkills? Pro { get; }

    /// <summary>Réglages de la progression du pro (C25), sauvegardés avec la partie.</summary>
    public ProgressionParams Progression { get; }

    /// <summary>Année de jeu du plafond d'entraînement : elle change le 1er avril, début de la saison (et non le 1er janvier).</summary>
    public int PlayYear => Date.Month < Calendar.StartMonth ? Date.Year - 1 : Date.Year;

    /// <summary>
    /// Vrai dans le jeu : à son tour, votre pro attend le coup du joueur (<see cref="GolferState.AwaitingShot"/>) et ne joue jamais seul.
    /// Faux par défaut (tests, banc) : il joue avec l'IA de C06, sans gagner d'expérience. Exige la ligue et le résolveur physique.
    /// </summary>
    public bool ManualPro
    {
        get => _manualPro;
        set
        {
            if (value && (Pro is null || _resolver is not PhysicalShotResolver))
            {
                throw new InvalidOperationException("Le pro joué à la main exige la ligue et le résolveur physique.");
            }

            _manualPro = value;
        }
    }

    /// <summary>Votre pro s'il attend le coup du joueur, sinon nul.</summary>
    public Golfer? AwaitingPro
    {
        get
        {
            foreach (var group in _groups)
            {
                foreach (var g in group.Golfers)
                {
                    if (g.State == GolferState.AwaitingShot)
                    {
                        return g;
                    }
                }
            }

            return null;
        }
    }

    /// <summary>Le trou en cours du groupe de ce golfeur (le trou 1 si le golfeur ne joue pas).</summary>
    public CourseHole HoleOf(Golfer golfer) => RequireTerrain().Course[_groups.FirstOrDefault(g => g.Golfers.Contains(golfer))?.Hole ?? 1];

    /// <summary>Vrai tant qu'un coup du pro attend son tick : le mode visée ne doit pas se rouvrir entre-temps.</summary>
    public bool ProShotPending => _proShots.Count > 0;

    /// <summary>Coup du pro, en file comme les autres commandes : joué au tick suivant s'il attend, ignoré sinon.</summary>
    public void PlayProShot(ProShot shot)
    {
        ArgumentNullException.ThrowIfNull(shot.Club);
        if (!(shot.Force > 0.0 && shot.Force <= 1.0))
        {
            throw new ArgumentOutOfRangeException(nameof(shot), shot.Force, "Force attendue dans ]0, 1].");
        }

        _proShots.Enqueue(shot);
    }

    /// <summary>Coup prévu, sans hasard ni arbres : ce que le mode visée dessine. Le vent du moment est compris.</summary>
    public ShotResult PreviewProShot(ProShot shot)
    {
        var terrain = RequireTerrain();
        var golfer = AwaitingPro ?? throw new InvalidOperationException("Le pro n'attend pas de coup.");
        var resolver = (PhysicalShotResolver)_resolver;
        resolver.Wind = Wind;
        return Execute(resolver, terrain, golfer.Ball, shot, null, PinOf(terrain, golfer));
    }

    /// <summary>Distance au trou sous laquelle le putt du pro est donné (« gimme ») : un coup, balle au trou.</summary>
    public const double GimmeDistance = 2.0;

    private bool IsManualPro(Golfer g) => _manualPro && g.MemberId == _proMemberId;

    /// <summary>Putt donné : sur le green à moins de <see cref="GimmeDistance"/> du trou, le coup compte et la balle est au trou, sans expérience.</summary>
    private static bool Gimme(TerrainGrid terrain, Point2 pin, Golfer g)
    {
        var dx = pin.X - g.Ball.X;
        var dz = pin.Z - g.Ball.Z;
        if (terrain.SurfaceAt(g.Ball.X, g.Ball.Z) != Surface.Green || (dx * dx) + (dz * dz) >= GimmeDistance * GimmeDistance)
        {
            return false;
        }

        g.Strokes++;
        g.Ball = pin;
        g.Holed = true;
        g.State = GolferState.Exiting;
        return true;
    }

    private void ApplyProShots()
    {
        while (_proShots.Count > 0)
        {
            var shot = _proShots.Dequeue();
            if (AwaitingPro is { } pro)
            {
                pro.PlannedShot = shot;
                pro.State = Terrain!.SurfaceAt(pro.Ball.X, pro.Ball.Z) == Surface.Green ? GolferState.Putting : GolferState.Swinging;
            }
        }
    }

    private ShotResult ExecuteProShot(TerrainGrid terrain, Golfer g, ProShot shot, Point2 pin) =>
        Execute((PhysicalShotResolver)_resolver, terrain, g.Ball, shot, _rng, pin);

    /// <summary>Drapeau du trou que joue le groupe de ce golfeur.</summary>
    private Point2 PinOf(TerrainGrid terrain, Golfer g) =>
        terrain.Course[_groups.First(group => group.Golfers.Contains(g)).Hole].Hole;

    private ShotResult Execute(PhysicalShotResolver resolver, TerrainGrid terrain, Point2 ball, ProShot shot, DeterministicRng? rng, Point2 pin) =>
        Pro!.Execute(resolver, terrain, ball, shot, rng, pin);

    /// <summary>Le coup prévu sans hasard, depuis la balle : le point visé qui sert de mesure à la qualité du coup (C25).</summary>
    private ShotResult AimedResult(TerrainGrid terrain, Golfer g, ProShot shot)
    {
        var resolver = (PhysicalShotResolver)_resolver;
        resolver.Wind = Wind;
        return Execute(resolver, terrain, g.Ball, shot, null, PinOf(terrain, g));
    }

    private void MixPro(ref ulong hash)
    {
        if (Pro is null)
        {
            return;
        }

        Mix(ref hash, Pro.Hash() ^ (_manualPro ? 1UL : 0UL));
        foreach (var v in Progression.Values())
        {
            Mix(ref hash, (ulong)v);
        }

        foreach (var s in _proShots)
        {
            MixShot(ref hash, s);
        }
    }

    private static void MixShot(ref ulong hash, in ProShot s)
    {
        Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(s.Target.X) ^ ((ulong)s.Shape << 56));
        Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(s.Target.Z));
        Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(s.Force));
        Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(s.Club.Speed));
    }
}
