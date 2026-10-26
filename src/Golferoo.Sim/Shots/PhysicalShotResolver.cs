using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Shots;

/// <summary>
/// Résolveur physique : planifie le coup par échantillonnage (club, direction, force) avec la physique de
/// <see cref="BallSimulator"/> sans bruit, garde le meilleur, puis l'exécute avec une dispersion tirée du RNG du monde.
/// Sur le green, le putt vise le trou avec une vitesse ajustée par quelques simulations.
/// </summary>
public sealed class PhysicalShotResolver : IShotResolver
{
    /// <summary>Distance visée au-delà du trou pour un putt, en mètres.</summary>
    public const double PuttOvershoot = 0.4;

    /// <summary>Écart type supplémentaire de la direction d'un putt par mètre de distance, en radians (0,06 degré).</summary>
    private const double PuttSigmaPerMeter = 0.06 * Math.PI / 180.0;

    /// <summary>Vitesse de balle perdue par point de niveau au-dessus de 1.</summary>
    private const double PowerLossPerSkill = 0.03;

    private const double MaxPuttSpeed = 10.0;
    private const int PuttIterations = 8;
    private const int Finalists = 4;
    private const double FlankSigmas = 1.5;

    /// <summary>Coût d'un coup de pénalité, en mètres équivalents.</summary>
    private const double PenaltyCost = 100.0;

    /// <summary>Coût d'une balle arrêtée par une couronne : celui d'une balle sous les arbres (voir <c>Cost</c>).</summary>
    private const double TreesLieCost = 25.0;

    /// <summary>Probabilité d'arrêt du meilleur candidat au-delà de laquelle la ligne du trou est jugée bouchée.</summary>
    private const double BlockedStop = 0.25;

    /// <summary>Portée minimale d'un candidat quand la ligne du trou est bouchée (on peut ressortir de côté d'un coup court), en mètres.</summary>
    private const double WideMinRange = 15.0;

    private static readonly double[] OffsetDegrees = [-20, -10, -4, 0, 4, 10, 20];
    private static readonly double[] WideDegrees = [-90, -60, -35, 35, 60, 90];
    private static readonly double[] LowDegrees = [-10, 0, 10];
    private static readonly double[] WideForces = [1.0, 0.65, 0.35];
    private static readonly double[] Forces = [1.0, 0.9, 0.8, 0.65, 0.5, 0.35];

    private readonly double _drag;

    public PhysicalShotResolver(double drag = BallSimulator.DefaultDrag)
    {
        _drag = drag;
    }

    /// <summary>
    /// Arbres que rencontre la balle (nul : aucun). L'exécution tire leurs couronnes au hasard ; la planification les voit en
    /// espérance (probabilité d'arrêt cumulée, sans tirage). Le monde y met ses arbres.
    /// </summary>
    public TreeStand? Trees { get; set; }

    /// <summary>Saison des couronnes, réglée par le monde.</summary>
    public Season Season { get; set; } = Season.Summer;

    /// <summary>Vent du moment, réglé par le monde. Le golfeur le connaît exactement : la planification simule avec lui.</summary>
    public Vec3 Wind { get; set; }

    public ShotResult Resolve(in ShotRequest request, TerrainGrid terrain, DeterministicRng rng)
    {
        if (terrain.SurfaceAt(request.Ball.X, request.Ball.Z) == Surface.Green)
        {
            return Putt(request, terrain, rng);
        }

        var plan = Plan(request, terrain);
        return Execute(request.Ball, plan.Club, plan.DirX, plan.DirZ, plan.Force, terrain, rng, request.Skill, plan.Low);
    }

    /// <summary>
    /// Exécute un coup avec la dispersion du club (direction et vitesse tirées du RNG), élargie par le niveau de jeu
    /// et avec une balle un peu moins rapide au-delà du niveau 1. Au niveau 1, chaque facteur vaut exactement 1.
    /// <paramref name="low"/> : sortie basse, lancement à la moitié de l'angle du club comme le chip de <see cref="ShotShape.Chip"/>.
    /// </summary>
    public ShotResult Execute(Point2 ball, Club club, double dirX, double dirZ, double force, TerrainGrid terrain, DeterministicRng rng, double skill = 1.0, bool low = false)
    {
        var eps = club.DirectionSigma * skill * Gauss(rng);
        var power = 1.0 - (PowerLossPerSkill * Math.Max(0.0, skill - 1.0));
        var speed = club.Speed * force * (1.0 + (club.SpeedSigma * skill * Gauss(rng))) * power;
        Rotate(dirX, dirZ, eps, 1.0 - (eps * eps / 2.0), out var ux, out var uz);
        var velocity = new Vec3(ux * club.LaunchCos * speed, club.LaunchSin * speed, uz * club.LaunchCos * speed);
        var backspin = 0.0;
        if (low)
        {
            (velocity, _, backspin) = ShotShapes.Launch(club, ux, uz, speed, ShotShape.Chip);
        }

        return BallSimulator.Run(terrain, ball, velocity, rolling: false, _drag, recordPath: true, trees: Trees, season: Season, rng: rng, wind: Wind, backspin: backspin).Shot;
    }

