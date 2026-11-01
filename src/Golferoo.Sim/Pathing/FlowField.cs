using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Pathing;

/// <summary>
/// Champ de flux vers une cellule de destination : distance pondérée de chaque cellule à la destination (Dijkstra, 8 voisins)
/// et direction du pas suivant. Un pas en diagonale est refusé si l'un des deux voisins orthogonaux est infranchissable.
/// Un champ borné (<see cref="Origin"/> donné) s'arrête à <see cref="Bound"/> : au-delà, les cellules ne sont pas couvertes et se lisent comme inatteignables.
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

    /// <summary>Un champ borné couvre les cellules à au plus <c>BoundFactor</c> fois la distance de son origine, plus <see cref="BoundMarginCells"/> pas en diagonale au coût de l'origine.</summary>
    internal const float BoundFactor = 1.5f;

    internal const int BoundMarginCells = 4;

    private FlowField(int cellsX, int cellsZ, double cellSize, int destX, int destZ, long version, float[] dist, sbyte[] next, int origin, float bound)
    {
        Origin = origin;
        Bound = bound;
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

    /// <summary>Version des surfaces (<see cref="TerrainGrid.SurfaceVersion"/>) au moment du calcul.</summary>
    public long TerrainVersion { get; }

    /// <summary>Clé de la cellule du demandeur qui borne le champ ; -1 pour un champ complet demandé comme tel.</summary>
    public int Origin { get; }

    /// <summary>Distance au-delà de laquelle les cellules ne sont pas couvertes ; infinie si le champ est complet (sans origine, ou origine inatteignable).</summary>
    public float Bound { get; }

    public bool IsComplete => float.IsPositiveInfinity(Bound);

    /// <summary>
    /// Calcule le champ vers la cellule (destX, destZ). Si elle est infranchissable, on prend la cellule franchissable la plus proche.
    /// </summary>
    public static FlowField Build(TerrainGrid terrain, int destX, int destZ) =>
        Build(terrain, BuildCosts(terrain), destX, destZ);

    /// <summary>Coût de traversée de chaque cellule (infini si infranchissable), dans l'ordre ligne par ligne.</summary>
    internal static float[] BuildCosts(TerrainGrid terrain, IReadOnlyList<CellRect>? obstacles = null)
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

        // Les emprises de bâtiments bloquent le passage comme une surface infranchissable.
        foreach (var rect in obstacles ?? [])
        {
            for (var z = Math.Max(0, rect.MinZ); z <= Math.Min(terrain.CellsZ - 1, rect.MaxZ); z++)
            {
                for (var x = Math.Max(0, rect.MinX); x <= Math.Min(terrain.CellsX - 1, rect.MaxX); x++)
                {
                    costs[(z * terrain.CellsX) + x] = float.PositiveInfinity;
                }
            }
        }

        return costs;
    }

    /// <summary>
    /// Avec <paramref name="origin"/> (clé d'une cellule), le calcul s'arrête quand toutes les cellules à au plus <see cref="Bound"/> de la destination sont fixées,
    /// borne fixée en atteignant l'origine. Les cellules couvertes sont exactement celles du champ complet à au plus cette distance, avec les mêmes valeurs :
    /// la couverture ne dépend que du terrain, des coûts, de la destination et de l'origine.
    /// </summary>
    internal static FlowField Build(TerrainGrid terrain, float[] costs, int destX, int destZ, int origin = -1)
    {
        if (!GridQueries.TryNearestWalkable(terrain, costs, destX, destZ, out var dx, out var dz))
        {
            throw new InvalidOperationException("Le terrain n'a aucune cellule franchissable.");
        }

        var nx = terrain.CellsX;
        var nz = terrain.CellsZ;
        var count = nx * nz;
        var dist = GC.AllocateUninitializedArray<float>(count);
        Array.Fill(dist, float.PositiveInfinity);
        var next = GC.AllocateUninitializedArray<sbyte>(count);
        Array.Fill(next, (sbyte)-1);

        var orth = (float)terrain.CellSize;
        var diag = (float)(terrain.CellSize * Math.Sqrt(2.0));

        // File à seaux (Dial) : la largeur d'un seau reste sous la plus petite arête, donc deux cellules d'un même seau
        // ne s'améliorent jamais l'une l'autre et l'ordre à l'intérieur du seau est sans effet : le résultat est celui de Dijkstra.
        var work = scratch ??= new BuildScratch();
        var (minCost, maxCost) = work.CostRange(costs);
        var width = orth * minCost * 0.99f;
        var invWidth = 1f / width;
        var bucketCount = (int)Math.Ceiling(diag * maxCost / width) + 2;
        var buckets = work.Buckets(bucketCount);
        var done = work.Done(count);
        var touched = work.Touched(); // cellules à distance provisoire, à effacer hors de la borne
        var bound = float.PositiveInfinity;
        var lastBucket = int.MaxValue;
        var start = (dz * nx) + dx;
        dist[start] = 0f;
        buckets[0].Add(start);
        var pending = 1;
        for (var k = 0; pending > 0 && k <= lastBucket; k++)
        {
            var bucket = buckets[k % bucketCount];
            for (var n = 0; n < bucket.Count; n++)
            {
                pending--;
                var a = bucket[n];
                var d = dist[a];
                if (done[a] || d > bound)
                {
                    continue;
                }

                done[a] = true;
                var az = Math.DivRem(a, nx, out var ax);
                var ca = costs[a];
                if (a == origin)
                {
                    bound = (d * BoundFactor) + (BoundMarginCells * diag * ca); // coût de l'origine même : inchangé tant qu'aucune emprise ne la couvre
                    lastBucket = (int)(bound * invWidth) + 1; // un seau de plus : l'arrondi d'un seau ne laisse aucune cellule sous la borne
                }

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
                        if (origin >= 0 && float.IsPositiveInfinity(dist[b]))
                        {
                            touched.Add(b);
                        }

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

        if (!float.IsPositiveInfinity(bound))
        {
            foreach (var b in buckets)
            {
                b.Clear();
            }

            foreach (var b in touched)
            {
                if (!done[b])
                {
                    dist[b] = float.PositiveInfinity;
                    next[b] = -1;
                }
            }
        }

        touched.Clear();
        return new FlowField(nx, nz, terrain.CellSize, dx, dz, terrain.SurfaceVersion, dist, next, origin, bound);
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
        private float[]? _costs;
        private (float Min, float Max) _range;
        private List<int>[] _buckets = [];
        private bool[] _done = [];
        private readonly List<int> _touched = [];

        public List<int> Touched() => _touched; // vidée en fin de calcul

        /// <summary>Plus petit et plus grand coût franchissable ; relu seulement quand le tableau de coûts change (même instance = mêmes coûts, un tableau n'est jamais modifié après sa construction).</summary>
        public (float Min, float Max) CostRange(float[] costs)
        {
            if (!ReferenceEquals(costs, _costs))
            {
                float min = float.PositiveInfinity, max = 0f;
                foreach (var c in costs)
                {
                    if (!float.IsPositiveInfinity(c))
                    {
                        min = Math.Min(min, c);
                        max = Math.Max(max, c);
                    }
                }

                (_costs, _range) = (costs, (min, max));
            }

            return _range;
        }

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
