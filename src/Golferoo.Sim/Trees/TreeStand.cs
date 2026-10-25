using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Trees;

/// <summary>
/// Les arbres du monde et leur index spatial : cases de 16 m, avec l'altitude absolue de la cime la plus haute de chaque case
/// (sol au pied de l'arbre + hauteur ; la balle ne teste les arbres que sous cette altitude). Recalculée à chaque ajout,
/// retrait, vieillissement (<see cref="Grow"/>) ou changement du relief (<see cref="RefreshTops"/>).
/// <see cref="Version"/> change à chaque modification.
/// </summary>
public sealed class TreeStand
{
    /// <summary>Côté d'une case de l'index, en mètres.</summary>
    public const double CellSize = 16.0;

    private readonly int _cellsX;
    private readonly int _cellsZ;
    private readonly List<Tree> _trees = [];
    private readonly Dictionary<int, Tree> _byId = [];
    private readonly List<int>[] _cells;
    private readonly double[] _top;
    private readonly TerrainGrid? _terrain;
    private long _ageSum;
    private int _nextId;

    /// <param name="terrain">Relief sous les arbres, pour les altitudes de cime ; sans relief, le sol est à 0.</param>
    public TreeStand(double widthMeters, double depthMeters, TerrainGrid? terrain = null)
    {
        _terrain = terrain;
        _cellsX = Math.Max(1, (int)Math.Ceiling(widthMeters / CellSize));
        _cellsZ = Math.Max(1, (int)Math.Ceiling(depthMeters / CellSize));
        _cells = new List<int>[_cellsX * _cellsZ];
        _top = new double[_cellsX * _cellsZ];
        Array.Fill(_top, double.NegativeInfinity);
    }

    /// <summary>Compteur incrémenté à chaque modification (ajout, retrait, vieillissement : une fois par appel).</summary>
    public long Version { get; private set; }

    public int Count => _trees.Count;

    /// <summary>Âge moyen des arbres en mois, 0 sans arbre.</summary>
    public double MeanAgeMonths => _trees.Count == 0 ? 0.0 : (double)_ageSum / _trees.Count;

    /// <summary>Arbres dans l'ordre d'ajout.</summary>
    public IReadOnlyList<Tree> Trees => _trees;

    /// <summary>Ajoute un arbre et lui donne un identifiant.</summary>
    public Tree Add(double x, double z, TreeSpeciesId species, int ageMonths, bool remarkable = false)
    {
        var tree = new Tree(_nextId++, x, z, species, ageMonths, TreeState.Standing, remarkable);
        _trees.Add(tree);
        _ageSum += ageMonths;
        _byId[tree.Id] = tree;
        var ci = CellIndex(x, z);
        (_cells[ci] ??= []).Add(tree.Id);
        _top[ci] = Math.Max(_top[ci], TopOf(tree));
        Version++;
        return tree;
    }

    public bool TryGet(int id, out Tree tree) => _byId.TryGetValue(id, out tree);

    /// <summary>Retire l'arbre ; faux s'il n'existe pas.</summary>
    public bool Remove(int id) => RemoveWhere(t => t.Id == id) > 0;

    /// <summary>Retire les arbres qui vérifient le prédicat ; rend leur nombre.</summary>
    public int RemoveWhere(Func<Tree, bool> predicate)
    {
        var touched = new HashSet<int>();
        var removed = _trees.RemoveAll(t =>
        {
            if (!predicate(t))
            {
                return false;
            }

            _byId.Remove(t.Id);
            _ageSum -= t.AgeMonths;
            var ci = CellIndex(t.X, t.Z);
            _cells[ci].Remove(t.Id);
            touched.Add(ci);
            return true;
        });
        foreach (var ci in touched)
        {
            RecomputeCell(ci);
        }

        if (removed > 0)
        {
            Version++;
        }

        return removed;
    }

    /// <summary>
    /// Vieillit tous les arbres de <paramref name="months"/> mois et recalcule les altitudes de cime ; la version n'augmente
    /// qu'une fois. Rend le nombre d'arbres vieillis.
    /// </summary>
    public int Grow(int months)
    {
        if (months < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(months), months, "Le temps ne recule pas.");
        }

        if (months == 0 || _trees.Count == 0)
        {
            return 0;
        }

        Array.Fill(_top, double.NegativeInfinity);
        for (var i = 0; i < _trees.Count; i++)
        {
            var t = _trees[i] with { AgeMonths = _trees[i].AgeMonths + months };
            _trees[i] = t;
            _byId[t.Id] = t;
            var ci = CellIndex(t.X, t.Z);
            _top[ci] = Math.Max(_top[ci], TopOf(t));
        }

