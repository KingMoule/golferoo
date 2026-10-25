namespace Golferoo.Sim.Trees;

/// <summary>
/// Les arbres du monde et leur index spatial : cases de 16 m, avec la hauteur maximale des arbres de chaque case
/// (la balle ne teste les arbres que sous cette hauteur, lot 2). <see cref="Version"/> change à chaque modification.
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
    private readonly double[] _maxHeight;
    private int _nextId;

    public TreeStand(double widthMeters, double depthMeters)
    {
        _cellsX = Math.Max(1, (int)Math.Ceiling(widthMeters / CellSize));
        _cellsZ = Math.Max(1, (int)Math.Ceiling(depthMeters / CellSize));
        _cells = new List<int>[_cellsX * _cellsZ];
        _maxHeight = new double[_cellsX * _cellsZ];
    }

    /// <summary>Compteur incrémenté à chaque ajout ou retrait.</summary>
    public long Version { get; private set; }

    public int Count => _trees.Count;

    /// <summary>Arbres dans l'ordre d'ajout.</summary>
    public IReadOnlyList<Tree> Trees => _trees;

    /// <summary>Ajoute un arbre et lui donne un identifiant.</summary>
    public Tree Add(double x, double z, TreeSpeciesId species, int ageMonths, bool remarkable = false)
    {
        var tree = new Tree(_nextId++, x, z, species, ageMonths, TreeState.Standing, remarkable);
        _trees.Add(tree);
        _byId[tree.Id] = tree;
        var ci = CellIndex(x, z);
        (_cells[ci] ??= []).Add(tree.Id);
        _maxHeight[ci] = Math.Max(_maxHeight[ci], tree.Height());
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
            var ci = CellIndex(t.X, t.Z);
            _cells[ci].Remove(t.Id);
            touched.Add(ci);
            return true;
        });
        foreach (var ci in touched)
        {
            var max = 0.0;
            foreach (var id in _cells[ci])
            {
                max = Math.Max(max, _byId[id].Height());
            }

            _maxHeight[ci] = max;
        }

        if (removed > 0)
        {
            Version++;
        }

        return removed;
    }

    /// <summary>Hauteur de l'arbre le plus haut de la case de 16 m qui contient (x, z), 0 s'il n'y en a pas.</summary>
    public double MaxHeightAt(double x, double z) => _maxHeight[CellIndex(x, z)];

    /// <summary>
    /// Hauteur de l'arbre le plus haut des cases de 16 m que touche le carré de demi-côté <paramref name="margin"/> centré sur (x, z) :
    /// une couronne déborde de sa case, la balle près d'un bord doit voir aussi les cases voisines.
    /// </summary>
    public double MaxHeightNear(double x, double z, double margin)
    {
        var x0 = Math.Clamp((int)Math.Floor((x - margin) / CellSize), 0, _cellsX - 1);
        var x1 = Math.Clamp((int)Math.Floor((x + margin) / CellSize), 0, _cellsX - 1);
        var z0 = Math.Clamp((int)Math.Floor((z - margin) / CellSize), 0, _cellsZ - 1);
        var z1 = Math.Clamp((int)Math.Floor((z + margin) / CellSize), 0, _cellsZ - 1);
        var max = 0.0;
        for (var cz = z0; cz <= z1; cz++)
        {
            for (var cx = x0; cx <= x1; cx++)
            {
                max = Math.Max(max, _maxHeight[(cz * _cellsX) + cx]);
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

    private int CellIndex(double x, double z)
    {
        var cx = Math.Clamp((int)Math.Floor(x / CellSize), 0, _cellsX - 1);
        var cz = Math.Clamp((int)Math.Floor(z / CellSize), 0, _cellsZ - 1);
        return (cz * _cellsX) + cx;
    }
}
