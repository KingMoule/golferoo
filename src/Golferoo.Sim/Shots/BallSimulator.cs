using Golferoo.Sim.Pathing;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Shots;

/// <summary>Résultat d'une simulation de balle, avec les mesures utiles aux tests et au planificateur.</summary>
/// <param name="Shot">Le résultat du coup (position finale, pénalités, issue, trajectoire si demandée).</param>
/// <param name="MaxHeight">Hauteur maximale atteinte au-dessus du point de départ, en mètres.</param>
/// <param name="FirstLanding">Premier contact avec le sol (le départ si la balle ne quitte pas le sol).</param>
/// <param name="Landed">Vrai si la balle a touché le sol après un vol.</param>
/// <param name="Duration">Durée simulée, en secondes.</param>
/// <param name="ClosestApproach">Point du roulement le plus proche du trou (le départ si la balle ne roule pas).</param>
public readonly record struct SimulationResult(
    ShotResult Shot,
    double MaxHeight,
    Point2 FirstLanding,
    bool Landed,
    double Duration,
    Point2 ClosestApproach);

/// <summary>
/// Physique de la balle sur <see cref="HeightField.Sample"/> : vol (gravité, traînée quadratique relative à l'air), rebond sur la normale
/// du terrain avec la restitution et le frottement de la surface, puis roulement (pente moins résistance) jusqu'à l'arrêt.
/// Pas fixe de 1/200 s, Euler semi-implicite ; la boucle n'emploie que + − × ÷ et √ pour rester déterministe.
/// </summary>
public static class BallSimulator
{
    public const double Gravity = 9.81;

    /// <summary>Pas d'intégration, en secondes.</summary>
    public const double Step = 1.0 / 200.0;

    /// <summary>Pas d'intégration par tick de simulation (20 Hz).</summary>
    public const int StepsPerTick = 10;

    /// <summary>Coefficient de traînée quadratique (1/m) : a = −k·|v − w|·(v − w), w étant le vent. Un seul coefficient, sans effet.</summary>
    public const double DefaultDrag = 0.0009;

    /// <summary>
    /// Renfort du vent dans l'axe du coup (face, dos). Le modèle n'a ni portance ni effet, et sa traînée est réglée bas pour les
    /// portées des clubs : sans renfort, 10 m/s de face ne raccourcissent un drive que de 4 %. À 3, on retrouve à peu près les
    /// ordres de grandeur réels (décision de Guillaume du 2026-10-25).
    /// </summary>
    public const double WindGainAlong = 3.0;

    /// <summary>Renfort du vent de travers, plus fort : l'effet de la balle, absent du modèle, amplifie la dérive (Guillaume, 2026-10-25).</summary>
    public const double WindGainAcross = 6.0;

    /// <summary>Rayon du trou, en mètres.</summary>
    public const double HoleRadius = 0.054;

    /// <summary>Vitesse horizontale maximale à laquelle le trou capture la balle, en m/s.</summary>
    public const double HoleMaxSpeed = 1.6;

    /// <summary>Hauteur sous laquelle un bosquet arrête la balle, en mètres.</summary>
    public const double TreeHeight = 8.0;

    /// <summary>Part de la vitesse horizontale qui survit au choc avec un bosquet.</summary>
    private const double TreeDamping = 0.05;

    /// <summary>Vitesse normale de rebond sous laquelle la balle se met à rouler, en m/s.</summary>
    private const double RollThreshold = 0.6;

    private const double MaxSeconds = 60.0;

    private enum End
    {
        Rest,
        Holed,
        Water,
        OutOfBounds,
    }

