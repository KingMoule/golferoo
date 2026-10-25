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

    private const double MaxPuttSpeed = 10.0;
    private const int PuttIterations = 8;
    private const int Finalists = 4;
    private const double FlankSigmas = 1.5;

    /// <summary>Coût d'un coup de pénalité, en mètres équivalents.</summary>
    private const double PenaltyCost = 100.0;

    private static readonly double[] OffsetDegrees = [-20, -10, -4, 0, 4, 10, 20];
    private static readonly double[] OffsetCos = OffsetDegrees.Select(d => Math.Cos(d * Math.PI / 180.0)).ToArray();
    private static readonly double[] OffsetSin = OffsetDegrees.Select(d => Math.Sin(d * Math.PI / 180.0)).ToArray();
    private static readonly double[] Forces = [1.0, 0.9, 0.8, 0.65, 0.5, 0.35];

    private readonly double _drag;

    public PhysicalShotResolver(double drag = BallSimulator.DefaultDrag)
    {
        _drag = drag;
    }

    /// <summary>
    /// Arbres que rencontre la balle d'un coup exécuté (nul : aucun). La planification les ignore : elle ne contourne pas
    /// les arbres, elle les subit. Le monde y met ses arbres.
    /// </summary>
    public TreeStand? Trees { get; set; }

    /// <summary>Saison des couronnes, réglée par le monde.</summary>
    public Season Season { get; set; } = Season.Summer;

    public ShotResult Resolve(in ShotRequest request, TerrainGrid terrain, DeterministicRng rng)
    {
        if (terrain.SurfaceAt(request.Ball.X, request.Ball.Z) == Surface.Green)
        {
            return Putt(request, terrain, rng);
        }

        var plan = Plan(request, terrain);
        return Execute(request.Ball, plan.Club, plan.DirX, plan.DirZ, plan.Force, terrain, rng);
    }

    /// <summary>Exécute un coup avec la dispersion du club (direction et vitesse tirées du RNG).</summary>
    public ShotResult Execute(Point2 ball, Club club, double dirX, double dirZ, double force, TerrainGrid terrain, DeterministicRng rng)
    {
        var eps = club.DirectionSigma * Gauss(rng);
        var speed = club.Speed * force * (1.0 + (club.SpeedSigma * Gauss(rng)));
        Rotate(dirX, dirZ, eps, 1.0 - (eps * eps / 2.0), out var ux, out var uz);
        var velocity = new Vec3(ux * club.LaunchCos * speed, club.LaunchSin * speed, uz * club.LaunchCos * speed);
        return BallSimulator.Run(terrain, ball, velocity, rolling: false, _drag, recordPath: true, trees: Trees, season: Season, rng: rng).Shot;
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
            ShotOutcome.Trees => 25.0,
            ShotOutcome.Water or ShotOutcome.OutOfBounds => 60.0,
            _ => 0.0,
        };
        return cost;
    }

    private (Club Club, double DirX, double DirZ, double Force) Plan(in ShotRequest request, TerrainGrid terrain)
    {
        var ex = request.Hole.X - request.Ball.X;
        var ez = request.Hole.Z - request.Ball.Z;
        var dist = Math.Sqrt((ex * ex) + (ez * ez));
        if (dist < 1e-9)
        {
            return (Clubs.Wedge, 1.0, 0.0, Forces[^1]);
        }

        var ux = ex / dist;
        var uz = ez / dist;
        var candidates = new List<(double Cost, Club Club, double DirX, double DirZ, double Force)>();
        foreach (var club in Clubs.Full)
        {
            foreach (var force in Forces)
            {
                var range = club.TargetRange * force * force;
                if (range > (1.3 * dist) + 15.0 || range < 0.3 * dist)
                {
                    continue;
                }

                for (var i = 0; i < OffsetDegrees.Length; i++)
                {
                    Rotate(ux, uz, OffsetSin[i], OffsetCos[i], out var dx, out var dz);
                    var result = Simulate(request.Ball, club, dx, dz, force, terrain);
                    candidates.Add((Cost(result, request.Hole), club, dx, dz, force));
                }
            }
        }

        if (candidates.Count == 0)
        {
            return (Clubs.Wedge, ux, uz, Forces[^1]);
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
            var left = Cost(Simulate(request.Ball, c.Club, ax, az, c.Force, terrain), request.Hole);
            var right = Cost(Simulate(request.Ball, c.Club, bx, bz, c.Force, terrain), request.Hole);
            var score = ((2.0 * c.Cost) + left + right) / 4.0;
            if (score < bestScore)
            {
                bestScore = score;
                best = c;
            }
        }

        return (best.Club, best.DirX, best.DirZ, best.Force);
    }

    private ShotResult Simulate(Point2 ball, Club club, double dirX, double dirZ, double force, TerrainGrid terrain)
    {
        var speed = club.Speed * force;
        var velocity = new Vec3(dirX * club.LaunchCos * speed, club.LaunchSin * speed, dirZ * club.LaunchCos * speed);
        return BallSimulator.Run(terrain, ball, velocity, rolling: false, _drag, recordPath: false).Shot;
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
        var eps = (club.DirectionSigma + (PuttSigmaPerMeter * dist)) * Gauss(rng);
        var noisy = bestSpeed * (1.0 + (club.SpeedSigma * Gauss(rng)));
        Rotate(bestX, bestZ, eps, 1.0 - (eps * eps / 2.0), out var dx, out var dz);
        return BallSimulator.Run(terrain, ball, new Vec3(dx * noisy, 0, dz * noisy), rolling: true, _drag, recordPath: true).Shot;
    }
}
