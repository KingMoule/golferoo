using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Pathing;

/// <summary>
/// Cache de champs de flux par cellule de destination. Après une édition des surfaces (une retouche de relief seule ne coûte rien) (<see cref="TerrainGrid.SurfaceVersion"/>),
/// les champs en cache sont à refaire : <see cref="Get(int, int)"/> refait tout de suite celui qu'on demande, <see cref="Refresh"/> tous.
/// Les marcheurs du monde passent par <see cref="TryGet(Point2, Point2)"/> : un marcheur dont le champ attend son tour reste sur place ce tick-là.
/// Le champ qu'un marcheur fait calculer est borné par sa position (<see cref="FlowField.Origin"/>) : un marcheur hors de la couverture le fait refaire complet.
/// <see cref="Get(int, int)"/> rend toujours un champ complet.
/// Au début de chaque tick (<see cref="BeginTick"/>), les <see cref="RebuildsPerTick"/> plus anciennes destinations en attente sont refaites, dans l'ordre,
/// qu'on les demande ou non : chacune passe donc au bout de <c>capacité / RebuildsPerTick</c> ticks au plus, même si le pinceau tenu change la carte
/// à chaque tick et que les marcheurs de devant redemandent sans cesse le leur (C21). Les destinations qui attendent (<see cref="Pending"/>) sont un état du monde
/// (somme de contrôle, sauvegarde), avec l'origine de leur borne ; les champs eux-mêmes se recalculent depuis le terrain et leur origine.
/// Capacité bornée : au-delà, le plus ancien est évincé (ordre d'insertion, donc déterministe).
/// </summary>
public sealed class FlowFieldCache
{
    /// <summary>
    /// Champs calculés par tick pour les marcheurs, qu'il s'agisse d'une retouche des surfaces ou d'une destination jamais vue (un champ complet coûte environ 0,7 ms sur une carte de 90 000 cellules,
    /// 3,8 ms sur le parcours de 18 trous du banc, d'où la borne). Sans ce plafond, plusieurs destinations neuves tombent dans le même tick quand des groupes circulent, et le p99 du tick dépasse 5 ms (C12).
    /// </summary>
    public const int RebuildsPerTick = 1;

    private readonly TerrainGrid _terrain;
    private readonly int _capacity;
    private readonly Dictionary<int, FlowField> _fields = [];
    private readonly List<int> _order = []; // clés en ordre d'insertion
    private readonly List<(int Key, int Origin)> _pending = []; // destinations dont le champ est à refaire, dans l'ordre où elles étaient en cache
    private readonly List<CellRect> _obstacles = []; // emprises de bâtiments : infranchissables
    private long _version;
    private int _builtThisTick;
    private float[] _costs;

    public FlowFieldCache(TerrainGrid terrain, int capacity = 64, IEnumerable<(int Key, int Origin)>? pending = null, IEnumerable<(int Key, int Origin)>? cached = null, IEnumerable<CellRect>? obstacles = null)
    {
        _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        _capacity = Math.Max(1, capacity);
        _version = terrain.SurfaceVersion;
        _obstacles.AddRange(obstacles ?? []);
        _costs = FlowField.BuildCosts(terrain, _obstacles);
        if (pending is not null)
        {
            _pending.AddRange(pending.DistinctBy(p => p.Key));
            TrimPending();
        }

        // Quels champs sont en cache décide, après une retouche, lesquels attendent : une sauvegarde les redonne, dans l'ordre, et on les refait.
        foreach (var (key, origin) in cached ?? [])
        {
            if (key >= 0 && key < terrain.CellsX * terrain.CellsZ && !_fields.ContainsKey(key))
            {
                _pending.RemoveAll(p => p.Key == key);
                Store(key, origin);
            }
        }
    }

    public int Count => _order.Count;

    /// <summary>Destinations (clés de cellule) dont le champ attend d'être refait après une retouche, avec l'origine de sa borne : état du monde.</summary>
    internal IReadOnlyList<(int Key, int Origin)> Pending => _pending;

    /// <summary>Destinations des champs en cache, du plus ancien au plus récent : écrites dans la sauvegarde (pas dans la somme de contrôle, elles se déduisent de l'historique).</summary>
    internal IReadOnlyList<int> CachedKeys => _order;

    /// <summary><see cref="CachedKeys"/> avec l'origine de la borne de chaque champ, pour la sauvegarde.</summary>
    internal IEnumerable<(int Key, int Origin)> Cached => _order.Select(key => (key, _fields[key].Origin));

    /// <summary>Début d'un tick du monde : constate une retouche des surfaces et refait, dans l'ordre d'attente, les plus anciennes destinations en attente.</summary>
    internal void BeginTick()
    {
        Sync();
        _builtThisTick = 0;
        while (_builtThisTick < RebuildsPerTick && _pending.Count > 0)
        {
            var (key, origin) = _pending[0];
            _pending.RemoveAt(0);
            Store(key, origin);
            _builtThisTick++;
        }
    }

