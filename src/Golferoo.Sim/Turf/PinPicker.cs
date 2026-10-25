using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Turf;

/// <summary>Position du drapeau du jour : une cellule de green à au moins <see cref="EdgeMeters"/> du bord, tirée du flux de la graine et du jour.</summary>
public static class PinPicker
{
    public const double EdgeMeters = 3.0;

    /// <summary>Centre de la cellule tirée, ou <c>null</c> si aucun green n'a de cellule à 3 m du bord.</summary>
    public static Point2? Pick(TerrainGrid terrain, ulong seed, int day)
    {
        var surfaces = terrain.Surfaces;
        var reach = (int)Math.Ceiling(EdgeMeters / terrain.CellSize) + 1;
        var candidates = new List<int>();
        for (var i = 0; i < surfaces.Length; i++)
        {
            if (surfaces[i] == Surface.Green && InsideGreen(terrain, i % terrain.CellsX, i / terrain.CellsX, reach))
            {
                candidates.Add(i);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        var rng = new DeterministicRng(seed ^ (0x9E3779B97F4A7C15UL * (ulong)(uint)(day + 1)));
        var pick = candidates[(int)(rng.NextUInt64() % (ulong)candidates.Count)];
        return terrain.CellCenter(pick % terrain.CellsX, pick / terrain.CellsX);
    }

    // Aucune cellule hors green (ou hors carte) à moins de 3 m du centre : distance du centre au carré de la cellule voisine.
    private static bool InsideGreen(TerrainGrid terrain, int cx, int cz, int reach)
    {
        var limit = EdgeMeters / terrain.CellSize;
        for (var dz = -reach; dz <= reach; dz++)
        {
            for (var dx = -reach; dx <= reach; dx++)
            {
                var gx = Math.Max(Math.Abs(dx) - 0.5, 0.0);
                var gz = Math.Max(Math.Abs(dz) - 0.5, 0.0);
                if (((gx * gx) + (gz * gz)) < limit * limit && terrain.IsOutsideGreen(cx + dx, cz + dz))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
