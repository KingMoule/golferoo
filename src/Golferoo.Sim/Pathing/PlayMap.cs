using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Pathing;

/// <summary>
/// Carte de jeu d'un drapeau : pour chaque cellule, la longueur du chemin que la balle peut suivre jusqu'au trou (Dijkstra sur
/// 16 voisins, assez fin pour que le chemin en terrain dégagé reste à 3 % de la ligne droite). Les forêts, les bosquets (cellules
/// couvertes par au moins deux couronnes d'arbres adultes debout) coûtent <see cref="BlockedFactor"/> fois leur
/// longueur : on les contourne, mais une balle qui y repose reçoit quand même une valeur. L'eau se survole : elle ne coûte rien de plus.
/// </summary>
public sealed class PlayMap
{
    /// <summary>Multiple de longueur d'une cellule de forêt, de bosquet .</summary>
    public const float BlockedFactor = 8f;

    /// <summary>Des couronnes couvrant une cellule au moins aussi nombreuses font un bosquet.</summary>
    private const int GroveCrowns = 2;

    private static readonly (int Dx, int Dz)[] Moves =
    [
        (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1),
        (2, 1), (2, -1), (-2, 1), (-2, -1), (1, 2), (1, -2), (-1, 2), (-1, -2),
    ];

    private readonly TerrainGrid _terrain;
    private readonly float[] _dist;
    private readonly int[] _parent;

    private PlayMap(TerrainGrid terrain, float[] dist, int[] parent, int holeCell, long terrainVersion, TreeStand? trees, long treesVersion)
    {
        _terrain = terrain;
        _dist = dist;
        _parent = parent;
        HoleCell = holeCell;
        TerrainVersion = terrainVersion;
        Trees = trees;
        TreesVersion = treesVersion;
    }

    /// <summary>Cellule du drapeau (ligne par ligne).</summary>
    public int HoleCell { get; }

    public long TerrainVersion { get; }

    public TreeStand? Trees { get; }

    public long TreesVersion { get; }

    public bool IsOf(TerrainGrid terrain) => ReferenceEquals(_terrain, terrain);

    public static PlayMap Build(TerrainGrid terrain, TreeStand? trees, Point2 hole)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        var nx = terrain.CellsX;
        var nz = terrain.CellsZ;
        var cost = CellCosts(terrain, trees);
        var dist = new float[nx * nz];
        Array.Fill(dist, float.PositiveInfinity);
        var parent = new int[dist.Length];
        Array.Fill(parent, -1);
        var start = (terrain.CellZOf(hole.Z) * nx) + terrain.CellXOf(hole.X);
        var cs = (float)terrain.CellSize;
        var lengths = new float[Moves.Length];
        for (var m = 0; m < Moves.Length; m++)
        {
            lengths[m] = cs * MathF.Sqrt((Moves[m].Dx * Moves[m].Dx) + (Moves[m].Dz * Moves[m].Dz));
        }

        var queue = new PriorityQueue<int, float>();
        dist[start] = 0f;
        queue.Enqueue(start, 0f);
        while (queue.TryDequeue(out var a, out var d))
        {
            if (d > dist[a])
            {
                continue;
            }

            var az = Math.DivRem(a, nx, out var ax);
            for (var m = 0; m < Moves.Length; m++)
            {
                var bx = ax + Moves[m].Dx;
                var bz = az + Moves[m].Dz;
                if ((uint)bx >= (uint)nx || (uint)bz >= (uint)nz)
                {
                    continue;
                }

                var b = (bz * nx) + bx;
                var nd = d + (lengths[m] * 0.5f * (cost[a] + cost[b]));
                if (nd < dist[b])
                {
                    dist[b] = nd;
                    parent[b] = a;
                    queue.Enqueue(b, nd);
                }
            }
        }