    /// <summary>Champ vers la cellule franchissable la plus proche du point (la même instance tant que le terrain ne change pas).</summary>
    public FlowField Get(Point2 destination) => Get(_terrain.CellXOf(destination.X), _terrain.CellZOf(destination.Z));

    public FlowField Get(int cx, int cz) => Resolve(cx, cz, from: -1)!;

    /// <summary>
    /// Comme <see cref="Get(Point2)"/> pour un marcheur en <paramref name="from"/>, mais un champ à refaire après une retouche n'est jamais refait ici : nul, le marcheur attend que
    /// <see cref="BeginTick"/> le refasse à son tour. Une destination jamais vue se calcule tout de suite, bornée par la cellule du marcheur, tant que le tick n'a pas
    /// atteint <see cref="RebuildsPerTick"/> champs ; au-delà, elle entre en attente comme après une retouche. Un champ en cache qui ne couvre pas la cellule
    /// du marcheur se refait complet, sous le même budget.
    /// </summary>
    internal FlowField? TryGet(Point2 destination, Point2 from) =>
        Resolve(_terrain.CellXOf(destination.X), _terrain.CellZOf(destination.Z), from: (_terrain.CellZOf(from.Z) * _terrain.CellsX) + _terrain.CellXOf(from.X));

    /// <summary>Sans marcheur (<paramref name="from"/> à -1), hors budget et toujours complet.</summary>
    private FlowField? Resolve(int cx, int cz, int from)
    {
        Sync();
        if (!GridQueries.TryNearestWalkable(_terrain, _costs, cx, cz, out var nx, out var nz))
        {
            throw new InvalidOperationException("Le terrain n'a aucune cellule franchissable.");
        }

        var key = (nz * _terrain.CellsX) + nx;
        var budgeted = from >= 0;
        if (_fields.TryGetValue(key, out var field))
        {
            if (field.IsComplete || (budgeted && field.IsReachable(from % _terrain.CellsX, from / _terrain.CellsX)))
            {
                return field;
            }

            if (!budgeted || _builtThisTick < RebuildsPerTick)
            {
                _builtThisTick += budgeted ? 1 : 0;
                return _fields[key] = FlowField.Build(_terrain, _costs, nx, nz); // trop étroit : refait complet, à sa place dans l'ordre
            }

            // Trop étroit et budget épuisé : il attend son tour, complet.
            _fields.Remove(key);
            _order.Remove(key);
            _pending.Add((key, -1));
            TrimPending();
            return null;
        }

        var waiting = _pending.FindIndex(p => p.Key == key);
        if (budgeted)
        {
            if (waiting < 0 && _builtThisTick < RebuildsPerTick)
            {
                _builtThisTick++;
                return Store(key, from);
            }

            if (waiting < 0)
            {
                _pending.Add((key, from));
                TrimPending();
            }

            return null;
        }

        if (waiting >= 0)
        {
            _pending.RemoveAt(waiting);
        }

        return Store(key, -1);
    }

    /// <summary>Calcule le champ vers la cellule de clé <paramref name="key"/>, borné par <paramref name="origin"/> (-1 : complet), et le met en cache (évince le plus ancien si le cache est plein).</summary>
    private FlowField Store(int key, int origin)
    {
        if (_order.Count >= _capacity)
        {
            _fields.Remove(_order[0]);
            _order.RemoveAt(0);
        }

        var field = FlowField.Build(_terrain, _costs, key % _terrain.CellsX, key / _terrain.CellsX, origin);
        _fields[key] = field;
        _order.Add(key);
        return field;
    }

    /// <summary>Refait tout de suite les champs à refaire si le terrain a changé depuis.</summary>
    public void Refresh()
    {
        Sync();
        foreach (var (key, _) in _pending.ToArray())
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
    /// n'y pointe) et qui n'atteint pas l'ancienne reste identique à ce qu'un calcul neuf donnerait, et reste en cache (C15). Un champ borné aussi, si l'origine
    /// de sa borne n'est dans aucune des deux emprises : ses cellules couvertes et sa borne ne changent pas.
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
            _pending.Add((key, _fields[key].Origin));
            _fields.Remove(key);
            _order.Remove(key);
        }

        TrimPending();
    }

    private static bool IsTouched(FlowField field, CellRect? removed, CellRect? added) =>
        (removed is { } r && (r.Contains(field.DestX, field.DestZ) || HoldsOrigin(field, r) || ReachesRing(field, r, pointsInto: false)))
        || (added is { } a && (a.Contains(field.DestX, field.DestZ) || HoldsOrigin(field, a) || ReachesRing(field, a, pointsInto: true)));

    private static bool HoldsOrigin(FlowField field, CellRect rect) => field.Origin >= 0 && rect.Contains(field.Origin % field.CellsX, field.Origin / field.CellsX);

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
            _pending.Add((key, _fields[key].Origin));
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