    /// <summary>
    /// Coup choisi (votre pro) : club, direction, force et trajectoire, avec le spin du club. <paramref name="dispersion"/> multiplie
    /// les écarts types du club ; sans <paramref name="rng"/>, le coup est exécuté sans hasard ni arbres (trajectoire prévue).
    /// <paramref name="amount"/> multiplie l'effet voulu (1 : exécuté comme voulu), à un écart type <paramref name="amountSigma"/> près.
    /// </summary>
    public ShotResult ExecuteShaped(Point2 ball, Club club, double dirX, double dirZ, double force, ShotShape shape, TerrainGrid terrain, DeterministicRng? rng, double dispersion = 1.0, double amount = 1.0, double amountSigma = 0.0)
    {
        var eps = rng is null ? 0.0 : club.DirectionSigma * dispersion * Gauss(rng);
        var speed = club.Speed * force * (rng is null ? 1.0 : 1.0 + (club.SpeedSigma * dispersion * Gauss(rng)));
        if (rng is not null && amountSigma > 0.0)
        {
            amount = Math.Clamp(amount + (amountSigma * Gauss(rng)), 0.0, 2.0);
        }

        Rotate(dirX, dirZ, eps, 1.0 - (eps * eps / 2.0), out var ux, out var uz);
        var (velocity, curve, backspin) = ShotShapes.Launch(club, ux, uz, speed, shape, amount);
        return BallSimulator.Run(terrain, ball, velocity, rolling: false, _drag, recordPath: true, trees: rng is null ? null : Trees, season: Season, rng: rng, wind: Wind, curve: curve, backspin: backspin).Shot;
    }

    /// <summary>
    /// Putt choisi (votre pro) : vers <paramref name="target"/>, à la vitesse qui l'y amènerait sur un green plat ; la pente est au
    /// joueur de la lire. Sans <paramref name="rng"/>, sans hasard (trajectoire prévue).
    /// </summary>
    public ShotResult PuttTo(Point2 ball, Point2 target, TerrainGrid terrain, DeterministicRng? rng, double dispersion = 1.0)
    {
        var ex = target.X - ball.X;
        var ez = target.Z - ball.Z;
        var dist = Math.Sqrt((ex * ex) + (ez * ez));
        if (dist < 1e-9)
        {
            ex = 1.0;
            dist = 1.0;
        }

        var club = Clubs.Putter;
        var resistance = terrain.Soil.RollResistanceAt(ball.X, ball.Z) * BallSimulator.Gravity;
        var speed = Math.Min(Math.Sqrt(2.0 * resistance * dist), MaxPuttSpeed);
        var eps = rng is null ? 0.0 : (club.DirectionSigma + (PuttSigmaPerMeter * dist)) * dispersion * Gauss(rng);
        speed *= rng is null ? 1.0 : 1.0 + (club.SpeedSigma * dispersion * Gauss(rng));
        Rotate(ex / dist, ez / dist, eps, 1.0 - (eps * eps / 2.0), out var dx, out var dz);
        return BallSimulator.Run(terrain, ball, new Vec3(dx * speed, 0, dz * speed), rolling: true, _drag, recordPath: true).Shot;
    }

    private static void Rotate(double ux, double uz, double sin, double cos, out double rx, out double rz)
    {
        var x = (ux * cos) - (uz * sin);
        var z = (ux * sin) + (uz * cos);
        var inv = 1.0 / Math.Sqrt((x * x) + (z * z));
        rx = x * inv;
        rz = z * inv;
    }

    /// <summary>Quasi-gaussienne centrée réduite (somme de quatre uniformes), sans log ni trigonométrie.</summary>
    private static double Gauss(DeterministicRng rng)
    {
        var s = rng.NextDouble() + rng.NextDouble() + rng.NextDouble() + rng.NextDouble();
        return (s - 2.0) * 1.7320508075688772;
    }

