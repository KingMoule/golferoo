using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Pathing;

/// <summary>
/// Cache de cartes de jeu par cellule de drapeau. Une carte est refaite à la demande quand les surfaces du terrain
/// (<see cref="TerrainGrid.SurfaceVersion"/>) ou les arbres (<see cref="TreeStand.Version"/>) ont changé depuis sa construction ;
/// un drapeau déplacé dans une autre cellule en demande une autre. Capacité bornée : au-delà, la moins récemment utilisée est évincée.
/// Sûr entre fils (les résolveurs de la ligue tournent en parallèle).
/// </summary>
public sealed class PlayMapCache(int capacity = 4)
{
    private readonly object _lock = new();
    private readonly List<PlayMap> _maps = [];
    private float[]? _costs;
    private TerrainGrid? _costsTerrain;
    private TreeStand? _costsTrees;
    private long _costsTerrainVersion;
    private long _costsTreesVersion;

    /// <summary>Nombre de cartes gardées ; le monde l'ajuste au parcours (un drapeau et un centre de green par trou), sinon chaque coup d'un autre trou refait une carte.</summary>
    public int Capacity { get; set; } = capacity;

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _maps.Count;
            }
        }
    }

    /// <summary>La carte d'indice <paramref name="i"/> vient de servir : elle passe en fin de liste, les plus anciennement utilisées sont évincées d'abord (un drapeau déplacé laisse une carte morte qui vieillit).</summary>
    private void Touch(int i)
    {
        var m = _maps[i];
        _maps.RemoveAt(i);
        _maps.Add(m);
    }

    public PlayMap Get(TerrainGrid terrain, TreeStand? trees, Point2 hole)
    {
        var cell = (terrain.CellZOf(hole.Z) * terrain.CellsX) + terrain.CellXOf(hole.X);
        var treesVersion = trees?.Version ?? 0;
        lock (_lock)
        {
            for (var i = 0; i < _maps.Count; i++)
            {
                var m = _maps[i];
                if (m.HoleCell == cell && m.TerrainVersion == terrain.SurfaceVersion && ReferenceEquals(m.Trees, trees) && m.TreesVersion == treesVersion && m.IsOf(terrain))
                {
                    Touch(i);
                    return m;
                }
            }

            // Terrain ou arbres ont changé depuis : si les coûts des cellules sont les mêmes (une retouche de bunker ou de fairway ne change rien
            // à la carte), c'est la même carte. Les coûts se comptent une fois par version, pour toutes les cartes.
            if (_costs is null || !ReferenceEquals(_costsTerrain, terrain) || _costsTerrainVersion != terrain.SurfaceVersion || !ReferenceEquals(_costsTrees, trees) || _costsTreesVersion != treesVersion)
            {
                _costs = PlayMap.CellCosts(terrain, trees);
                (_costsTerrain, _costsTerrainVersion, _costsTrees, _costsTreesVersion) = (terrain, terrain.SurfaceVersion, trees, treesVersion);
            }

            for (var i = 0; i < _maps.Count; i++)
            {
                var m = _maps[i];
                if (m.HoleCell == cell && ReferenceEquals(m.Trees, trees) && m.IsOf(terrain) && m.Costs.AsSpan().SequenceEqual(_costs))
                {
                    (m.TerrainVersion, m.TreesVersion) = (terrain.SurfaceVersion, treesVersion);
                    Touch(i);
                    return m;
                }
            }

            _maps.RemoveAll(m => !m.IsOf(terrain) || m.HoleCell == cell);
            if (_maps.Count >= Math.Max(1, Capacity))
            {
                _maps.RemoveAt(0);
            }

            var built = PlayMap.Build(terrain, trees, hole, _costs);
            _maps.Add(built);
            return built;
        }
    }
}
