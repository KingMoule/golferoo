using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Pathing;

/// <summary>
/// Cache de champs de flux par cellule de destination. Après une édition des surfaces (une retouche de relief seule ne coûte rien) (<see cref="TerrainGrid.SurfaceVersion"/>),
/// tous les champs en cache sont reconstruits : à la prochaine demande, ou tout de suite par <see cref="Refresh"/>.
/// Capacité bornée : au-delà, le plus ancien est évincé (ordre d'insertion, donc déterministe).
/// </summary>
public sealed class FlowFieldCache
{
    private readonly TerrainGrid _terrain;
    private readonly int _capacity;
    private readonly Dictionary<int, FlowField> _fields = [];
    private readonly List<int> _order = []; // clés en ordre d'insertion
    private long _version;
    private float[] _costs;

    public FlowFieldCache(TerrainGrid terrain, int capacity = 64)
    {
        _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        _capacity = Math.Max(1, capacity);
        _version = terrain.SurfaceVersion;
        _costs = FlowField.BuildCosts(terrain);
    }

    public int Count => _order.Count;

    /// <summary>Champ vers la cellule franchissable la plus proche du point (la même instance tant que le terrain ne change pas).</summary>
    public FlowField Get(Point2 destination) => Get(_terrain.CellXOf(destination.X), _terrain.CellZOf(destination.Z));

    public FlowField Get(int cx, int cz)
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

    /// <summary>Reconstruit tout de suite les champs en cache si le terrain a changé depuis.</summary>
    public void Refresh() => Sync();

    private void Sync()
    {
        if (_version == _terrain.SurfaceVersion)
        {
            return;
        }

        _version = _terrain.SurfaceVersion;
        _costs = FlowField.BuildCosts(_terrain);
        // Les destinations restent franchissables ou se replient ; on reconstruit en ordre d'insertion.
        var keys = _order.ToArray();
        _fields.Clear();
        _order.Clear();
        foreach (var key in keys)
        {
            if (!GridQueries.TryNearestWalkable(_terrain, key % _terrain.CellsX, key / _terrain.CellsX, out var nx, out var nz))
            {
                continue;
            }

            var k = (nz * _terrain.CellsX) + nx;
            if (_fields.ContainsKey(k))
            {
                continue;
            }

            _fields[k] = FlowField.Build(_terrain, _costs, nx, nz);
            _order.Add(k);
        }
    }
}