    private static double Cost(in ShotResult r, Point2 hole)
    {
        var dx = r.Ball.X - hole.X;
        var dz = r.Ball.Z - hole.Z;
        var cost = Math.Sqrt((dx * dx) + (dz * dz)) + (PenaltyCost * r.PenaltyStrokes);
        cost += r.Outcome switch
        {
            ShotOutcome.Green => -8.0,
            ShotOutcome.Fairway or ShotOutcome.Tee or ShotOutcome.Path => -2.0,
            ShotOutcome.Rough => 6.0,
            ShotOutcome.Bunker => 18.0,
            ShotOutcome.Trees => TreesLieCost,
            ShotOutcome.Water or ShotOutcome.OutOfBounds => 60.0,
            _ => 0.0,
        };
        return cost;
    }

    private (Club Club, double DirX, double DirZ, double Force, bool Low) Plan(in ShotRequest request, TerrainGrid terrain)
    {
        var ex = request.Hole.X - request.Ball.X;
        var ez = request.Hole.Z - request.Ball.Z;
        var dist = Math.Sqrt((ex * ex) + (ez * ez));
        if (dist < 1e-9)
        {
            return (Clubs.Wedge, 1.0, 0.0, Forces[^1], false);
        }

        var ux = ex / dist;
        var uz = ez / dist;
        var ball = request.Ball;
        var hole = request.Hole;
        var candidates = new List<(double Cost, double Stop, Club Club, double DirX, double DirZ, double Force, bool Low)>();

        void Add(IReadOnlyList<double> degrees, IReadOnlyList<double> forces, bool low, bool relaxed)
        {
            foreach (var club in Clubs.Full)
            {
                foreach (var force in forces)
                {
                    var range = club.TargetRange * force * force;
                    if (range > (1.3 * dist) + 15.0 || range < (relaxed ? WideMinRange : 0.3 * dist))
                    {
                        continue;
                    }

                    foreach (var degree in degrees)
                    {
                        var radians = degree * Math.PI / 180.0;
                        Rotate(ux, uz, Math.Sin(radians), Math.Cos(radians), out var dx, out var dz);
                        var cost = Evaluate(ball, hole, club, dx, dz, force, low, terrain, out var stop);
                        candidates.Add((cost, stop, club, dx, dz, force, low));
                    }
                }
            }
        }

        Add(OffsetDegrees, Forces, low: false, relaxed: false);

        // Ligne du trou bouchée par les arbres : on essaie aussi les coups bas (sous les branches) et les directions jusqu'à ±90°
        // pour ressortir de côté, avec moins de forces et de directions pour tenir le temps de planification.
        if (candidates.Count > 0 && Best(candidates).Stop > BlockedStop)
        {
            Add(LowDegrees, WideForces, low: true, relaxed: false);
            Add(WideDegrees, WideForces, low: false, relaxed: true);
        }

        if (candidates.Count == 0)
        {
            return (Clubs.Wedge, ux, uz, Forces[^1], false);
        }

        // Les meilleurs candidats sont re-testés avec la direction décalée de chaque côté, pour préférer un coup sûr.
        candidates.Sort(static (a, b) => a.Cost.CompareTo(b.Cost));
        var best = candidates[0];
        var bestScore = double.MaxValue;
        for (var k = 0; k < Math.Min(Finalists, candidates.Count); k++)
        {
            var c = candidates[k];
            var eps = c.Club.DirectionSigma * FlankSigmas;
            var cos = 1.0 - (eps * eps / 2.0);
            Rotate(c.DirX, c.DirZ, eps, cos, out var ax, out var az);
            Rotate(c.DirX, c.DirZ, -eps, cos, out var bx, out var bz);
            var left = Evaluate(ball, hole, c.Club, ax, az, c.Force, c.Low, terrain, out _);
            var right = Evaluate(ball, hole, c.Club, bx, bz, c.Force, c.Low, terrain, out _);
            var score = ((2.0 * c.Cost) + left + right) / 4.0;
            if (score < bestScore)
            {
                bestScore = score;
                best = c;
            }
        }

        return (best.Club, best.DirX, best.DirZ, best.Force, best.Low);
    }

    private static (double Cost, double Stop, Club Club, double DirX, double DirZ, double Force, bool Low) Best(List<(double Cost, double Stop, Club Club, double DirX, double DirZ, double Force, bool Low)> candidates)
    {
        var best = candidates[0];
        foreach (var c in candidates)
        {
            if (c.Cost < best.Cost)
            {
                best = c;
            }
        }

        return best;
    }