        _ageSum += (long)months * _trees.Count;
        Version++;
        return _trees.Count;
    }

    /// <summary>Recalcule les altitudes de cime des cases de l'index que touche le rectangle [x0, x1] x [z0, z1] (mètres) : le relief y a changé.</summary>
    public void RefreshTops(double x0, double z0, double x1, double z1)
    {
        var cx0 = Math.Clamp((int)Math.Floor(x0 / CellSize), 0, _cellsX - 1);
        var cx1 = Math.Clamp((int)Math.Floor(x1 / CellSize), 0, _cellsX - 1);
        var cz0 = Math.Clamp((int)Math.Floor(z0 / CellSize), 0, _cellsZ - 1);
        var cz1 = Math.Clamp((int)Math.Floor(z1 / CellSize), 0, _cellsZ - 1);
        for (var cz = cz0; cz <= cz1; cz++)
        {
            for (var cx = cx0; cx <= cx1; cx++)
            {
                RecomputeCell((cz * _cellsX) + cx);
            }
        }
    }

    /// <summary>Altitude absolue de la cime la plus haute de la case de 16 m qui contient (x, z), moins l'infini s'il n'y a pas d'arbre debout.</summary>
    public double MaxTopAt(double x, double z) => _top[CellIndex(x, z)];

    /// <summary>
    /// Altitude absolue de la cime la plus haute des cases de 16 m que touche le carré de demi-côté <paramref name="margin"/> centré sur (x, z) :
    /// une couronne déborde de sa case, la balle près d'un bord doit voir aussi les cases voisines.
    /// </summary>
    public double MaxTopNear(double x, double z, double margin)
    {
        var x0 = Math.Clamp((int)Math.Floor((x - margin) / CellSize), 0, _cellsX - 1);
        var x1 = Math.Clamp((int)Math.Floor((x + margin) / CellSize), 0, _cellsX - 1);
        var z0 = Math.Clamp((int)Math.Floor((z - margin) / CellSize), 0, _cellsZ - 1);
        var z1 = Math.Clamp((int)Math.Floor((z + margin) / CellSize), 0, _cellsZ - 1);
        var max = double.NegativeInfinity;
        for (var cz = z0; cz <= z1; cz++)
        {
            for (var cx = x0; cx <= x1; cx++)
            {
                max = Math.Max(max, _top[(cz * _cellsX) + cx]);
            }
        }

        return max;
    }

    /// <summary>Ajoute à <paramref name="result"/> les arbres dont le centre est à au plus <paramref name="radius"/> mètres de (x, z).</summary>
    public void Query(double x, double z, double radius, List<Tree> result)
    {
        var x0 = Math.Clamp((int)Math.Floor((x - radius) / CellSize), 0, _cellsX - 1);
        var x1 = Math.Clamp((int)Math.Floor((x + radius) / CellSize), 0, _cellsX - 1);
        var z0 = Math.Clamp((int)Math.Floor((z - radius) / CellSize), 0, _cellsZ - 1);
        var z1 = Math.Clamp((int)Math.Floor((z + radius) / CellSize), 0, _cellsZ - 1);
        var r2 = radius * radius;
        for (var cz = z0; cz <= z1; cz++)
        {
            for (var cx = x0; cx <= x1; cx++)
            {
                if (_cells[(cz * _cellsX) + cx] is not { } ids)
                {
                    continue;
                }

                foreach (var id in ids)
                {
                    var t = _byId[id];
                    var dx = t.X - x;
                    var dz = t.Z - z;
                    if ((dx * dx) + (dz * dz) <= r2)
                    {
                        result.Add(t);
                    }
                }
            }
        }
    }

    private double TopOf(Tree t) =>
        t.State == TreeState.Standing ? (_terrain?.Sample(t.X, t.Z) ?? 0.0) + t.Height() : double.NegativeInfinity;

    private void RecomputeCell(int ci)
    {
        var max = double.NegativeInfinity;
        if (_cells[ci] is { } ids)
        {
            foreach (var id in ids)
            {
                max = Math.Max(max, TopOf(_byId[id]));
            }
        }

        _top[ci] = max;
    }

    private int CellIndex(double x, double z)
    {
        var cx = Math.Clamp((int)Math.Floor(x / CellSize), 0, _cellsX - 1);
        var cz = Math.Clamp((int)Math.Floor(z / CellSize), 0, _cellsZ - 1);
        return (cz * _cellsX) + cx;
    }
}
