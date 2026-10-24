using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Pathing;

/// <summary>Requêtes déterministes sur la grille de cellules.</summary>
public static class GridQueries
{
    /// <summary>
    /// Cellule franchissable la plus proche de (cx, cz) (distance euclidienne entre centres ; à égalité, la première en ordre ligne par ligne).
    /// Renvoie la cellule elle-même si elle est franchissable. Faux si la carte n'a aucune cellule franchissable.
    /// </summary>
    public static bool TryNearestWalkable(TerrainGrid terrain, int cx, int cz, out int nx, out int nz)
    {
        cx = Math.Clamp(cx, 0, terrain.CellsX - 1);
        cz = Math.Clamp(cz, 0, terrain.CellsZ - 1);
        nx = cx;
        nz = cz;
        if (terrain.IsWalkable(cx, cz))
        {
            return true;
        }

        var maxRadius = Math.Max(terrain.CellsX, terrain.CellsZ);
        var bestD2 = long.MaxValue;
        var found = false;
        var bestRing = -1;
        for (var r = 1; r <= maxRadius; r++)
        {
            // Une cellule de l'anneau r+1 ne peut battre une trouvaille de l'anneau r que de peu : on regarde un anneau de plus.
            if (found && r > bestRing + 1)
            {
                break;
            }

            for (var z = cz - r; z <= cz + r; z++)
            {
                var edgeRow = z == cz - r || z == cz + r;
                var step = edgeRow ? 1 : 2 * r;
                for (var x = cx - r; x <= cx + r; x += step)
                {
                    if (!terrain.IsWalkable(x, z))
                    {
                        continue;
                    }

                    var d2 = ((long)(x - cx) * (x - cx)) + ((long)(z - cz) * (z - cz));
                    if (d2 < bestD2)
                    {
                        bestD2 = d2;
                        nx = x;
                        nz = z;
                        if (!found)
                        {
                            bestRing = r;
                        }

                        found = true;
                    }
                }
            }
        }

        return found;
    }

    /// <summary>Point franchissable le plus proche : le point lui-même s'il l'est, sinon le centre de la cellule franchissable la plus proche.</summary>
    public static Point2 NearestWalkablePoint(TerrainGrid terrain, Point2 p)
    {
        var cx = terrain.CellXOf(p.X);
        var cz = terrain.CellZOf(p.Z);
        if (terrain.IsWalkable(cx, cz))
        {
            return new Point2(Math.Clamp(p.X, 0, terrain.CellsX * terrain.CellSize), Math.Clamp(p.Z, 0, terrain.CellsZ * terrain.CellSize));
        }

        if (!TryNearestWalkable(terrain, cx, cz, out var nx, out var nz))
        {
            throw new InvalidOperationException("Le terrain n'a aucune cellule franchissable.");
        }

        return terrain.CellCenter(nx, nz);
    }
}