    /// <summary>
    /// Simule un coup depuis le sol au point <paramref name="start"/>.
    /// </summary>
    /// <param name="rolling">Vrai pour un putt : la balle roule dès le départ (la composante verticale est ignorée).</param>
    /// <param name="startHeight">Hauteur de départ au-dessus du sol, en mètres (0 : la balle est posée).</param>
    /// <param name="trees">Arbres que la balle en vol rencontre (nul : aucun, la planification les ignore). Exige <paramref name="rng"/>.</param>
    /// <param name="season">Saison, qui règle la densité des couronnes.</param>
    /// <param name="rng">Source du hasard des couronnes : un tirage par pas qui traverse une couronne, aucun sinon.</param>
    /// <param name="recordPath">Vrai pour échantillonner la trajectoire au tick de simulation.</param>
    /// <param name="wind">Vent en vol, en m/s (horizontal) ; sans effet sur le roulement.</param>
    /// <param name="curve">Effet latéral : vitesse de rotation de la vitesse horizontale en vol, en rad/s (positif : draw, vers la gauche d'un droitier ; 0 : aucun).</param>
    /// <param name="backspin">Part de la vitesse horizontale perdue au premier rebond (0 : aucune).</param>
    public static SimulationResult Run(TerrainGrid terrain, Point2 start, Vec3 velocity, bool rolling, double drag, bool recordPath, double startHeight = 0.0, TreeStand? trees = null, Season season = Season.Summer, DeterministicRng? rng = null, Vec3 wind = default, double curve = 0.0, double backspin = 0.0)
    {
        if (trees is not null && rng is null)
        {
            throw new ArgumentNullException(nameof(rng), "Des arbres exigent le RNG du coup.");
        }

        var width = terrain.CellsX * terrain.CellSize;
        var depth = terrain.CellsZ * terrain.CellSize;
        var holeR2 = HoleRadius * HoleRadius;
        var x = start.X;
        var z = start.Z;
        var y = terrain.Sample(x, z) + startHeight;
        var y0 = y;
        var vx = velocity.X;
        var vy = velocity.Y;
        var vz = velocity.Z;
        List<Vec3>? path = recordPath ? [new Vec3(x, y, z)] : null;
        var maxHeight = 0.0;
        var landed = false;
        var firstLanding = start;
        var treeHit = false;
        var crownDone = false;
        var probe = trees is null ? null : new TreeCollision.Probe();
        var gateCell = -1;
        var gateTop = double.NegativeInfinity;
        var closest = start;
        var closestD2 = Dist2(x, z, terrain.Hole);
        var end = End.Rest;
        var maxSteps = (int)(MaxSeconds / Step);
        // Vent renforcé, décomposé une fois pour toutes selon l'axe horizontal du coup au départ (face ou dos, et travers).
        var hv2 = (vx * vx) + (vz * vz);
        var p = hv2 > 1e-12 ? ((wind.X * vx) + (wind.Z * vz)) * (WindGainAcross - WindGainAlong) / hv2 : 0.0;
        var airX = (WindGainAcross * wind.X) - (p * vx);
        var airZ = (WindGainAcross * wind.Z) - (p * vz);
        var steps = 0;

        for (; steps < maxSteps; steps++)
        {
            if (!rolling)
            {
                // Vitesse relative à l'air. Sans vent, rx = vx au bit près.
                var rx = vx - airX;
                var rz = vz - airZ;
                var speed = Math.Sqrt((rx * rx) + (vy * vy) + (rz * rz));
                var k = drag * speed;
                vx -= k * rx * Step;
                vy -= (Gravity + (k * vy)) * Step;
                vz -= k * rz * Step;
                if (curve != 0.0)
                {
                    // a = curve × (vz, 0, −vx) : la vitesse horizontale tourne, vers la gauche pour un draw.
                    var cx = vx;
                    vx += curve * vz * Step;
                    vz -= curve * cx * Step;
                }

                var px = x;
                var py = y;
                var pz = z;
                x += vx * Step;
                y += vy * Step;
                z += vz * Step;
                if (y - y0 > maxHeight)
                {
                    maxHeight = y - y0;
                }

                if (x < 0 || x >= width || z < 0 || z >= depth)
                {
                    end = End.OutOfBounds;
                    steps++;
                    break;
                }

                var h = terrain.Sample(x, z);
                // L'index donne l'altitude absolue de la cime la plus haute autour de la balle (sol au pied + hauteur),
                // relue seulement quand la balle change de case : à chaque pas, elle coûtait la moitié d'un vol sans arbres.
                if (trees is not null && trees.CellOf(x, z) is var cell && cell != gateCell)
                {
                    gateCell = cell;
                    gateTop = trees.MaxTopAround(cell, TreeCollision.Margin);
                }

                if (trees is not null && y < gateTop)
                {
                    TreeCollision.Step(terrain, trees, season, rng!, probe!, px, py, pz, ref x, ref y, ref z, ref vx, ref vy, ref vz, ref crownDone);
                    h = terrain.Sample(x, z);
                }

                if (!treeHit && y - h < TreeHeight && terrain.SurfaceAt(x, z) == Surface.Trees)
                {
                    treeHit = true;
                    vx *= TreeDamping;
                    vz *= TreeDamping;
                    if (vy > 0)
                    {
                        vy = 0;
                    }
                }

                if (y <= h)
                {
                    // Point de contact : interpolation linéaire entre les deux pas.
                    var d0 = py - terrain.Sample(px, pz);
                    var d1 = y - h;
                    var f = d0 > 0 && d0 - d1 > 0 ? d0 / (d0 - d1) : 1.0;
                    x = px + ((x - px) * f);
                    z = pz + ((z - pz) * f);
                    h = terrain.Sample(x, z);
                    y = h;
                    crownDone = false;
                    var firstContact = !landed;
                    if (!landed)
                    {
                        landed = true;
                        firstLanding = new Point2(x, z);
                    }

                    var surface = terrain.SurfaceAt(x, z);
                    if (surface == Surface.Water)
                    {
                        end = End.Water;
                        steps++;
                        break;
                    }

                    if (Dist2(x, z, terrain.Hole) < holeR2 && Math.Sqrt((vx * vx) + (vz * vz)) < HoleMaxSpeed)
                    {
                        end = End.Holed;
                        steps++;
                        break;
                    }

                    var n = terrain.SampleNormal(x, z);
                    var vn = (vx * n.X) + (vy * n.Y) + (vz * n.Z);
                    if (vn < 0)
                    {
                        var keep = 1.0 - terrain.Soil.FrictionAt(x, z);
                        var bounce = -vn * terrain.Soil.RestitutionAt(x, z);
                        var tx = (vx - (vn * n.X)) * keep;
                        var ty = (vy - (vn * n.Y)) * keep;
                        var tz = (vz - (vn * n.Z)) * keep;
                        if (firstContact && backspin != 0.0)
                        {
                            tx *= 1.0 - backspin;
                            tz *= 1.0 - backspin;
                        }

                        if (bounce < RollThreshold)
                        {
                            rolling = true;
                            vx = tx;
                            vz = tz;
                            vy = 0;
                        }
                        else
                        {
                            vx = tx + (n.X * bounce);
                            vy = ty + (n.Y * bounce);
                            vz = tz + (n.Z * bounce);
                        }
                    }
                }
            }
            else
            {
                var n = terrain.SampleNormal(x, z);
                var r = terrain.Soil.RollResistanceAt(x, z) * Gravity;
                var sx = Gravity * n.Y * n.X;
                var sz = Gravity * n.Y * n.Z;
                var speed = Math.Sqrt((vx * vx) + (vz * vz));
                if (speed < 1e-9)
                {
                    var slope = Math.Sqrt((sx * sx) + (sz * sz));
                    if (slope <= r)
                    {
                        break;
                    }

                    var push = (slope - r) / slope * Step;
                    vx = sx * push;
                    vz = sz * push;
                }
                else
                {
                    var nvx = vx + ((sx - (r * vx / speed)) * Step);
                    var nvz = vz + ((sz - (r * vz / speed)) * Step);
                    if ((nvx * vx) + (nvz * vz) <= 0)
                    {
                        // La résistance a arrêté la balle : l'arrêt définitif se décide au pas suivant (pente contre résistance).
                        nvx = 0;
                        nvz = 0;
                    }

                    vx = nvx;
                    vz = nvz;
                }

                x += vx * Step;
                z += vz * Step;
                if (x < 0 || x >= width || z < 0 || z >= depth)
                {
                    end = End.OutOfBounds;
                    steps++;
                    break;
                }

                var surface = terrain.SurfaceAt(x, z);
                if (surface == Surface.Water)
                {
                    end = End.Water;
                    steps++;
                    break;
                }

                var d2 = Dist2(x, z, terrain.Hole);
                if (d2 < closestD2)
                {
                    closestD2 = d2;
                    closest = new Point2(x, z);
                }

                if (d2 < holeR2 && Math.Sqrt((vx * vx) + (vz * vz)) < HoleMaxSpeed)
                {
                    end = End.Holed;
                    steps++;
                    break;
                }

                if (surface == Surface.Trees)
                {
                    steps++;
                    break;
                }
            }

            if (path is not null && (steps + 1) % StepsPerTick == 0)
            {
                if (rolling)
                {
                    // Au roulement la hauteur ne sert qu'à l'échantillon : la balle suit le sol.
                    y = terrain.Sample(x, z);
                }

                path.Add(new Vec3(x, y, z));
            }
        }

        var shot = Finish(terrain, start, x, z, end, path);
        return new SimulationResult(shot, maxHeight, firstLanding, landed, steps * Step, closest);
    }