        return new PlayMap(terrain, dist, parent, start, terrain.SurfaceVersion, trees, trees?.Version ?? 0);
    }

    /// <summary>
    /// Longueur du chemin jusqu'au trou depuis ce point (interpolée entre les centres de cellule), jamais sous la ligne droite.
    /// </summary>
    public double DistanceAt(double x, double z, Point2 hole)
    {
        var straight = Math.Sqrt(((x - hole.X) * (x - hole.X)) + ((z - hole.Z) * (z - hole.Z)));
        var cs = _terrain.CellSize;
        var fx = (x / cs) - 0.5;
        var fz = (z / cs) - 0.5;
        var x0 = Math.Clamp((int)Math.Floor(fx), 0, _terrain.CellsX - 1);
        var z0 = Math.Clamp((int)Math.Floor(fz), 0, _terrain.CellsZ - 1);
        var x1 = Math.Min(x0 + 1, _terrain.CellsX - 1);
        var z1 = Math.Min(z0 + 1, _terrain.CellsZ - 1);
        var tx = Math.Clamp(fx - x0, 0.0, 1.0);
        var tz = Math.Clamp(fz - z0, 0.0, 1.0);
        var n = _terrain.CellsX;
        var top = (_dist[(z0 * n) + x0] * (1.0 - tx)) + (_dist[(z0 * n) + x1] * tx);
        var bottom = (_dist[(z1 * n) + x0] * (1.0 - tx)) + (_dist[(z1 * n) + x1] * tx);

        // La carte mesure jusqu'au centre de la cellule du drapeau : on retire l'écart entre ce centre et le drapeau.
        var centre = _terrain.CellCenter(HoleCell % n, HoleCell / n);
        var offset = Math.Sqrt(((centre.X - hole.X) * (centre.X - hole.X)) + ((centre.Z - hole.Z) * (centre.Z - hole.Z)));
        return Math.Max(straight, (top * (1.0 - tz)) + (bottom * tz) - offset);
    }

    /// <summary>
    /// Point du chemin le plus court, à <paramref name="lookahead"/> mètres de longueur de chemin du point donné (le drapeau si le
    /// chemin est plus court) : la direction que la balle doit prendre pour contourner les bois.
    /// </summary>
    public Point2 Waypoint(Point2 from, double lookahead, Point2 hole)
    {
        var cell = (_terrain.CellZOf(from.Z) * _terrain.CellsX) + _terrain.CellXOf(from.X);
        var walked = 0.0;
        var cs = _terrain.CellSize;
        var n = _terrain.CellsX;
        while (cell != HoleCell && _parent[cell] >= 0)
        {
            var next = _parent[cell];
            var dx = ((next % n) - (cell % n)) * cs;
            var dz = ((next / n) - (cell / n)) * cs;
            walked += Math.Sqrt((dx * dx) + (dz * dz));
            cell = next;
            if (walked >= lookahead)
            {
                return _terrain.CellCenter(cell % n, cell / n);
            }
        }

        return hole;
    }

    private static float[] CellCosts(TerrainGrid terrain, TreeStand? trees)
    {
        var nx = terrain.CellsX;
        var cost = new float[nx * terrain.CellsZ];
        var crowns = new byte[cost.Length];
        if (trees is not null)
        {
            var cs = terrain.CellSize;
            foreach (var t in trees.Trees)
            {
                if (t.State != TreeState.Standing || t.Height() < Tree.YoungMaxHeight)
                {
                    continue;
                }

                var r = t.CrownRadius();
                var x0 = Math.Max(0, (int)Math.Floor(((t.X - r) / cs) - 0.5));
                var x1 = Math.Min(nx - 1, (int)Math.Ceiling(((t.X + r) / cs) - 0.5));
                var z0 = Math.Max(0, (int)Math.Floor(((t.Z - r) / cs) - 0.5));
                var z1 = Math.Min(terrain.CellsZ - 1, (int)Math.Ceiling(((t.Z + r) / cs) - 0.5));
                for (var cz = z0; cz <= z1; cz++)
                {
                    for (var cx = x0; cx <= x1; cx++)
                    {
                        var dx = ((cx + 0.5) * cs) - t.X;
                        var dz = ((cz + 0.5) * cs) - t.Z;
                        if ((dx * dx) + (dz * dz) <= r * r && crowns[(cz * nx) + cx] < byte.MaxValue)
                        {
                            crowns[(cz * nx) + cx]++;
                        }
                    }
                }
            }
        }

        for (var cz = 0; cz < terrain.CellsZ; cz++)
        {
            for (var cx = 0; cx < nx; cx++)
            {
                var i = (cz * nx) + cx;
                var surface = terrain.GetCellSurface(cx, cz);
                cost[i] = surface == Surface.Trees || crowns[i] >= GroveCrowns ? BlockedFactor : 1f;
            }
        }

        return cost;
    }
}
