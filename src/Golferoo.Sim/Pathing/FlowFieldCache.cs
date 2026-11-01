using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Pathing;

/// <summary>
/// Cache de champs de flux par cellule de destination. Après une édition des surfaces (une retouche de relief seule ne coûte rien) (<see cref="TerrainGrid.SurfaceVersion"/>),
/// les champs en cache sont à refaire : <see cref="Get(int, int)"/> refait tout de suite celui qu'on demande, <see cref="Refresh"/> tous.
/// Les marcheurs du monde passent par <see cref="TryGet(Point2)"/> : un marcheur dont le champ attend son tour reste sur place ce tick-là.
/// Au début de chaque tick (<see cref="BeginTick"/>), les <see cref="RebuildsPerTick"/> plus anciennes destinations en attente sont refaites, dans l'ordre,
/// qu'on les demande ou non : chacune passe donc au bout de <c>capacité / RebuildsPerTick</c> ticks au plus, même si le pinceau tenu change la carte
/// à chaque tick et que les marcheurs de devant redemandent sans cesse le leur (C21). Les destinations qui attendent (<see cref="Pending"/>) sont un état du monde
/// (somme de contrôle, sauvegarde) ; les champs eux-mêmes se recalculent depuis le terrain.
/// Capacité bornée : au-delà, le plus ancien est évincé (ordre d'insertion, donc déterministe).
/// </summary>
public sealed class FlowFieldCache
{
    /// <summary>
    /// Champs calculés par tick pour les marcheurs, qu'il s'agisse d'une retouche des surfaces ou d'une destination jamais vue (environ 0,7 ms chacun sur une carte de 90 000 cellules,
    /// 3,8 ms sur le parcours de 18 trous du banc). Sans ce plafond, plusieurs destinations neuves tombent dans le même tick quand des groupes circulent, et le p99 du tick dépasse 5 ms (C12).
    /// </summary>
    public const int RebuildsPerTick = 1;

    private readonly TerrainGrid _terrain;
    private readonly int _capacity;
    private readonly Dictionary<int, FlowField> _fields = [];
    private readonly List<int> _order = []; // clés en ordre d'insertion
    private readonly List<int> _pending = []; // destinations dont le champ est à refaire, dans l'ordre où elles étaient en cache
    private readonly List<CellRect> _obstacles = []; // emprises de bâtiments : infranchissables
    private long _version;
    private int _builtThisTick;
    private float[] _costs;

