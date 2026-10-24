using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Pathing;

/// <summary>
/// Champ de flux vers une cellule de destination : distance pondérée de chaque cellule à la destination (Dijkstra, 8 voisins)
/// et direction du pas suivant. Un pas en diagonale est refusé si l'un des deux voisins orthogonaux est infranchissable.
/// </summary>
public sealed class FlowField
{
    // Directions : 0 E, 1 SE, 2 S, 3 SO, 4 O, 5 NO, 6 N, 7 NE. L'opposée de k est (k + 4) & 7.
    private static readonly int[] Dx = [1, 1, 0, -1, -1, -1, 0, 1];
    private static readonly int[] Dz = [0, 1, 1, 1, 0, -1, -1, -1];
    private static readonly double[] UnitX = [1, Math.Sqrt(0.5), 0, -Math.Sqrt(0.5), -1, -Math.Sqrt(0.5), 0, Math.Sqrt(0.5)];
    private static readonly double[] UnitZ = [0, Math.Sqrt(0.5), 1, Math.Sqrt(0.5), 0, -Math.Sqrt(0.5), -1, -Math.Sqrt(0.5)];

    [ThreadStatic]
    private static BuildScratch? scratch;

    private readonly float[] _dist;
    private readonly sbyte[] _next;

    private FlowField(int cellsX, int cellsZ, double cellSize, int destX, int destZ, long version, float[] dist, sbyte[] next)
    {
        CellsX = cellsX;
        CellsZ = cellsZ;
        CellSize = cellSize;
        DestX = destX;
        DestZ = destZ;
        TerrainVersion = version;
        _dist = dist;
        _next = next;
    }

    public int CellsX { get; }

    public int CellsZ { get; }

    public double CellSize { get; }

    /// <summary>Cellule de destination effective (franchissable).</summary>
    public int DestX { get; }

    public int DestZ { get; }

    /// <summary>Version du terrain au moment du calcul.</summary>
    public long TerrainVersion { get; }

    /// <summary>
    /// Calcule le champ vers la cellule (destX, destZ). Si elle est infranchissable, on prend la cellule franchissable la plus proche.
    /// </summary>
    public static FlowField Build(TerrainGrid terrain, int destX, int destZ) =>
        Build(terrain, BuildCosts(terrain), destX, destZ);

    /// <summary>Coût de traversée de chaque cellule (infini si infranchissable), dans l'ordre ligne par ligne.</summary>
    internal static float[] BuildCosts(TerrainGrid terrain)
    {
        var surfaces = terrain.Surfaces;
        var costs = new float[surfaces.Length];
        Span<float> byType = stackalloc float[SurfaceTable.Count];
        for (var s = 0; s < byType.Length; s++)
        {
            var props = SurfaceTable.Get((Surface)s);
            byType[s] = props.Walkable ? (float)props.WalkCost : float.PositiveInfinity;
        }

        for (var i = 0; i < costs.Length; i++)
        {
            costs[i] = byType[(int)surfaces[i]];
        }

        return costs;
    }

    internal static FlowField Build(TerrainGrid terrain, float[] costs, int destX, int destZ)
    {
        if (!GridQueries.TryNearestWalkable(terrain, destX, destZ, out var dx, out var dz))
        {
            throw new InvalidOperationException("Le terrain n'a aucune cellule franchissable.");
        }

        var nx = terrain.CellsX;
        var nz = terrain.CellsZ;
        var count = nx * nz;
        var dist = new float[count];
        Array.Fill(dist, float.PositiveInfinity);
        var next = new sbyte[count];
        Array.Fill(next, (sbyte)-1);

        var orth = (float)terrain.CellSize;
        var diag = (float)(terrain.CellSize * Math.Sqrt(2.0));

        // File à seaux (Dial) : la largeur d'un seau reste sous la plus petite arête, donc deux cellules d'un même seau
        // ne s'améliorent jamais l'une l'autre et l'ordre à l'intérieur du seau est sans effet : le résultat est celui de Dijkstra.
        var minCost = float.PositiveInfinity;
        var maxCost = 0f;
        foreach (var c in costs)
        {
            if (!float.IsPositiveInfinity(c))
            {
                minCost = Math.Min(minCost, c);
                maxCost = Math.Max(maxCost, c);
            }
        }

        var width = orth * minCost * 0.99f;
        var invWidth = 1f / width;
        var bucketCount = (int)Math.Ceiling(diag * maxCost / width) + 2;
        var work = scratch ??= new BuildScratch();
        var buckets = work.Buckets(bucketCount);
        var done = work.Done(count);
        var start = (dz * nx) + dx;
        dist[start] = 0f;
        buckets[0].Add(start);
        var pending = 1;
        for (var k = 0; pending > 0; k++)
        {
            var bucket = buckets[k % bucketCount];
            for (var n = 0; n < bucket.Count; n++)
            {
                pending--;
                var a = bucket[n];
                if (done[a])
                {
                    continue;
                }

                done[a] = true;
                var d = dist[a];
                var az = Math.DivRem(a, nx, out var ax);
                var ca = costs[a];
                for (var dir = 0; dir < 8; dir++)
                {
                    var bx = ax + Dx[dir];
                    var bz = az + Dz[dir];
                    if (bx < 0 || bx >= nx || bz < 0 || bz >= nz)
                    {
                        continue;
                    }

                    var b = (bz * nx) + bx;
                    var cb = costs[b];
                    if (float.IsPositiveInfinity(cb) || done[b])
                    {
                        continue;
                    }

                    float len;
                    if ((dir & 1) == 0)
                    {
                        len = orth;
                    }
                    else
                    {
                        if (float.IsPositiveInfinity(costs[(az * nx) + bx]) || float.IsPositiveInfinity(costs[(bz * nx) + ax]))
                        {
                            continue; // pas de coupe de coin bloqué
                        }

                        len = diag;
                    }

                    var nd = d + (len * 0.5f * (ca + cb));
                    if (nd < dist[b])
                    {
                        dist[b] = nd;
                        next[b] = (sbyte)((dir + 4) & 7);
                        var slot = Math.Max((int)(nd * invWidth), k + 1) % bucketCount;
                        buckets[slot].Add(b);
                        pending++;
                    }
                }
            }

            bucket.Clear();
        }

        return new FlowField(nx, nz, terrain.CellSize, dx, dz, terrain.Version, dist, next);
    }

