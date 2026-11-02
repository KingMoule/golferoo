using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Terrain;

/// <summary>Propriété générée : le terrain (cellules <see cref="Surface.Trees"/> peintes) et les arbres plantés dessus, remarquables compris.</summary>
public sealed record GeneratedProperty(TerrainGrid Terrain, IReadOnlyList<Tree> Trees);

/// <summary>
/// Propriété vierge d'une nouvelle partie (C24) : 1 200 × 800 m (300 × 200 cellules de 4 m) de relief doux, d'étangs et d'un ruisseau,
/// avec une clairière plate de <see cref="PropertyParams.ClearingRadius"/> mètres au centre (600 ; 400) où se pose le pavillon, des massifs d'arbres
/// (lot 2 : environ 25 % hors eau et hors zone dégagée, des arbres remarquables isolés près du centre). Aucun trou tracé.
/// Le terrain naît d'un <see cref="DeterministicRng"/> dérivé de la graine par un sel propre ; si une garantie échoue (relief, eau, surface constructible, couloirs de 400 m, arbres),
/// on retente avec le sel suivant (8 essais au plus) puis on lève une exception.
/// La météo, les arrivées et la ligue lisent la graine telle quelle. Même graine, même carte, au bit près.
/// </summary>
public static class PropertyGenerator
{
    public const double CellSize = 4.0;
    public const int CellsX = 300;
    public const int CellsZ = 200;

    /// <summary>Centre de la carte et de la clairière.</summary>
    public static readonly Point2 Center = new(CellsX * CellSize / 2, CellsZ * CellSize / 2);

    /// <summary>Repère du trou 1 (terre vierge, à l'est du pavillon), gardé sans arbre sur <see cref="SpotKeepOut"/> mètres pour que le pavillon y mène.</summary>
    private static readonly Point2 Spot = new(Center.X + 40, Center.Z);

    private const double SpotKeepOut = 12.0;
    private const int MaxAttempts = 8;
    private const ulong TerrainSalt = 0x7E44A1B5C3D2F091UL;
    private const ulong SaltStep = 0x9E3779B97F4A7C15UL;
    private const double BaseHeight = 5.0;
    private const int StreamStarts = 24;
    private const int StreamMaxSteps = 800;

    /// <summary>Un couloir de golf libre : départ (à moins de <see cref="PropertyParams.CorridorStartRadius"/> du pavillon) et cap en degrés (0 = vers +X, sens de +Z).</summary>
    internal readonly record struct Corridor(Point2 Start, int Bearing);

    /// <summary>Une propriété générée et ce qu'elle contient, pour les tests et le banc.</summary>
    internal readonly record struct Result(
        TerrainGrid Terrain, int Ponds, double StreamLength, int Fords, int Attempts, int Hills,
        IReadOnlyList<Tree> Trees, double WoodShare, double ClearedWoodShare, int Remarkables, IReadOnlyList<Corridor> Corridors, double BuildableHa)
    {
        public GeneratedProperty Property => new(Terrain, Trees);
    }

    /// <summary>Le terrain seul (les cellules d'arbres sont peintes, les arbres non : voir <see cref="CreateWithTrees"/>).</summary>
    public static TerrainGrid Create(ulong seed) => Generate(seed).Terrain;

    /// <summary>Le terrain et les arbres générés ; <see cref="InvalidOperationException"/> si aucun des 8 essais ne tient les garanties.</summary>
    public static GeneratedProperty CreateWithTrees(ulong seed) => Generate(seed).Property;

    internal static Result Generate(ulong seed, PropertyParams? parameters = null)
    {
        var p = parameters ?? PropertyParams.Parkland;
        var failures = new List<string>();
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var rng = new DeterministicRng(seed ^ (TerrainSalt + ((ulong)attempt * SaltStep)));
            if (TryBuild(rng, p, attempt + 1, out var failure) is { } result)
            {
                return result;
            }

            failures.Add(failure);
        }

