using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Pathing;

/// <summary>
/// Cache de champs de flux par cellule de destination. Après une édition des surfaces (une retouche de relief seule ne coûte rien) (<see cref="TerrainGrid.SurfaceVersion"/>),
/// les champs en cache sont à refaire : <see cref="Get(int, int)"/> refait tout de suite celui qu'on demande, <see cref="Refresh"/> tous.
/// Les marcheurs du monde passent par <see cref="TryGet(Point2)"/> : au plus <see cref="RebuildsPerTick"/> champs refaits par tick, et un
/// marcheur dont le champ attend son tour reste sur place ce tick-là. Les destinations qui attendent (<see cref="Pending"/>) sont un état du monde
/// (somme de contrôle, sauvegarde) ; les champs eux-mêmes se recalculent depuis le terrain.
/// Capacité bornée : au-delà, le plus ancien est évincé (ordre d'insertion, donc déterministe).
/// </summary>
public sealed class FlowFieldCache
{
    /// <summary>Champs refaits par tick après une retouche des surfaces, pour les marcheurs (environ 0,7 ms chacun sur une carte de 90 000 cellules).</summary>
    public const int RebuildsPerTick = 2;

    private readonly TerrainGrid _terrain;
    private readonly int _capacity;
    private readonly Dictionary<int, FlowField> _fields = [];
    private readonly List<int> _order = []; // clés en ordre d'insertion
    private readonly List<int> _pending = []; // destinations dont le champ est à refaire, dans l'ordre où elles étaient en cache
    private long _version;
    private float[] _costs;
    private int _rebuildsLeft = RebuildsPerTick;

    public FlowFieldCache(TerrainGrid terrain, int capacity = 64, IEnumerable<int>? pending = null, IEnumerable<int>? cached = null)
    {
        _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        _capacity = Math.Max(1, capacity);
        _version = terrain.SurfaceVersion;
        _costs = FlowField.BuildCosts(terrain);
        if (pending is not null)
        {
            _pending.AddRange(pending.Distinct());
            TrimPending();
        }

        // Quels champs sont en cache décide, après une retouche, lesquels attendent : une sauvegarde les redonne, dans l'ordre, et on les refait.
        foreach (var key in cached ?? [])
        {
            if (key >= 0 && key < terrain.CellsX * terrain.CellsZ)
            {
                Get(key % terrain.CellsX, key / terrain.CellsX);
            }
        }
    }

    public int Count => _order.Count;

    /// <summary>Destinations (clés de cellule) dont le champ attend d'être refait après une retouche : état du monde.</summary>
    internal IReadOnlyList<int> Pending => _pending;

    /// <summary>Destinations des champs en cache, du plus ancien au plus récent : écrites dans la sauvegarde (pas dans la somme de contrôle, elles se déduisent de l'historique).</summary>
    internal IReadOnlyList<int> CachedKeys => _order;

    /// <summary>Début d'un tick du monde : constate une retouche des surfaces et rend son budget de reconstructions.</summary>
    internal void BeginTick()
    {
        Sync();
        _rebuildsLeft = RebuildsPerTick;
    }

    /// <summary>Champ vers la cellule franchissable la plus proche du point (la même instance tant que le terrain ne change pas).</summary>
    public FlowField Get(Point2 destination) => Get(_terrain.CellXOf(destination.X), _terrain.CellZOf(destination.Z));

    public FlowField Get(int cx, int cz) => Resolve(cx, cz, budgeted: false)!;

    /// <summary>
    /// Comme <see cref="Get(Point2)"/>, mais un champ à refaire après une retouche n'est refait que s'il reste du budget ce tick-ci (<see cref="RebuildsPerTick"/>) ;
    /// sinon nul : le marcheur attend. Une destination jamais vue se calcule tout de suite.
    /// </summary>
    internal FlowField? TryGet(Point2 destination) => Resolve(_terrain.CellXOf(destination.X), _terrain.CellZOf(destination.Z), budgeted: true);

    private FlowField? Resolve(int cx, int cz, bool budgeted)
    {
        Sync();
        if (!GridQueries.TryNearestWalkable(_terrain, cx, cz, out var nx, out var nz))
        {
            throw new InvalidOperationException("Le terrain n'a aucune cellule franchissable.");
        }

        var key = (nz * _terrain.CellsX) + nx;
        if (_fields.TryGetValue(key, out var field))
        {
            return field;
        }

        var waiting = _pending.IndexOf(key);
        if (waiting >= 0)
        {
            if (budgeted && _rebuildsLeft <= 0)
            {
                return null;
            }

            if (budgeted)
            {
                _rebuildsLeft--;
            }

            _pending.RemoveAt(waiting);
        }

        if (_order.Count >= _capacity)
        {
            _fields.Remove(_order[0]);
            _order.RemoveAt(0);
        }

        field = FlowField.Build(_terrain, _costs, nx, nz);
        _fields[key] = field;
        _order.Add(key);
        return field;
    }

    /// <summary>Refait tout de suite les champs à refaire si le terrain a changé depuis.</summary>
    public void Refresh()
    {
        Sync();
        foreach (var key in _pending.ToArray())
        {
            Get(key % _terrain.CellsX, key / _terrain.CellsX);
        }

        _pending.Clear();
    }

    private void Sync()
    {
        if (_version == _terrain.SurfaceVersion)
        {
            return;
        }

        // Les champs en cache passent en attente, dans leur ordre d'insertion ; les coûts, eux, se refont une fois pour toutes.
        _version = _terrain.SurfaceVersion;
        _costs = FlowField.BuildCosts(_terrain);
        foreach (var key in _order)
        {
            if (!_pending.Contains(key))
            {
                _pending.Add(key);
            }
        }

        _fields.Clear();
        _order.Clear();
        TrimPending();
    }

    private void TrimPending()
    {
        if (_pending.Count > _capacity)
        {
            _pending.RemoveRange(0, _pending.Count - _capacity);
        }
    }
}