    /// <summary>Distance pondérée de la cellule à la destination ; infinie si inatteignable ou hors grille.</summary>
    public float DistanceAt(int cx, int cz) =>
        cx < 0 || cx >= CellsX || cz < 0 || cz >= CellsZ ? float.PositiveInfinity : _dist[(cz * CellsX) + cx];

    public bool IsReachable(int cx, int cz) => !float.IsPositiveInfinity(DistanceAt(cx, cz));

    /// <summary>Cellule voisine du pas suivant ; faux sur la destination et sur les cellules inatteignables.</summary>
    public bool TryNextCell(int cx, int cz, out int nx, out int nz)
    {
        nx = cx;
        nz = cz;
        if (cx < 0 || cx >= CellsX || cz < 0 || cz >= CellsZ)
        {
            return false;
        }

        var code = _next[(cz * CellsX) + cx];
        if (code < 0)
        {
            return false;
        }

        nx = cx + Dx[code];
        nz = cz + Dz[code];
        return true;
    }

    /// <summary>Direction unitaire du pas suivant depuis le centre de la cellule, sans lissage.</summary>
    public bool TryCellDirection(int cx, int cz, out double dirX, out double dirZ)
    {
        dirX = 0;
        dirZ = 0;
        if (cx < 0 || cx >= CellsX || cz < 0 || cz >= CellsZ)
        {
            return false;
        }

        var code = _next[(cz * CellsX) + cx];
        if (code < 0)
        {
            return false;
        }

        dirX = UnitX[code];
        dirZ = UnitZ[code];
        return true;
    }

    /// <summary>
    /// Direction lissée au point (x, z) : interpolation bilinéaire des directions des quatre centres de cellule voisins,
    /// ce qui évite le zigzag de cellule en cellule. Faux si aucune cellule voisine n'a de direction.
    /// </summary>
    public bool TryDirection(double x, double z, out double dirX, out double dirZ)
    {
        var u = (x / CellSize) - 0.5;
        var v = (z / CellSize) - 0.5;
        var i0 = (int)Math.Floor(u);
        var j0 = (int)Math.Floor(v);
        var fu = u - i0;
        var fv = v - j0;
        double sx = 0, sz = 0;
        for (var b = 0; b < 2; b++)
        {
            var cz = j0 + b;
            if (cz < 0 || cz >= CellsZ)
            {
                continue;
            }

            for (var a = 0; a < 2; a++)
            {
                var cx = i0 + a;
                if (cx < 0 || cx >= CellsX)
                {
                    continue;
                }

                var code = _next[(cz * CellsX) + cx];
                if (code < 0)
                {
                    continue;
                }

                var w = (a == 0 ? 1 - fu : fu) * (b == 0 ? 1 - fv : fv);
                sx += w * UnitX[code];
                sz += w * UnitZ[code];
            }
        }

        var len = Math.Sqrt((sx * sx) + (sz * sz));
        if (len < 1e-6)
        {
            dirX = 0;
            dirZ = 0;
            return false;
        }

        dirX = sx / len;
        dirZ = sz / len;
        return true;
    }

    /// <summary>Tampons réutilisés d'un calcul à l'autre sur un même thread : évite des allocations et des relances du GC.</summary>
    private sealed class BuildScratch
    {
        private List<int>[] _buckets = [];
        private bool[] _done = [];

        public List<int>[] Buckets(int count)
        {
            if (_buckets.Length < count)
            {
                var old = _buckets.Length;
                Array.Resize(ref _buckets, count);
                for (var i = old; i < count; i++)
                {
                    _buckets[i] = [];
                }
            }

            return _buckets; // les seaux sont vidés en fin de calcul
        }

        public bool[] Done(int count)
        {
            if (_done.Length < count)
            {
                _done = new bool[count];
            }
            else
            {
                Array.Clear(_done, 0, count);
            }

            return _done;
        }
    }
}