        throw new InvalidOperationException($"Aucune propriété valide en {MaxAttempts} essais pour la graine {seed} : revoir les bornes de parkland.json. Échecs : {string.Join(" ; ", failures)}.");
    }

    /// <summary>Pente moyenne du sol hors eau (différences finies sur les coins de chaque cellule).</summary>
    internal static double MeanSlope(TerrainGrid t)
    {
        double sum = 0;
        var n = 0;
        var h = t.Heights;
        for (var cz = 0; cz < t.CellsZ; cz++)
        {
            for (var cx = 0; cx < t.CellsX; cx++)
            {
                if (t.GetCellSurface(cx, cz) == Surface.Water)
                {
                    continue;
                }

                sum += CellGradient(h.GetCornerHeight(cx, cz), h.GetCornerHeight(cx + 1, cz), h.GetCornerHeight(cx, cz + 1), h.GetCornerHeight(cx + 1, cz + 1));
                n++;
            }
        }

        return n == 0 ? 0 : sum / n;
    }

    private static double CellGradient(double h00, double h10, double h01, double h11)
    {
        var gx = (h10 + h11 - h00 - h01) / (2 * CellSize);
        var gz = (h01 + h11 - h00 - h10) / (2 * CellSize);
        return Math.Sqrt((gx * gx) + (gz * gz));
    }

    private sealed record Pond(Point2 Center, double Radius, double Phase1, double Phase2, double Level);

    private static Result? TryBuild(DeterministicRng rng, PropertyParams p, int attempts, out string failure)
    {
        failure = "";
        var corners = new double[(CellsX + 1) * (CellsZ + 1)];
        AddRelief(corners, rng, p);
        var hills = AddBumps(corners, rng, p);
        FlattenClearing(corners, p);

        var ponds = DigPonds(corners, rng, p);
        if (ponds is null)
        {
            failure = "pas de place pour les étangs";
            return null;
        }

        var water = new bool[CellsX * CellsZ];
        foreach (var pond in ponds)
        {
            MarkPond(water, pond, p);
        }

        var stream = FindStream(corners, water, rng, p);
        if (stream is null)
        {
            failure = "pas de ruisseau";
            return null;
        }

        var (path, fords) = stream.Value;
        CarveAndMarkStream(corners, water, path, fords, p);
        var playableFords = fords.Count(f => Distance(f, Center) <= p.PlayableRadius);
        if (playableFords < p.FordsMin)
        {
            failure = $"{playableFords} gué(s) dans la zone jouable";
            return null;
        }

        var terrain = Assemble(corners, water);
        if (MeanSlope(terrain) > p.MaxMeanSlope)
        {
            failure = "pente moyenne trop forte";
            return null;
        }

        var slopes = CellSlopes(terrain);
        var buildable = BuildableHectares(water, slopes, p);
        if (buildable < p.BuildableMinHa)
        {
            failure = $"surface constructible de {buildable:F1} ha";
            return null;
        }

        var corridors = FindCorridors(water, slopes, p);
        if (corridors is null)
        {
            failure = "couloirs de 400 m insuffisants";
            return null;
        }

        var (woodShare, clearedShare) = PaintWoodland(terrain, water, rng, p);
        var trees = PlantTrees(terrain, rng, p, out var remarkables);
        if (trees.Count > p.MaxTrees)
        {
            failure = $"{trees.Count} arbres";
            return null;
        }

        if (remarkables < p.RemarkablesMin)
        {
            failure = $"{remarkables} arbre(s) remarquable(s)";
            return null;
        }

        return new Result(terrain, ponds.Count, Length(path), playableFords, attempts, hills, trees, woodShare, clearedShare, remarkables, corridors, buildable);
    }

    // ---------- relief ----------

    private static void AddRelief(double[] corners, DeterministicRng rng, PropertyParams p)
    {
        var amplitude = Lerp(p.ReliefAmplitudeMin, p.ReliefAmplitudeMax, rng.NextDouble());
        const int octaves = 3;
        var lattices = new double[octaves][];
        var widths = new int[octaves];
        var wavelengths = new double[octaves];
        for (var o = 0; o < octaves; o++)
        {
            wavelengths[o] = p.ReliefWavelength / (1 << o);
            widths[o] = (int)(CellsX * CellSize / wavelengths[o]) + 3;
            var heights = (int)(CellsZ * CellSize / wavelengths[o]) + 3;
            lattices[o] = new double[widths[o] * heights];
            for (var i = 0; i < lattices[o].Length; i++)
            {
                lattices[o][i] = rng.NextDouble() - 0.5;
            }
        }

        double low = double.MaxValue, high = double.MinValue;
        for (var j = 0; j <= CellsZ; j++)
        {
            for (var i = 0; i <= CellsX; i++)
            {
                double n = 0, weight = 1;
                for (var o = 0; o < octaves; o++)
                {
                    n += weight * Noise(lattices[o], widths[o], i * CellSize / wavelengths[o], j * CellSize / wavelengths[o]);
                    weight *= 0.5;
                }

                corners[Index(i, j)] = n;
                (low, high) = (Math.Min(low, n), Math.Max(high, n));
            }
        }

        // L'amplitude est l'écart entre le point le plus haut et le plus bas du relief de fond.
        for (var k = 0; k < corners.Length; k++)
        {
            corners[k] = BaseHeight + (amplitude * (((corners[k] - low) / (high - low)) - 0.5));
        }
    }

    private static double Noise(double[] lattice, int width, double x, double z)
    {
        int ix = (int)x, iz = (int)z;
        double fx = Smooth(x - ix), fz = Smooth(z - iz);
        var a = Lerp(lattice[(iz * width) + ix], lattice[(iz * width) + ix + 1], fx);
        var b = Lerp(lattice[((iz + 1) * width) + ix], lattice[((iz + 1) * width) + ix + 1], fx);
        return Lerp(a, b, fz);
    }

    /// <summary>Collines (une à deux) et un creux, en gaussiennes.</summary>
    private static int AddBumps(double[] corners, DeterministicRng rng, PropertyParams p)
    {
        var hills = p.HillsMin + (int)(rng.NextDouble() * (p.HillsMax - p.HillsMin + 1));
        for (var k = 0; k < hills + 1; k++)
        {
            var sign = k < hills ? 1.0 : -1.0;
            var height = sign * Lerp(p.BumpHeightMin, p.BumpHeightMax, rng.NextDouble());
            var radius = Lerp(p.BumpRadiusMin, p.BumpRadiusMax, rng.NextDouble());
            var at = new Point2(Lerp(0.1, 0.9, rng.NextDouble()) * CellsX * CellSize, Lerp(0.1, 0.9, rng.NextDouble()) * CellsZ * CellSize);
            var sigma2 = radius * radius / 1.4;
            ForCornersWithin(at, radius * 3, (i, j, d) => corners[Index(i, j)] += height * Math.Exp(-d * d / (2 * sigma2)));
        }

        return hills;
    }

    /// <summary>Clairière du pavillon : plan bombé, raccordé au relief par une couronne lissée.</summary>
    private static void FlattenClearing(double[] corners, PropertyParams p)
    {
        double sum = 0;
        var n = 0;
        var outer = p.ClearingRadius + p.ClearingBlend;
        ForCornersWithin(Center, outer, (i, j, _) =>
        {
            sum += corners[Index(i, j)];
            n++;
        });
        var plane = sum / n;
        ForCornersWithin(Center, outer, (i, j, d) =>
        {
            var k = Index(i, j);
            corners[k] = d <= p.ClearingRadius
                ? plane + (p.ClearingDome * (1 - (d / p.ClearingRadius)))
                : Lerp(plane, corners[k], Smooth((d - p.ClearingRadius) / p.ClearingBlend));
        });
    }

    // ---------- eau ----------

    /// <summary>Creuse un à deux étangs aux points les plus bas, hors de la zone sans eau ; null si l'espace manque.</summary>
    private static List<Pond>? DigPonds(double[] corners, DeterministicRng rng, PropertyParams p)
    {
        var wanted = p.PondsMin + (int)(rng.NextDouble() * (p.PondsMax - p.PondsMin + 1));
        var radii = Enumerable.Range(0, wanted).Select(_ => Lerp(p.PondRadiusMin, p.PondRadiusMax, rng.NextDouble())).ToArray();
        var phases = Enumerable.Range(0, wanted * 2).Select(_ => rng.NextDouble() * Math.PI * 2).ToArray();

        var candidates = new List<(int I, int J)>();
        for (var j = 0; j <= CellsZ; j += 5)
        {
            for (var i = 0; i <= CellsX; i += 5)
            {
                candidates.Add((i, j));
            }
        }

        candidates.Sort((a, b) =>
        {
            var c = corners[Index(a.I, a.J)].CompareTo(corners[Index(b.I, b.J)]);
            return c != 0 ? c : Index(a.I, a.J).CompareTo(Index(b.I, b.J));
        });

        var ponds = new List<Pond>();
        foreach (var (i, j) in candidates)
        {
            if (ponds.Count == wanted)
            {
                break;
            }

            var r = radii[ponds.Count];
            var at = new Point2(i * CellSize, j * CellSize);
            var reach = (r * 1.3) + p.PondShore;
            var farFromClub = Distance(at, Center) >= p.MinWaterDistance + (r * 1.3) + CellSize;
            var inside = at.X >= reach && at.Z >= reach && at.X <= (CellsX * CellSize) - reach && at.Z <= (CellsZ * CellSize) - reach;
            if (!farFromClub || !inside || ponds.Any(o => Distance(at, o.Center) < p.PondSpacing))
            {
                continue;
            }

            var low = double.MaxValue;
            ForCornersWithin(at, r * 1.3, (ci, cj, _) => low = Math.Min(low, corners[Index(ci, cj)]));
            ponds.Add(new Pond(at, r, phases[ponds.Count * 2], phases[(ponds.Count * 2) + 1], low - p.PondDepth));
        }

        if (ponds.Count < p.PondsMin)
        {
            return null;
        }

        foreach (var pond in ponds)
        {
            ForCornersWithin(pond.Center, (pond.Radius * 1.3) + p.PondShore, (i, j, d) =>
            {
                var e = PondDistance(pond, i * CellSize, j * CellSize, d);
                if (e < pond.Radius + p.PondShore)
                {
                    var k = Index(i, j);
                    corners[k] = e <= pond.Radius ? pond.Level : Lerp(pond.Level, corners[k], Smooth((e - pond.Radius) / p.PondShore));
                }
            });
        }

        return ponds;
    }

    /// <summary>Distance au centre ramenée à un étang circulaire : le contour est déformé par deux ondes.</summary>
    private static double PondDistance(Pond pond, double x, double z, double d)
    {
        var angle = Math.Atan2(z - pond.Center.Z, x - pond.Center.X);
        return d / (1 + (0.2 * Math.Sin((2 * angle) + pond.Phase1)) + (0.1 * Math.Sin((3 * angle) + pond.Phase2)));
    }

    private static void MarkPond(bool[] water, Pond pond, PropertyParams p)
    {
        for (var cz = 0; cz < CellsZ; cz++)
        {
            for (var cx = 0; cx < CellsX; cx++)
            {
                var c = CellCenter(cx, cz);
                var d = Distance(c, pond.Center);
                if (d <= pond.Radius * 1.3 && PondDistance(pond, c.X, c.Z, d) <= pond.Radius && Distance(c, Center) >= p.MinWaterDistance)
                {
                    water[(cz * CellsX) + cx] = true;
                }
            }
        }
    }

    /// <summary>
    /// Ruisseau : descente de la plus forte pente depuis une cellule tirée au hasard, jusqu'à un étang ou au bord de la carte, en évitant la zone du pavillon.
    /// Rend le chemin (cellules) et les centres de gué, ou null si aucun départ n'aboutit.
    /// </summary>
    private static (List<(int X, int Z)> Path, List<Point2> Fords)? FindStream(double[] corners, bool[] water, DeterministicRng rng, PropertyParams p)
    {
        var avoid = p.MinWaterDistance + (2 * p.StreamRadius) + (2 * CellSize);
        for (var s = 0; s < StreamStarts; s++)
        {
            // Départ dans la zone jouable, pour que le ruisseau la traverse.
            var (angle, reach) = (rng.NextDouble() * Math.PI * 2, p.PlayableRadius * Math.Sqrt(rng.NextDouble()));
            var (x, z) = (Math.Clamp((int)((Center.X + (reach * Math.Cos(angle))) / CellSize), 1, CellsX - 2), Math.Clamp((int)((Center.Z + (reach * Math.Sin(angle))) / CellSize), 1, CellsZ - 2));
            var visited = new bool[CellsX * CellsZ];
            var path = new List<(int X, int Z)>();
            var reached = false;
            while (path.Count < StreamMaxSteps && Distance(CellCenter(x, z), Center) >= avoid)
            {
                path.Add((x, z));
                visited[(z * CellsX) + x] = true;
                if (water[(z * CellsX) + x] || x == 0 || z == 0 || x == CellsX - 1 || z == CellsZ - 1)
                {
                    reached = true;
                    break;
                }

                (int X, int Z)? best = null;
                var bestHeight = double.MaxValue;
                for (var dz = -1; dz <= 1; dz++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, nz = z + dz;
                        if ((dx == 0 && dz == 0) || nx < 0 || nz < 0 || nx >= CellsX || nz >= CellsZ || visited[(nz * CellsX) + nx])
                        {
                            continue;
                        }

                        var h = CellHeight(corners, nx, nz);
                        if (h < bestHeight)
                        {
                            (best, bestHeight) = ((nx, nz), h);
                        }
                    }
                }

                if (best is null)
                {
                    break;
                }

                (x, z) = best.Value;
            }

            if (!reached || Length(path) < p.StreamMinLength)
            {
                continue;
            }

            var fords = new List<Point2>();
            double along = 0, nextFord = p.FordSpacing / 2;
            for (var k = 1; k < path.Count - 1; k++)
            {
                along += CellSize * Math.Sqrt(((path[k].X - path[k - 1].X) * (path[k].X - path[k - 1].X)) + ((path[k].Z - path[k - 1].Z) * (path[k].Z - path[k - 1].Z)));
                if (along >= nextFord)
                {
                    fords.Add(CellCenter(path[k].X, path[k].Z));
                    nextFord += p.FordSpacing;
                }
            }

            return (path, fords);
        }

        return null;
    }

    private static double Length(List<(int X, int Z)> path)
    {
        double length = 0;
        for (var k = 1; k < path.Count; k++)
        {
            length += CellSize * Math.Sqrt(((path[k].X - path[k - 1].X) * (path[k].X - path[k - 1].X)) + ((path[k].Z - path[k - 1].Z) * (path[k].Z - path[k - 1].Z)));
        }

        return length;
    }

    /// <summary>Creuse le lit et peint l'eau du ruisseau, sauf aux gués et dans la zone sans eau du pavillon.</summary>
    private static void CarveAndMarkStream(double[] corners, bool[] water, List<(int X, int Z)> path, List<Point2> fords, PropertyParams p)
    {
        var carve = new double[corners.Length];
        foreach (var (x, z) in path)
        {
            var at = CellCenter(x, z);
            ForCornersWithin(at, p.StreamRadius + CellSize, (i, j, d) =>
            {
                var k = Index(i, j);
                carve[k] = Math.Max(carve[k], 1 - Smooth(d / (p.StreamRadius + CellSize)));
            });
            ForCellsWithin(at, p.StreamRadius, (cx, cz) =>
            {
                var c = CellCenter(cx, cz);
                if (Distance(c, Center) >= p.MinWaterDistance && !fords.Any(f => Distance(c, f) < p.FordHalfLength))
                {
                    water[(cz * CellsX) + cx] = true;
                }
            });
        }

        for (var k = 0; k < corners.Length; k++)
        {
            corners[k] -= p.StreamCarve * carve[k];
        }
    }

    // ---------- surface constructible et couloirs ----------

    /// <summary>Pente de chaque cellule (plus forte descente, 0,08 = 8 %).</summary>
    private static double[] CellSlopes(TerrainGrid t)
    {
        var slopes = new double[CellsX * CellsZ];
        var h = t.Heights;
        for (var cz = 0; cz < CellsZ; cz++)
        {
            for (var cx = 0; cx < CellsX; cx++)
            {
                slopes[(cz * CellsX) + cx] = CellGradient(h.GetCornerHeight(cx, cz), h.GetCornerHeight(cx + 1, cz), h.GetCornerHeight(cx, cz + 1), h.GetCornerHeight(cx + 1, cz + 1));
            }
        }

        return slopes;
    }

    /// <summary>Hectares d'un seul tenant autour du pavillon : cellules sans eau et de pente au plus <see cref="PropertyParams.BuildableMaxSlope"/> (les arbres se rasent).</summary>
    private static double BuildableHectares(bool[] water, double[] slopes, PropertyParams p)
    {
        bool Ok(int cx, int cz) => !water[(cz * CellsX) + cx] && slopes[(cz * CellsX) + cx] <= p.BuildableMaxSlope;
        int startX = (int)(Center.X / CellSize), startZ = (int)(Center.Z / CellSize);
        if (!Ok(startX, startZ))
        {
            return 0;
        }

        var seen = new bool[CellsX * CellsZ];
        var queue = new Queue<(int X, int Z)>();
        queue.Enqueue((startX, startZ));
        seen[(startZ * CellsX) + startX] = true;
        var count = 0;
        while (queue.Count > 0)
        {
            var (x, z) = queue.Dequeue();
            count++;
            foreach (var (nx, nz) in new[] { (x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1) })
            {
                if (nx >= 0 && nz >= 0 && nx < CellsX && nz < CellsZ && !seen[(nz * CellsX) + nx] && Ok(nx, nz))
                {
                    seen[(nz * CellsX) + nx] = true;
                    queue.Enqueue((nx, nz));
                }
            }
        }

        return count * CellSize * CellSize / 10000.0;
    }

    /// <summary>
    /// Un couloir libre par cap de 15° au plus : départ sur une grille de 20 m à moins de <see cref="PropertyParams.CorridorStartRadius"/> du pavillon,
    /// bande de <see cref="PropertyParams.CorridorLength"/> m sur <c>2 × CorridorHalfWidth</c> sans eau et sous <see cref="PropertyParams.CorridorMaxSlope"/>, dans la carte.
    /// Rend les couloirs si au moins <see cref="PropertyParams.CorridorsMin"/> caps distincts s'écartent d'au moins <see cref="PropertyParams.CorridorSpreadDegrees"/> degrés, sinon null.
    /// </summary>
    private static List<Corridor>? FindCorridors(bool[] water, double[] slopes, PropertyParams p)
    {
        const int bearingStep = 15;
        const double gridStep = 20.0;
        var corridors = new List<Corridor>();
        for (var bearing = 0; bearing < 360; bearing += bearingStep)
        {
            var found = false;
            for (var dz = -p.CorridorStartRadius; dz <= p.CorridorStartRadius && !found; dz += gridStep)
            {
                for (var dx = -p.CorridorStartRadius; dx <= p.CorridorStartRadius && !found; dx += gridStep)
                {
                    if ((dx * dx) + (dz * dz) <= p.CorridorStartRadius * p.CorridorStartRadius)
                    {
                        var start = new Point2(Center.X + dx, Center.Z + dz);
                        if (IsFreeStrip(start, bearing, water, slopes, p))
                        {
                            corridors.Add(new Corridor(start, bearing));
                            found = true;
                        }
                    }
                }
            }
        }

        if (corridors.Count < p.CorridorsMin)
        {
            return null;
        }

        var spread = 0;
        foreach (var a in corridors)
        {
            foreach (var b in corridors)
            {
                var d = Math.Abs(a.Bearing - b.Bearing);
                spread = Math.Max(spread, Math.Min(d, 360 - d));
            }
        }

        return spread >= p.CorridorSpreadDegrees ? corridors : null;
    }

    /// <summary>Bande dans la carte dont aucune cellule (centre dans le rectangle de <c>CorridorLength × 2 CorridorHalfWidth</c> partant de <paramref name="start"/>) n'est d'eau ni trop pentue.</summary>
    private static bool IsFreeStrip(Point2 start, int bearing, bool[] water, double[] slopes, PropertyParams p)
    {
        var angle = bearing * Math.PI / 180;
        double ux = Math.Cos(angle), uz = Math.Sin(angle);
        double x0 = double.MaxValue, x1 = double.MinValue, z0 = double.MaxValue, z1 = double.MinValue;
        foreach (var (along, across) in new[] { (0.0, -p.CorridorHalfWidth), (0.0, p.CorridorHalfWidth), (p.CorridorLength, -p.CorridorHalfWidth), (p.CorridorLength, p.CorridorHalfWidth) })
        {
            double x = start.X + (along * ux) - (across * uz), z = start.Z + (along * uz) + (across * ux);
            (x0, x1, z0, z1) = (Math.Min(x0, x), Math.Max(x1, x), Math.Min(z0, z), Math.Max(z1, z));
        }

        if (x0 < 0 || z0 < 0 || x1 >= CellsX * CellSize || z1 >= CellsZ * CellSize)
        {
            return false;
        }

        for (var cz = (int)(z0 / CellSize); cz <= (int)(z1 / CellSize); cz++)
        {
            for (var cx = (int)(x0 / CellSize); cx <= (int)(x1 / CellSize); cx++)
            {
                var c = CellCenter(cx, cz);
                double dx = c.X - start.X, dz = c.Z - start.Z;
                double along = (dx * ux) + (dz * uz), across = (-dx * uz) + (dz * ux);
                if (along >= 0 && along <= p.CorridorLength && Math.Abs(across) <= p.CorridorHalfWidth && (water[(cz * CellsX) + cx] || slopes[(cz * CellsX) + cx] > p.CorridorMaxSlope))
                {
                    return false;
                }
            }
        }

        return true;
    }

    // ---------- boisement ----------

    /// <summary>
    /// Peint les cellules <see cref="Surface.Trees"/> par bruit de seuil : les cellules de plus fort bruit, en nombre exact. Hors de la zone dégagée, une part tirée dans
    /// [<see cref="PropertyParams.WoodShareMin"/>, <see cref="PropertyParams.WoodShareMax"/>] des cellules sans eau ; dans la zone dégagée, <see cref="PropertyParams.ClearedWoodShare"/> des cellules sans eau,
    /// jamais dans la clairière du pavillon. Rend les parts réellement peintes.
    /// </summary>
    private static (double Wood, double Cleared) PaintWoodland(TerrainGrid terrain, bool[] water, DeterministicRng rng, PropertyParams p)
    {
        const int octaves = 2;
        var lattices = new double[octaves][];
        var widths = new int[octaves];
        var wavelengths = new double[octaves];
        for (var o = 0; o < octaves; o++)
        {
            wavelengths[o] = p.WoodWavelength / (1 << o);
            widths[o] = (int)(CellsX * CellSize / wavelengths[o]) + 3;
            lattices[o] = new double[widths[o] * ((int)(CellsZ * CellSize / wavelengths[o]) + 3)];
            for (var i = 0; i < lattices[o].Length; i++)
            {
                lattices[o][i] = rng.NextDouble();
            }
        }

        var noise = new double[CellsX * CellsZ];
        List<int> outer = [], inner = [];
        var zoneCells = 0;
        for (var cz = 0; cz < CellsZ; cz++)
        {
            for (var cx = 0; cx < CellsX; cx++)
            {
                var k = (cz * CellsX) + cx;
                if (water[k])
                {
                    continue;
                }

                var c = CellCenter(cx, cz);
                noise[k] = Noise(lattices[0], widths[0], c.X / wavelengths[0], c.Z / wavelengths[0]) + (0.5 * Noise(lattices[1], widths[1], c.X / wavelengths[1], c.Z / wavelengths[1]));
                var d = Distance(c, Center);
                if (d > p.ClearedRadius)
                {
                    outer.Add(k);
                }
                else
                {
                    zoneCells++;
                    if (d >= p.ClearingRadius + CellSize && Distance(c, Spot) >= SpotKeepOut)
                    {
                        inner.Add(k);
                    }
                }
            }
        }

        int Paint(List<int> cells, int count)
        {
            cells.Sort((a, b) => noise[b].CompareTo(noise[a]) is var c && c != 0 ? c : a.CompareTo(b));
            foreach (var k in cells.Take(count))
            {
                terrain.PaintCell(k % CellsX, k / CellsX, Surface.Trees);
            }

            return Math.Min(count, cells.Count);
        }

        var wood = Paint(outer, (int)Math.Round(Lerp(p.WoodShareMin, p.WoodShareMax, rng.NextDouble()) * outer.Count));
        var cleared = Paint(inner, (int)Math.Floor(p.ClearedWoodShare * zoneCells));
        return ((double)wood / Math.Max(1, outer.Count), (double)cleared / Math.Max(1, zoneCells));
    }

    /// <summary>
    /// Plante les massifs par <see cref="WoodlandGenerator.Fill"/> (trois essences, âges variés), puis les arbres remarquables isolés. Fill marque « remarquable » les arbres
    /// tirés au-delà de 1,5 fois l'âge adulte (9 %) : on les rajeunit d'un mois sous le seuil, les seuls remarquables sont ceux posés ici.
    /// </summary>
    private static List<Tree> PlantTrees(TerrainGrid terrain, DeterministicRng rng, PropertyParams p, out int remarkables)
    {
        var drawn = new TreeStand(CellsX * CellSize, CellsZ * CellSize, terrain);
        WoodlandGenerator.Fill(drawn, terrain, new CellRect(0, 0, CellsX - 1, CellsZ - 1), rng);
        var stand = new TreeStand(CellsX * CellSize, CellsZ * CellSize, terrain);
        foreach (var t in drawn.Trees)
        {
            var spec = TreeSpeciesTable.Get(t.Species);
            stand.Add(t.X, t.Z, t.Species, t.Remarkable ? (int)(Tree.RemarkableAgeFactor * spec.AdultAgeMonths) - 1 : t.AgeMonths);
        }

        var wanted = p.RemarkablesMin + (int)(rng.NextDouble() * (p.RemarkablesMax - p.RemarkablesMin + 1));
        var near = new List<Tree>();
        var placed = new List<Point2>();
        for (var attempt = 0; attempt < wanted * 200 && placed.Count < wanted; attempt++)
        {
            var angle = rng.NextDouble() * Math.PI * 2;
            var reach = Lerp(p.ClearingRadius + p.RemarkableIsolation, p.RemarkableMaxDistance, Math.Sqrt(rng.NextDouble()));
            var at = new Point2(Center.X + (reach * Math.Cos(angle)), Center.Z + (reach * Math.Sin(angle)));
            var pick = rng.NextDouble();
            var species = pick < 0.5 ? TreeSpeciesId.Oak : pick < 0.75 ? TreeSpeciesId.Birch : TreeSpeciesId.Fir;
            var spec = TreeSpeciesTable.Get(species);
            var age = (int)(spec.AdultAgeMonths * Lerp(p.RemarkableAgeFactorMin, p.RemarkableAgeFactorMax, rng.NextDouble()));
            if (!IsOpenGround(terrain, at, p.RemarkableIsolation) || placed.Any(o => Distance(at, o) < p.RemarkableSpacing))
            {
                continue;
            }

            var crown = Tree.CrownDiameter(spec, age) * 0.5;
            near.Clear();
            stand.Query(at.X, at.Z, crown + TreeSpeciesTable.MaxCrownRadius, near);
            if (near.Any(o => Distance(at, new Point2(o.X, o.Z)) < WoodlandGenerator.SpacingFactor * (crown + o.CrownRadius())))
            {
                continue;
            }

            stand.Add(at.X, at.Z, species, age, remarkable: true);
            placed.Add(at);
        }

        remarkables = placed.Count;
        return [.. stand.Trees];
    }

    /// <summary>Vrai si ni eau ni boisement à moins de <paramref name="margin"/> mètres de <paramref name="at"/> (et dans la carte).</summary>
    private static bool IsOpenGround(TerrainGrid terrain, Point2 at, double margin)
    {
        if (at.X < margin || at.Z < margin || at.X > (CellsX * CellSize) - margin || at.Z > (CellsZ * CellSize) - margin)
        {
            return false;
        }

        var open = true;
        ForCellsWithin(at, margin, (cx, cz) => open &= terrain.GetCellSurface(cx, cz) is not (Surface.Water or Surface.Trees));
        return open && terrain.GetCellSurface(terrain.CellXOf(at.X), terrain.CellZOf(at.Z)) is not (Surface.Water or Surface.Trees);
    }

    // ---------- assemblage et contrôles ----------

    private static TerrainGrid Assemble(double[] corners, bool[] water)
    {
        var heights = new HeightField(CellsX, CellsZ, CellSize);
        for (var j = 0; j <= CellsZ; j++)
        {
            for (var i = 0; i <= CellsX; i++)
            {
                heights.SetCornerHeight(i, j, Math.Clamp(corners[Index(i, j)], TerrainGrid.MinHeight + 1, TerrainGrid.MaxHeight - 1));
            }
        }

        // Pas de trou tracé : le repère du trou 1 n'est qu'un point d'appui dans la clairière, hors de l'emprise du pavillon (terre vierge).
        var terrain = new TerrainGrid(heights, "Propriété", Spot, Spot, Spot) { Blank = true };
        for (var cz = 0; cz < CellsZ; cz++)
        {
            for (var cx = 0; cx < CellsX; cx++)
            {
                if (water[(cz * CellsX) + cx])
                {
                    terrain.PaintCell(cx, cz, Surface.Water);
                }
            }
        }

        return terrain;
    }

    // ---------- outils ----------

    private static int Index(int i, int j) => (j * (CellsX + 1)) + i;

    private static Point2 CellCenter(int cx, int cz) => new((cx + 0.5) * CellSize, (cz + 0.5) * CellSize);

    private static double CellHeight(double[] corners, int cx, int cz) =>
        (corners[Index(cx, cz)] + corners[Index(cx + 1, cz)] + corners[Index(cx, cz + 1)] + corners[Index(cx + 1, cz + 1)]) / 4;

    private static double Distance(Point2 a, Point2 b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);

    private static double Smooth(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - (2 * t));
    }

    private static void ForCornersWithin(Point2 at, double radius, Action<int, int, double> visit)
    {
        int i0 = Math.Max(0, (int)Math.Floor((at.X - radius) / CellSize)), i1 = Math.Min(CellsX, (int)Math.Ceiling((at.X + radius) / CellSize));
        int j0 = Math.Max(0, (int)Math.Floor((at.Z - radius) / CellSize)), j1 = Math.Min(CellsZ, (int)Math.Ceiling((at.Z + radius) / CellSize));
        for (var j = j0; j <= j1; j++)
        {
            for (var i = i0; i <= i1; i++)
            {
                var d = Distance(new Point2(i * CellSize, j * CellSize), at);
                if (d <= radius)
                {
                    visit(i, j, d);
                }
            }
        }
    }

    private static void ForCellsWithin(Point2 at, double radius, Action<int, int> visit)
    {
        int x0 = Math.Max(0, (int)Math.Floor((at.X - radius) / CellSize)), x1 = Math.Min(CellsX - 1, (int)Math.Ceiling((at.X + radius) / CellSize));
        int z0 = Math.Max(0, (int)Math.Floor((at.Z - radius) / CellSize)), z1 = Math.Min(CellsZ - 1, (int)Math.Ceiling((at.Z + radius) / CellSize));
        for (var cz = z0; cz <= z1; cz++)
        {
            for (var cx = x0; cx <= x1; cx++)
            {
                if (Distance(CellCenter(cx, cz), at) <= radius)
                {
                    visit(cx, cz);
                }
            }
        }
    }
}