    private static ShotResult Finish(TerrainGrid terrain, Point2 start, double x, double z, End end, List<Vec3>? path)
    {
        Point2 ball;
        var penalty = 0;
        ShotOutcome outcome;
        var holed = false;
        switch (end)
        {
            case End.Holed:
                ball = terrain.Hole;
                holed = true;
                outcome = ShotOutcome.Holed;
                break;
            case End.Water:
                ball = DropBeforeWater(terrain, new Point2(x, z), start);
                penalty = 1;
                outcome = ShotOutcome.Water;
                break;
            case End.OutOfBounds:
                ball = start;
                penalty = 1;
                outcome = ShotOutcome.OutOfBounds;
                break;
            default:
                if (Dist2(x, z, terrain.Hole) < HoleRadius * HoleRadius)
                {
                    ball = terrain.Hole;
                    holed = true;
                    outcome = ShotOutcome.Holed;
                    break;
                }

                var surface = terrain.SurfaceAt(x, z);
                var cx = terrain.CellXOf(x);
                var cz = terrain.CellZOf(z);
                if (!terrain.IsWalkable(cx, cz))
                {
                    ball = GridQueries.NearestWalkablePoint(terrain, new Point2(x, z));
                    outcome = ShotOutcome.Trees;
                }
                else
                {
                    // Flaque (« casual water ») : dégagement sans pénalité au sec, pas plus près du trou.
                    ball = terrain.Puddles.Relief(new Point2(x, z), terrain.Hole);
                    outcome = OutcomeOf(terrain.SurfaceAt(ball.X, ball.Z));
                }

                break;
        }

        IReadOnlyList<Vec3>? trajectory = null;
        if (path is not null)
        {
            // Dernier échantillon : la balle au repos (après drop, retour ou entrée dans le trou).
            path.Add(new Vec3(ball.X, terrain.Sample(ball.X, ball.Z), ball.Z));
            trajectory = path;
        }

        return new ShotResult(ball, holed, penalty, outcome, trajectory);
    }

