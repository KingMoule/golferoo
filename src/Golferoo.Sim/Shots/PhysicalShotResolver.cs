using Golferoo.Sim.Golfers;
using Golferoo.Sim.Pathing;
using Golferoo.Sim.Pro;
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

    /// <summary>Vitesse de balle perdue par point de niveau au-dessus de 1 (C21 : un handicap 18 perd 5 % de vitesse, 10 % de portée).</summary>
    private const double PowerLossPerSkill = 0.05;

    private const double MaxPuttSpeed = 10.0;
    private const int PuttIterations = 8;
    private const int Finalists = 4;

    /// <summary>Coût d'un coup de pénalité, en mètres équivalents.</summary>
    private const double PenaltyCost = 100.0;

    /// <summary>Écart de score au-delà duquel un choix non optimal n'est plus plausible : 0,5 coup, un coup de pénalité valant <see cref="PenaltyCost"/>.</summary>
    internal const double MaxMistakeCost = 0.5 * PenaltyCost;

    /// <summary>Coût d'une balle arrêtée par une couronne : celui d'une balle sous les arbres (voir <c>Cost</c>).</summary>
    private const double TreesLieCost = 60.0;

    /// <summary>
    /// Une balle qui finit à moins de ce rayon de son départ (rebond sur un tronc collé à elle) a gâché un coup : elle coûte
    /// <see cref="PenaltyCost"/> de plus. Sans ce coût, rester sur place battait toute sortie de forêt et le golfeur retapait sans fin.
    /// </summary>
    private const double WastedRadius = 5.0;

    /// <summary>Probabilité d'arrêt du meilleur candidat au-delà de laquelle la ligne du trou est jugée bouchée.</summary>
    private const double BlockedStop = 0.25;

    /// <summary>Portée minimale d'un candidat quand la ligne du trou est bouchée (on peut ressortir de côté d'un coup court), en mètres.</summary>
    private const double WideMinRange = 15.0;

    private static readonly double[] OffsetDegrees = [-20, -10, -4, 0, 4, 10, 20];
    private static readonly double[] WideDegrees = [-150, -120, -90, -60, -35, 35, 60, 90, 120, 150, 180];
    private static readonly double[] LowDegrees = [-10, 0, 10];
    private static readonly double[] WideForces = [1.0, 0.65, 0.35];
    private static readonly double[] Forces = [1.0, 0.9, 0.8, 0.65, 0.5, 0.35];

    /// <summary>Un chemin de balle plus long que la ligne droite de ce facteur : on vise le chemin et non plus le drapeau.</summary>
    private const double DetourFactor = 1.05;

    /// <summary>Distance au drapeau au-delà de laquelle le prudent vise le centre du green ; plus près, il vise le drapeau.</summary>
    private const double SafeAimRange = 40.0;

    private readonly double _drag;
    private readonly PlayMapCache _maps = new();
    private readonly Lock _beautyLock = new();
    private BeautyMap? _beauty;

    /// <summary>Cartes de jeu gardées (voir <see cref="PlayMapCache.Capacity"/>) : le monde l'ajuste au parcours.</summary>
    internal int MapCapacity
    {
        set => _maps.Capacity = value;
    }

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

    /// <summary>Sources de beauté posées dans le monde (objets de décor), pour l'artiste (<see cref="BeautyMap"/>). Le monde y met les siennes.</summary>
    public IReadOnlyList<Point2> BeautySources { get; set; } = [];

    public ShotResult Resolve(in ShotRequest request, TerrainGrid terrain, DeterministicRng rng) => Resolve(request, terrain, rng, out _);

    /// <summary>Comme <see cref="Resolve(in ShotRequest, TerrainGrid, DeterministicRng)"/>, en rendant le coup planifié (nul pour un putt).</summary>
    internal ShotResult Resolve(in ShotRequest request, TerrainGrid terrain, DeterministicRng rng, out ShotPlan? planned)
    {
        var profile = GolferProfile.Of(request.Profile);
        if (terrain.SurfaceAt(request.Ball.X, request.Ball.Z) == Surface.Green)
        {
            planned = null;
            var puttBeauty = profile.Artist ? Beauty(terrain).At(request.Ball.X, request.Ball.Z) : 0.0;
            return Putt(request, terrain, rng, profile.BeautyFactor(puttBeauty));
        }

        var plan = Plan(request, terrain, rng);
        planned = plan;
        return ExecutePlan(request, plan, terrain, rng);
    }

    /// <summary>Joue le coup <paramref name="plan"/> planifié pour <paramref name="request"/> (hors green), avec la dispersion tirée du RNG du monde.</summary>
    internal ShotResult ExecutePlan(in ShotRequest request, in ShotPlan plan, TerrainGrid terrain, DeterministicRng rng)
    {
        var profile = GolferProfile.Of(request.Profile);
        var beauty = profile.Artist ? Beauty(terrain).At(request.Ball.X, request.Ball.Z) : 0.0;
        var shapeSigma = plan.Shape == ShotShape.Straight ? 0.0 : ProSkills.ShapeSigmaPerLevel * request.Skill;
        return Execute(request.Ball, plan.Club, plan.DirX, plan.DirZ, plan.Force * profile.Speed(plan.Club, plan.Shape), terrain, rng, request.Skill, plan.Low,
            profile.Dispersion(plan.Shape, beauty), plan.Shape, shapeSigma, request.Hole);
    }

    /// <summary>
    /// Coup que planifie le golfeur de <paramref name="request"/>, joué sans hasard, trajectoire comprise : celui que la planification
    /// a évalué (arbres en espérance, sans perte de vitesse due au niveau). Nul sur le green (putt). Sert aux captures.
    /// </summary>
    public ShotResult? Planned(in ShotRequest request, TerrainGrid terrain)
    {
        if (terrain.SurfaceAt(request.Ball.X, request.Ball.Z) == Surface.Green)
        {
            return null;
        }

        var profile = GolferProfile.Of(request.Profile);
        var plan = Plan(request, terrain, null);
        var expectation = Trees is null ? null : new TreeCollision.Expectation();
        return Simulate(request.Ball, plan.Club, plan.DirX, plan.DirZ, plan.Force * profile.Speed(plan.Club, plan.Shape), plan.Low, plan.Shape, terrain, expectation, recordPath: true, request.Hole).Shot;
    }

    /// <summary>Carte de beauté du terrain, refaite quand le terrain, les arbres, la saison ou les sources ont changé.</summary>
    internal BeautyMap Beauty(TerrainGrid terrain)
    {
        lock (_beautyLock)
        {
            if (_beauty is null || !_beauty.StillValid(terrain, Trees, Season, BeautySources))
            {
                _beauty = BeautyMap.Build(terrain, Trees, Season, BeautySources);
            }

            return _beauty;
        }
    }

    /// <summary>
    /// Exécute un coup avec la dispersion du club (direction et vitesse tirées du RNG), élargie par le niveau de jeu
    /// et avec une balle un peu moins rapide au-delà du niveau 1. Au niveau 1, chaque facteur vaut exactement 1.
    /// <paramref name="low"/> : sortie basse, lancement à la moitié de l'angle du club comme le chip de <see cref="ShotShape.Chip"/>.
    /// <paramref name="dispersion"/> multiplie encore les écarts types (profil du golfeur). <paramref name="shape"/> : draw ou fade
    /// (<see cref="ShotShapes"/>), dont l'effet varie d'un écart type <paramref name="amountSigma"/>.
    /// </summary>
    public ShotResult Execute(Point2 ball, Club club, double dirX, double dirZ, double force, TerrainGrid terrain, DeterministicRng rng, double skill = 1.0, bool low = false,
        double dispersion = 1.0, ShotShape shape = ShotShape.Straight, double amountSigma = 0.0, Point2? pin = null)
    {
        var sigma = skill * dispersion;
        var eps = club.DirectionSigma * sigma * Gauss(rng);
        var power = 1.0 - (PowerLossPerSkill * Math.Max(0.0, skill - 1.0));
        var speed = club.Speed * force * (1.0 + (club.SpeedSigma * sigma * Gauss(rng))) * power;
        Rotate(dirX, dirZ, eps, 1.0 - (eps * eps / 2.0), out var ux, out var uz);
        var velocity = new Vec3(ux * club.LaunchCos * speed, club.LaunchSin * speed, uz * club.LaunchCos * speed);
        var backspin = 0.0;
        var curve = 0.0;
        if (shape is ShotShape.Draw or ShotShape.Fade)
        {
            var amount = amountSigma > 0.0 ? Math.Clamp(1.0 + (amountSigma * Gauss(rng)), 0.0, 2.0) : 1.0;
            (velocity, curve, backspin) = ShotShapes.Launch(club, ux, uz, speed, shape, amount);
        }
        else if (low)
        {
            (velocity, _, backspin) = ShotShapes.Launch(club, ux, uz, speed, ShotShape.Chip);
        }

        return BallSimulator.Run(terrain, ball, velocity, rolling: false, _drag, recordPath: true, trees: Trees, season: Season, rng: rng, wind: Wind, curve: curve, backspin: backspin, pin: pin).Shot;
    }

    /// <summary>
    /// Coup choisi (votre pro) : club, direction, force et trajectoire, avec le spin du club. <paramref name="dispersion"/> multiplie
    /// les écarts types du club ; sans <paramref name="rng"/>, le coup est exécuté sans hasard ni arbres (trajectoire prévue).
    /// <paramref name="amount"/> multiplie l'effet voulu (1 : exécuté comme voulu), à un écart type <paramref name="amountSigma"/> près.
    /// </summary>
    public ShotResult ExecuteShaped(Point2 ball, Club club, double dirX, double dirZ, double force, ShotShape shape, TerrainGrid terrain, DeterministicRng? rng, double dispersion = 1.0, double amount = 1.0, double amountSigma = 0.0, Point2? pin = null)
    {
        var eps = rng is null ? 0.0 : club.DirectionSigma * dispersion * Gauss(rng);
        var speed = club.Speed * force * (rng is null ? 1.0 : 1.0 + (club.SpeedSigma * dispersion * Gauss(rng)));
        if (rng is not null && amountSigma > 0.0)
        {
            amount = Math.Clamp(amount + (amountSigma * Gauss(rng)), 0.0, 2.0);
        }

        Rotate(dirX, dirZ, eps, 1.0 - (eps * eps / 2.0), out var ux, out var uz);
        var (velocity, curve, backspin) = ShotShapes.Launch(club, ux, uz, speed, shape, amount);
        return BallSimulator.Run(terrain, ball, velocity, rolling: false, _drag, recordPath: true, trees: rng is null ? null : Trees, season: Season, rng: rng, wind: Wind, curve: curve, backspin: backspin, pin: pin).Shot;
    }

    /// <summary>
    /// Putt choisi (votre pro) : vers <paramref name="target"/>, à la vitesse qui l'y amènerait sur un green plat ; la pente est au
    /// joueur de la lire. Sans <paramref name="rng"/>, sans hasard (trajectoire prévue).
    /// </summary>
    public ShotResult PuttTo(Point2 ball, Point2 target, TerrainGrid terrain, DeterministicRng? rng, double dispersion = 1.0, Point2? pin = null)
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
        return BallSimulator.Run(terrain, ball, new Vec3(dx * speed, 0, dz * speed), rolling: true, _drag, recordPath: true, pin: pin).Shot;
    }

    private static void Rotate(double ux, double uz, double sin, double cos, out double rx, out double rz)
    {
        var x = (ux * cos) - (uz * sin);
        var z = (ux * sin) + (uz * cos);
        var inv = 1.0 / Math.Sqrt((x * x) + (z * z));
        rx = x * inv;
        rz = z * inv;
    }

    /// <summary>
    /// Normale centrée réduite par Box-Muller (C21 : loi normale sans troncature), deux tirages du RNG par valeur ; le second
    /// résultat n'est pas gardé, pour que le RNG reste le seul état.
    /// </summary>
    private static double Gauss(DeterministicRng rng)
    {
        var u = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u)) * Math.Cos(2.0 * Math.PI * rng.NextDouble());
    }

    /// <summary>
    /// Coût d'un point de chute : distance au trou par la carte de jeu, pénalités et lie ; pénalités et lies difficiles pèsent
    /// <see cref="GolferProfile.Risk"/> fois.
    /// </summary>
    private static double Cost(in ShotResult r, PlayMap map, Point2 hole, GolferProfile profile)
    {
        var cost = map.DistanceAt(r.Ball.X, r.Ball.Z, hole) + (profile.Risk * PenaltyCost * r.PenaltyStrokes);
        var lie = r.Outcome switch
        {
            ShotOutcome.Green => -8.0,
            ShotOutcome.Fairway or ShotOutcome.Tee or ShotOutcome.Path => -2.0,
            ShotOutcome.Rough => 6.0,
            ShotOutcome.Bunker => 18.0,
            ShotOutcome.Trees => TreesLieCost,
            ShotOutcome.Water or ShotOutcome.OutOfBounds => 60.0,
            _ => 0.0,
        };
        return cost + (lie > 0.0 ? profile.Risk * lie : lie);
    }

    private ShotPlan Plan(in ShotRequest request, TerrainGrid terrain, DeterministicRng? rng)
    {
        var job = new PlanJob();
        Advance(job, request, terrain, int.MaxValue, rng);
        return job.Result!.Value;
    }

    /// <summary>
    /// Fait avancer la planification de <paramref name="job"/> tant qu'il reste du <paramref name="budget"/>, compté en pas de simulation de la balle (<see cref="BallSimulator.Step"/>) des coups candidats évalués (C12 lot 4 :
    /// un budget par tick, pour qu'aucun tick ne porte tout un drive). Rend vrai quand elle est finie (<see cref="PlanJob.Result"/>) et le budget restant.
    /// Les évaluations se font toujours dans le même ordre : l'état de <paramref name="job"/> suffit à reprendre, et le résultat est celui d'un seul tenant.
    /// <paramref name="rng"/> : le RNG du monde, tiré une fois la planification finie pour un éventuel choix non optimal (<see cref="SkillLevels.MistakeChance"/>) ; nul, toujours le meilleur.
    /// </summary>
    internal (bool Done, int Left) Advance(PlanJob job, ShotRequest request, TerrainGrid terrain, int budget, DeterministicRng? rng = null)
    {
        if (job.IsDone)
        {
            return (true, budget);
        }

        var profile = GolferProfile.Of(request.Profile);

        // Le prudent vise le centre du green plutôt que le drapeau, sauf sur une approche courte.
        var ball = request.Ball;
        var pin = request.Hole;
        var fx = request.Hole.X - ball.X;
        var fz = request.Hole.Z - ball.Z;
        var hole = profile.Safe && (fx * fx) + (fz * fz) > SafeAimRange * SafeAimRange ? GreenCenter(terrain, request.Hole) : request.Hole;
        var ex = hole.X - ball.X;
        var ez = hole.Z - ball.Z;
        var dist = Math.Sqrt((ex * ex) + (ez * ez));
        if (dist < 1e-9)
        {
            job.Result = new ShotPlan(Clubs.Wedge, 1.0, 0.0, Forces[^1], false, ShotShape.Straight, ball);
            job.Stage = PlanJob.Done;
            return (true, budget);
        }

        var map = _maps.Get(terrain, Trees, hole);
        var beauty = profile.Artist ? Beauty(terrain) : null;

        // Ligne du trou barrée par des bois : on vise le chemin que la balle peut suivre (le coin du fairway), pas le drapeau.
        var ux = ex / dist;
        var uz = ez / dist;
        if (map.DistanceAt(ball.X, ball.Z, hole) > DetourFactor * dist)
        {
            var way = map.Waypoint(ball, Clubs.Driver.TargetRange, hole);
            var wx = way.X - ball.X;
            var wz = way.Z - ball.Z;
            var wd = Math.Sqrt((wx * wx) + (wz * wz));
            if (wd > 1e-9)
            {
                ux = wx / wd;
                uz = wz / wd;
            }
        }

        var candidates = job.Candidates;

        // Le long frappeur préfère le driver au départ.
        var driverBonus = profile.Long && request.StrokeNumber == 0 ? GolferProfile.TeeDriverBonus : 0.0;

        void Try(Club club, double dx, double dz, double force, bool low, ShotShape shape)
        {
            var cost = Evaluate(ball, hole, pin, map, club, dx, dz, force * profile.Speed(club, shape), low, shape, terrain, profile, beauty, out var stop, out var landing, out var bonus, out var steps);
            if (ReferenceEquals(club, Clubs.Driver))
            {
                cost -= driverBonus;
            }

            budget -= steps;
            candidates.Add(new PlanCandidate(cost, stop, bonus, new ShotPlan(club, dx, dz, force, low, shape, landing)));
        }

        // Une étape de candidats ; chaque évaluation compte pour une unité du curseur, celles déjà faites sont sautées. Faux : budget épuisé.
        bool Run(IReadOnlyList<double> degrees, IReadOnlyList<double> forces, bool low, bool relaxed)
        {
            var n = 0;
            foreach (var club in Clubs.Full)
            {
                var length = profile.DistanceFactor(club);
                foreach (var force in forces)
                {
                    var range = club.TargetRange * force * force * length;
                    if (range > (1.3 * dist) + 15.0 || range < (relaxed ? WideMinRange : 0.3 * dist))
                    {
                        continue;
                    }

                    foreach (var degree in degrees)
                    {
                        var radians = degree * Math.PI / 180.0;
                        Rotate(ux, uz, Math.Sin(radians), Math.Cos(radians), out var dx, out var dz);
                        if (n++ >= job.Cursor)
                        {
                            if (budget <= 0)
                            {
                                return false;
                            }

                            Try(club, dx, dz, force, low, ShotShape.Straight);
                            job.Cursor = n;
                        }

                        // Joueur de draw ou de fade : chaque coup plein est aussi essayé avec son effet.
                        if (!low && profile.Shape != ShotShape.Straight && n++ >= job.Cursor)
                        {
                            if (budget <= 0)
                            {
                                return false;
                            }

                            Try(club, dx, dz, force, low, profile.Shape);
                            job.Cursor = n;
                        }
                    }
                }
            }

            return true;
        }

        void Enter(int stage)
        {
            job.Stage = stage;
            job.Cursor = 0;
            if (stage != 3)
            {
                return;
            }

            if (candidates.Count == 0)
            {
                job.Result = new ShotPlan(Clubs.Wedge, ux, uz, Forces[^1], false, ShotShape.Straight, ball);
                job.Stage = PlanJob.Done;
                return;
            }

            // Les meilleurs candidats sont re-testés plus bas : on les trie une fois, à l'entrée de l'étape.
            candidates.Sort(static (a, b) => a.Cost.CompareTo(b.Cost));
        }

        if (job.Stage == 0)
        {
            if (!Run(OffsetDegrees, Forces, low: false, relaxed: false))
            {
                return (false, budget);
            }

            // Ligne du trou bouchée par les arbres : on essaie aussi les coups bas (sous les branches) et les directions sur tout le tour (±180°)
            // pour ressortir de côté, avec moins de forces et de directions pour tenir le temps de planification.
            Enter(candidates.Count > 0 && Best(candidates).Stop > BlockedStop ? 1 : 3);
        }

        if (job.Stage == 1)
        {
            if (!Run(LowDegrees, WideForces, low: true, relaxed: false))
            {
                return (false, budget);
            }

            Enter(2);
        }

        if (job.Stage == 2)
        {
            if (!Run(WideDegrees, WideForces, low: false, relaxed: true))
            {
                return (false, budget);
            }

            Enter(3);
        }

        if (job.IsDone)
        {
            return (true, budget);
        }

        // Les meilleurs candidats sont re-testés avec la direction décalée de chaque côté, pour préférer un coup sûr. Le profil règle
        // l'écart de ces flancs (Flank, en écarts types du club), qui suit la dispersion de l'effet choisi mais pas celle due à la beauté.
        // La beauté (artiste) ne compte qu'à ce choix final, et seulement entre les finalistes dont le point de chute n'est pas plus loin
        // du trou (carte de jeu) que celui du choix neutre de plus de BeautyCost : il ne s'en écarte que pour un point de chute plus beau.
        var count = Math.Min(Finalists, candidates.Count);
        var perFinalist = profile.Safe ? 4 : 2;
        for (var u = job.Cursor; u < count * perFinalist; u++)
        {
            if (budget <= 0)
            {
                return (false, budget);
            }

            var c = candidates[u / perFinalist].Plan;
            var eps = c.Club.DirectionSigma * profile.Flank * profile.Dispersion(c.Shape, 0.0);
            var cos = 1.0 - (eps * eps / 2.0);
            var force = c.Force * profile.Speed(c.Club, c.Shape);
            var depth = c.Club.SpeedSigma * profile.Flank; // le prudent teste aussi le coup trop long et trop court
            var (ax, az, f) = (u % perFinalist) switch
            {
                0 => Turned(c, eps, cos, force),
                1 => Turned(c, -eps, cos, force),
                2 => (c.DirX, c.DirZ, force * (1.0 + depth)),
                _ => (c.DirX, c.DirZ, force * (1.0 - depth)),
            };
            var value = Evaluate(ball, hole, pin, map, c.Club, ax, az, f, c.Low, c.Shape, terrain, profile, beauty, out _, out _, out var bonus, out var finalSteps);
            budget -= finalSteps;
            job.Finals.Add((value, bonus));
            job.Cursor = u + 1;
        }

        var scores = Scores(job, profile);
        var best = 0;
        for (var k = 1; k < count; k++)
        {
            if (scores[k].Neutral < scores[best].Neutral)
            {
                best = k;
            }
        }

        if (beauty is not null)
        {
            var reach = map.DistanceAt(candidates[best].Plan.Landing.X, candidates[best].Plan.Landing.Z, hole) + GolferProfile.BeautyCost;
            var artist = best;
            for (var k = 0; k < count; k++)
            {
                var landing = candidates[k].Plan.Landing;
                if (scores[k].Neutral - scores[k].Beauty < scores[artist].Neutral - scores[artist].Beauty && map.DistanceAt(landing.X, landing.Z, hole) <= reach)
                {
                    artist = k;
                }
            }

            best = artist;
        }

        job.Result = candidates[rng is null ? best : Mistake(scores, best, request.Skill, rng)].Plan;
        job.Stage = PlanJob.Done;
        return (true, budget);
    }

    /// <summary>
    /// Score de chaque finaliste (les <see cref="Finalists"/> premiers candidats triés) : coût du coup visé et de ses flancs
    /// (et, pour le prudent, du trop long et du trop court), et bonus de beauté moyen (artiste).
    /// </summary>
    internal static (double Neutral, double Beauty)[] Scores(PlanJob job, GolferProfile profile)
    {
        var perFinalist = profile.Safe ? 4 : 2;
        var scores = new (double Neutral, double Beauty)[job.Finals.Count / perFinalist];
        for (var k = 0; k < scores.Length; k++)
        {
            var (cost, _, bonus, _) = job.Candidates[k];
            var (left, right) = (job.Finals[k * perFinalist], job.Finals[(k * perFinalist) + 1]);
            var score = ((2.0 * cost) + left.Cost + right.Cost) / 4.0;
            if (profile.Safe)
            {
                score = ((4.0 * score) + job.Finals[(k * perFinalist) + 2].Cost + job.Finals[(k * perFinalist) + 3].Cost) / 6.0;
            }

            scores[k] = (score, ((2.0 * bonus) + left.Bonus + right.Bonus) / 4.0);
        }

        return scores;
    }

    /// <summary>
    /// Choix non optimal (C21) : avec la probabilité <see cref="SkillLevels.MistakeChance"/> du niveau <paramref name="skill"/>, le golfeur
    /// prend le deuxième ou le troisième finaliste par score au lieu du meilleur, au hasard entre ceux qui restent à moins de
    /// <see cref="MaxMistakeCost"/> du meilleur ; sinon, ou si aucun ne l'est, le meilleur. Un tirage du RNG du monde par coup, deux s'il se trompe.
    /// </summary>
    private static int Mistake((double Neutral, double Beauty)[] scores, int best, double skill, DeterministicRng rng)
    {
        if (rng.NextDouble() >= SkillLevels.MistakeChance(skill))
        {
            return best;
        }

        int second = -1, third = -1;
        for (var k = 0; k < scores.Length; k++)
        {
            if (k == best || scores[k].Neutral - scores[best].Neutral >= MaxMistakeCost)
            {
                continue;
            }

            if (second < 0 || scores[k].Neutral < scores[second].Neutral)
            {
                (second, third) = (k, second);
            }
            else if (third < 0 || scores[k].Neutral < scores[third].Neutral)
            {
                third = k;
            }
        }

        var pick = rng.NextDouble();
        return second < 0 ? best : third < 0 || pick < 0.5 ? second : third;
    }

    /// <summary>Direction du coup <paramref name="c"/> tournée d'un angle de sinus <paramref name="sin"/> et de cosinus <paramref name="cos"/>, avec la force donnée.</summary>
    private static (double X, double Z, double Force) Turned(ShotPlan c, double sin, double cos, double force)
    {
        Rotate(c.DirX, c.DirZ, sin, cos, out var x, out var z);
        return (x, z, force);
    }

    /// <summary>Centre (moyenne des cellules) du green qui porte le drapeau ; le drapeau lui-même s'il n'est pas sur un green.</summary>
    private static Point2 GreenCenter(TerrainGrid terrain, Point2 hole)
    {
        var start = (terrain.CellXOf(hole.X), terrain.CellZOf(hole.Z));
        if (terrain.GetCellSurface(start.Item1, start.Item2) != Surface.Green)
        {
            return hole;
        }

        var seen = new HashSet<(int, int)> { start };
        var queue = new Queue<(int X, int Z)>();
        queue.Enqueue(start);
        double sx = 0.0, sz = 0.0;
        while (queue.Count > 0)
        {
            var (x, z) = queue.Dequeue();
            var c = terrain.CellCenter(x, z);
            sx += c.X;
            sz += c.Z;
            foreach (var (nx, nz) in new[] { (x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1) })
            {
                if (nx >= 0 && nz >= 0 && nx < terrain.CellsX && nz < terrain.CellsZ && terrain.GetCellSurface(nx, nz) == Surface.Green && seen.Add((nx, nz)))
                {
                    queue.Enqueue((nx, nz));
                }
            }
        }

        return new Point2(sx / seen.Count, sz / seen.Count);
    }

    private static PlanCandidate Best(List<PlanCandidate> candidates)
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
    /// qui finissent sous les arbres, pour juger la ligne du trou bouchée ; <paramref name="landing"/> : où la balle passée s'arrête ;
    /// <paramref name="bonus"/> : coût retiré par la beauté espérée du point de chute, nulle sous les arbres (artiste : 5 m × beauté, 0 sans <paramref name="beauty"/>).
    /// <paramref name="force"/> comprend déjà le facteur de vitesse du profil.
    /// </summary>
    private double Evaluate(Point2 ball, Point2 hole, Point2 pin, PlayMap map, Club club, double dirX, double dirZ, double force, bool low, ShotShape shape, TerrainGrid terrain,
        GolferProfile profile, BeautyMap? beauty, out double stop, out Point2 landing, out double bonus, out int steps)
    {
        var expectation = Trees is null ? null : new TreeCollision.Expectation();
        var run = Simulate(ball, club, dirX, dirZ, force, low, shape, terrain, expectation, false, pin);
        var shot = run.Shot;
        steps = (int)Math.Round(run.Duration / BallSimulator.Step);
        landing = shot.Ball;
        // Un coup rentré, ou déjà pénalisé (hors limites, eau), ne compte pas comme gâché.
        var wasted = shot.Holed || shot.PenaltyStrokes > 0 ? 0.0 : Wasted(ball, shot.Ball);
        var cost = Cost(shot, map, hole, profile) + wasted;
        bonus = beauty is null ? 0.0 : GolferProfile.BeautyCost * beauty.At(shot.Ball.X, shot.Ball.Z);
        var crown = expectation?.Stopped ?? 0.0;

        // Part des balles qui finissent sous les arbres : arrêtées par une couronne, ou passées mais retombées dans le bois
        // ou renvoyées à leur départ par un tronc (la ligne est bouchée tout autant).
        stop = crown + ((1.0 - crown) * (shot.Outcome == ShotOutcome.Trees || wasted > 0.0 ? 1.0 : 0.0));
        if (crown <= 0.0)
        {
            return cost;
        }

        var at = expectation!.StopPoint;
        var stopped = map.DistanceAt(at.X, at.Z, hole) + (profile.Risk * TreesLieCost) + Wasted(ball, at);
        // Arrêtée sous les arbres, la balle ne gagne rien à la beauté : l'artiste n'est pas récompensé de tirer dans les bois.
        bonus *= 1.0 - crown;

        return (crown * stopped) + ((1.0 - crown) * cost);
    }

    /// <summary>Vol et roule sans hasard d'un coup candidat ; les arbres sont vus en espérance (<paramref name="expectation"/>), sans tirage.</summary>
    private SimulationResult Simulate(Point2 ball, Club club, double dirX, double dirZ, double force, bool low, ShotShape shape, TerrainGrid terrain,
        TreeCollision.Expectation? expectation, bool recordPath, Point2 pin)
    {
        var speed = club.Speed * force;
        Vec3 velocity;
        var backspin = 0.0;
        var curve = 0.0;
        if (shape != ShotShape.Straight)
        {
            (velocity, curve, backspin) = ShotShapes.Launch(club, dirX, dirZ, speed, shape);
        }
        else if (low)
        {
            (velocity, _, backspin) = ShotShapes.Launch(club, dirX, dirZ, speed, ShotShape.Chip);
        }
        else
        {
            velocity = new Vec3(dirX * club.LaunchCos * speed, club.LaunchSin * speed, dirZ * club.LaunchCos * speed);
        }

        return BallSimulator.Run(terrain, ball, velocity, rolling: false, _drag, recordPath: recordPath, trees: Trees, season: Season, wind: Wind, curve: curve, backspin: backspin, expectation: expectation, pin: pin);
    }

    private static double Wasted(Point2 from, Point2 to) =>
        ((to.X - from.X) * (to.X - from.X)) + ((to.Z - from.Z) * (to.Z - from.Z)) < WastedRadius * WastedRadius ? PenaltyCost : 0.0;

    private ShotResult Putt(in ShotRequest request, TerrainGrid terrain, DeterministicRng rng, double dispersion)
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
            var sim = BallSimulator.Run(terrain, ball, new Vec3(aimX * speed, 0, aimZ * speed), rolling: true, _drag, recordPath: false, pin: request.Hole);
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
        var sigma = request.Skill * dispersion;
        var eps = (club.DirectionSigma + (PuttSigmaPerMeter * dist)) * sigma * Gauss(rng);
        var noisy = bestSpeed * (1.0 + (club.SpeedSigma * sigma * Gauss(rng)));
        Rotate(bestX, bestZ, eps, 1.0 - (eps * eps / 2.0), out var dx, out var dz);
        return BallSimulator.Run(terrain, ball, new Vec3(dx * noisy, 0, dz * noisy), rolling: true, _drag, recordPath: true, pin: request.Hole).Shot;
    }
}

/// <summary>Coup planifié : club, direction, force (sans le facteur de vitesse du profil), sortie basse, effet, et point de chute prévu.</summary>
internal readonly record struct ShotPlan(Club Club, double DirX, double DirZ, double Force, bool Low, ShotShape Shape, Point2 Landing);