    public FlowFieldCache(TerrainGrid terrain, int capacity = 64, IEnumerable<int>? pending = null, IEnumerable<int>? cached = null, IEnumerable<CellRect>? obstacles = null)
    {
        _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        _capacity = Math.Max(1, capacity);
        _version = terrain.SurfaceVersion;
        _obstacles.AddRange(obstacles ?? []);
        _costs = FlowField.BuildCosts(terrain, _obstacles);
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

    /// <summary>Début d'un tick du monde : constate une retouche des surfaces et refait, dans l'ordre d'attente, les plus anciennes destinations en attente.</summary>
    internal void BeginTick()
    {
        Sync();
        _builtThisTick = 0;
        while (_builtThisTick < RebuildsPerTick && _pending.Count > 0)
        {
            var key = _pending[0];
            _pending.RemoveAt(0);
            Store(key);
            _builtThisTick++;
        }
    }

    /// <summary>Champ vers la cellule franchissable la plus proche du point (la même instance tant que le terrain ne change pas).</summary>
    public FlowField Get(Point2 destination) => Get(_terrain.CellXOf(destination.X), _terrain.CellZOf(destination.Z));

    public FlowField Get(int cx, int cz) => Resolve(cx, cz, budgeted: false)!;

    /// <summary>
    /// Comme <see cref="Get(Point2)"/>, mais un champ à refaire après une retouche n'est jamais refait ici : nul, le marcheur attend que
    /// <see cref="BeginTick"/> le refasse à son tour. Une destination jamais vue se calcule tout de suite tant que le tick n'a pas
    /// atteint <see cref="RebuildsPerTick"/> champs ; au-delà, elle entre en attente comme après une retouche.
    /// </summary>
    internal FlowField? TryGet(Point2 destination) => Resolve(_terrain.CellXOf(destination.X), _terrain.CellZOf(destination.Z), budgeted: true);

    private FlowField? Resolve(int cx, int cz, bool budgeted)
    {
        Sync();
        if (!GridQueries.TryNearestWalkable(_terrain, _costs, cx, cz, out var nx, out var nz))
        {
            throw new InvalidOperationException("Le terrain n'a aucune cellule franchissable.");
        }

        var key = (nz * _terrain.CellsX) + nx;
        if (_fields.TryGetValue(key, out var field))
        {
            return field;
        }

        var waiting = _pending.IndexOf(key);
        if (budgeted)
        {
            if (waiting < 0 && _builtThisTick < RebuildsPerTick)
            {
                _builtThisTick++;
                return Store(key);
            }

            if (waiting < 0)
            {
                _pending.Add(key);
                TrimPending();
            }

            return null;
        }

        if (waiting >= 0)
        {
            _pending.RemoveAt(waiting);
        }

        return Store(key);
    }

    /// <summary>Calcule le champ vers la cellule de clé <paramref name="key"/> et le met en cache (évince le plus ancien si le cache est plein).</summary>
    private FlowField Store(int key)
    {
        if (_order.Count >= _capacity)
        {
            _fields.Remove(_order[0]);
            _order.RemoveAt(0);
        }

        var field = FlowField.Build(_terrain, _costs, key % _terrain.CellsX, key / _terrain.CellsX);
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

    /// <summary>Coûts de traversée si <paramref name="removed"/> était libérée et <paramref name="added"/> bloquée, pour essayer un emplacement sans toucher au cache.</summary>
    internal float[] CostsWith(CellRect? removed, CellRect? added)
    {
        var obstacles = _obstacles.Where(o => o != removed).ToList();
        if (added is { } a)
        {
            obstacles.Add(a);
        }

        return FlowField.BuildCosts(_terrain, obstacles);
    }

    /// <summary>
    /// Une emprise de bâtiment en remplace une autre (<paramref name="removed"/> libérée, <paramref name="added"/> bloquée). Seuls les champs que le
    /// changement touche passent en attente, comme après une retouche : un champ dont aucun chemin ne traverse la nouvelle emprise (aucune cellule voisine
    /// n'y pointe) et qui n'atteint pas l'ancienne reste identique à ce qu'un calcul neuf donnerait, et reste en cache (C15).
    /// </summary>
    internal void ChangeObstacle(CellRect? removed, CellRect? added)
    {
        Sync();
        if (removed is { } old)
        {
            _obstacles.Remove(old);
        }

        if (added is { } fresh)
        {
            _obstacles.Add(fresh);
        }

        _costs = FlowField.BuildCosts(_terrain, _obstacles);
        foreach (var key in _order.Where(key => IsTouched(_fields[key], removed, added)).ToList())
        {
            _fields.Remove(key);
            _order.Remove(key);
            if (!_pending.Contains(key))
            {
                _pending.Add(key);
            }
        }

        TrimPending();
    }

    private static bool IsTouched(FlowField field, CellRect? removed, CellRect? added) =>
        (removed is { } r && (r.Contains(field.DestX, field.DestZ) || ReachesRing(field, r, pointsInto: false)))
        || (added is { } a && (a.Contains(field.DestX, field.DestZ) || ReachesRing(field, a, pointsInto: true)));

    /// <summary>
    /// Pour une emprise ajoutée (<paramref name="pointsInto"/>) : une cellule voisine a son pas suivant dans l'emprise. Pour une emprise libérée : une cellule voisine est atteinte
    /// par le champ, donc les cellules libérées le seraient aussi.
    /// </summary>
    private static bool ReachesRing(FlowField field, CellRect rect, bool pointsInto)
    {
        for (var z = rect.MinZ - 1; z <= rect.MaxZ + 1; z++)
        {
            for (var x = rect.MinX - 1; x <= rect.MaxX + 1; x++)
            {
                if (rect.Contains(x, z))
                {
                    continue;
                }

                if (pointsInto ? field.TryNextCell(x, z, out var nx, out var nz) && rect.Contains(nx, nz) : field.IsReachable(x, z))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void Sync()
    {
        if (_version == _terrain.SurfaceVersion)
        {
            return;
        }

        // Les champs en cache passent en attente, dans leur ordre d'insertion ; les coûts, eux, se refont une fois pour toutes.
        _version = _terrain.SurfaceVersion;
        _costs = FlowField.BuildCosts(_terrain, _obstacles);
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