    private static ShotOutcome OutcomeOf(Surface surface) => surface switch
    {
        Surface.Tee => ShotOutcome.Tee,
        Surface.Green => ShotOutcome.Green,
        Surface.Rough => ShotOutcome.Rough,
        Surface.Bunker => ShotOutcome.Bunker,
        Surface.Path => ShotOutcome.Path,
        _ => ShotOutcome.Fairway,
    };

    /// <summary>
    /// Drop : depuis le point d'entrée dans l'eau, on remonte vers le point de départ du coup (côté tee) jusqu'à la première
    /// cellule franchissable qui n'est pas de l'eau.
    /// </summary>
    private static Point2 DropBeforeWater(TerrainGrid terrain, Point2 entry, Point2 start)
    {
        var dx = start.X - entry.X;
        var dz = start.Z - entry.Z;
        var len = Math.Sqrt((dx * dx) + (dz * dz));
        if (len < 1e-9)
        {
            return start;
        }

        var stepLen = terrain.CellSize * 0.25;
        var n = (int)(len / stepLen);
        for (var i = 1; i <= n; i++)
        {
            var t = i * stepLen / len;
            var px = entry.X + (dx * t);
            var pz = entry.Z + (dz * t);
            var cx = terrain.CellXOf(px);
            var cz = terrain.CellZOf(pz);
            if (terrain.IsWalkable(cx, cz) && terrain.GetCellSurface(cx, cz) != Surface.Water)
            {
                return new Point2(px, pz);
            }
        }

        return start;
    }

    private static double Dist2(double x, double z, Point2 p)
    {
        var dx = x - p.X;
        var dz = z - p.Z;
        return (dx * dx) + (dz * dz);
    }
}