    /// <summary>
    /// Coût espéré d'un candidat, sans hasard : p × coût(arrêté au pied des arbres) + (1 − p) × coût(passé), p étant la probabilité
    /// cumulée d'être arrêté par une couronne. Les troncs agissent comme à l'exécution. <paramref name="stop"/> : part des balles
    /// qui finissent sous les arbres, pour juger la ligne du trou bouchée.
    /// </summary>
    private double Evaluate(Point2 ball, Point2 hole, Club club, double dirX, double dirZ, double force, bool low, TerrainGrid terrain, out double stop)
    {
        var speed = club.Speed * force;
        Vec3 velocity;
        var backspin = 0.0;
        if (low)
        {
            (velocity, _, backspin) = ShotShapes.Launch(club, dirX, dirZ, speed, ShotShape.Chip);
        }
        else
        {
            velocity = new Vec3(dirX * club.LaunchCos * speed, club.LaunchSin * speed, dirZ * club.LaunchCos * speed);
        }

        var expectation = Trees is null ? null : new TreeCollision.Expectation();
        var shot = BallSimulator.Run(terrain, ball, velocity, rolling: false, _drag, recordPath: false, trees: Trees, season: Season, wind: Wind, backspin: backspin, expectation: expectation).Shot;
        var cost = Cost(shot, hole);
        var crown = expectation?.Stopped ?? 0.0;

        // Part des balles qui finissent sous les arbres : arrêtées par une couronne, ou passées mais retombées dans le bois.
        stop = crown + ((1.0 - crown) * (shot.Outcome == ShotOutcome.Trees ? 1.0 : 0.0));
        if (crown <= 0.0)
        {
            return cost;
        }

        var at = expectation!.StopPoint;
        var stopped = Math.Sqrt(((at.X - hole.X) * (at.X - hole.X)) + ((at.Z - hole.Z) * (at.Z - hole.Z))) + TreesLieCost;
        return (crown * stopped) + ((1.0 - crown) * cost);
    }

    private ShotResult Putt(in ShotRequest request, TerrainGrid terrain, DeterministicRng rng)
    {
        var ball = request.Ball;
        var ex = request.Hole.X - ball.X;
        var ez = request.Hole.Z - ball.Z;
        var dist = Math.Sqrt((ex * ex) + (ez * ez));
        if (dist < 1e-9)
        {
            return new ShotResult(request.Hole, true, 0, ShotOutcome.Holed, [new Vec3(ball.X, terrain.Sample(ball.X, ball.Z), ball.Z)]);
        }

        var ux = ex / dist;
        var uz = ez / dist;
        var target = dist + PuttOvershoot;
        var resistance = terrain.Soil.RollResistanceAt(ball.X, ball.Z) * BallSimulator.Gravity;
        var speed = Math.Min(Math.Sqrt(2.0 * resistance * target), MaxPuttSpeed);
        var aimX = ux;
        var aimZ = uz;
        var bestX = aimX;
        var bestZ = aimZ;
        var bestSpeed = speed;
        var bestError = double.MaxValue;

        // Quelques essais sans bruit : on corrige la direction d'après l'écart latéral au passage du trou,
        // et la vitesse d'après la distance parcourue contre la distance visée.
        var perpX = -uz;
        var perpZ = ux;
        for (var i = 0; i < PuttIterations; i++)
        {
            var sim = BallSimulator.Run(terrain, ball, new Vec3(aimX * speed, 0, aimZ * speed), rolling: true, _drag, recordPath: false);
            var trial = sim.Shot;
            if (trial.Holed)
            {
                bestX = aimX;
                bestZ = aimZ;
                bestSpeed = speed;
                break;
            }

            var along = ((trial.Ball.X - ball.X) * ux) + ((trial.Ball.Z - ball.Z) * uz);
            var lateral = ((sim.ClosestApproach.X - request.Hole.X) * perpX) + ((sim.ClosestApproach.Z - request.Hole.Z) * perpZ);
            var error = ((along - target) * (along - target)) + (lateral * lateral);
            if (error < bestError)
            {
                bestError = error;
                bestX = aimX;
                bestZ = aimZ;
                bestSpeed = speed;
            }

            var nx = aimX - (perpX * lateral / dist);
            var nz = aimZ - (perpZ * lateral / dist);
            var inv = 1.0 / Math.Sqrt((nx * nx) + (nz * nz));
            aimX = nx * inv;
            aimZ = nz * inv;
            var ratio = along > 0.05 ? Math.Sqrt(target / along) : 1.6;
            speed = Math.Min(speed * Math.Clamp(ratio, 0.6, 1.6), MaxPuttSpeed);
        }

        var club = Clubs.Putter;
        var eps = (club.DirectionSigma + (PuttSigmaPerMeter * dist)) * request.Skill * Gauss(rng);
        var noisy = bestSpeed * (1.0 + (club.SpeedSigma * request.Skill * Gauss(rng)));
        Rotate(bestX, bestZ, eps, 1.0 - (eps * eps / 2.0), out var dx, out var dz);
        return BallSimulator.Run(terrain, ball, new Vec3(dx * noisy, 0, dz * noisy), rolling: true, _drag, recordPath: true).Shot;
    }
}
