using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Pathing;

/// <summary>
/// Cache de cartes de jeu par cellule de drapeau. Une carte est refaite à la demande quand les surfaces du terrain
/// (<see cref="TerrainGrid.SurfaceVersion"/>) ou les arbres (<see cref="TreeStand.Version"/>) ont changé depuis sa construction ;
/// un drapeau déplacé dans une autre cellule en demande une autre. Capacité bornée : au-delà, la plus ancienne est évincée.
/// Sûr entre fils (les résolveurs de la ligue tournent en parallèle).
/// </summary>
public sealed class PlayMapCache(int capacity = 4)
{
    private readonly object _lock = new();
    private readonly List<PlayMap> _maps = [];

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
                    return m;
                }
            }

            _maps.RemoveAll(m => !m.IsOf(terrain) || m.HoleCell == cell);
            if (_maps.Count >= Math.Max(1, capacity))
            {
                _maps.RemoveAt(0);
            }

            var built = PlayMap.Build(terrain, trees, hole);
            _maps.Add(built);
            return built;
        }
    }
}
