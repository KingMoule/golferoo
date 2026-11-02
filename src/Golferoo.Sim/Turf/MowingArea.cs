using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Turf;

/// <summary>
/// Où les tondeuses du rendu ont le droit de travailler (C26) : les cellules à moins de <see cref="Reach"/> m de l'axe départ-drapeau d'un trou ouvert.
/// Sans trou ouvert (propriété neuve), aucune cellule : personne ne tond une terre où rien n'est tracé.
/// </summary>
public static class MowingArea
{
    /// <summary>Largeur de part et d'autre de l'axe, en mètres : couvre le fairway, ses bords et les coudes d'un dogleg.</summary>
    public const double Reach = 60.0;

    /// <summary>Une case par cellule (rangée par rangée) : vrai si la cellule est tondable. <paramref name="isOpen"/> reçoit le numéro du trou (1 à N).</summary>
    public static bool[] Cells(TerrainGrid grid, Func<int, bool> isOpen)
    {
        var cells = new bool[grid.CellsX * grid.CellsZ];
        foreach (var hole in grid.Course.Holes.Where(h => isOpen(h.Number)))
        {
            var (a, b) = (hole.Tee, hole.Hole);
            var x0 = Math.Max(grid.CellXOf(Math.Min(a.X, b.X) - Reach), 0);
            var x1 = grid.CellXOf(Math.Max(a.X, b.X) + Reach);
            var z0 = Math.Max(grid.CellZOf(Math.Min(a.Z, b.Z) - Reach), 0);
            var z1 = grid.CellZOf(Math.Max(a.Z, b.Z) + Reach);
            for (var cz = z0; cz <= z1; cz++)
            {
                for (var cx = x0; cx <= x1; cx++)
                {
                    cells[(cz * grid.CellsX) + cx] |= DistanceToSegment(grid.CellCenter(cx, cz), a, b) <= Reach;
                }
            }
        }

        return cells;
    }

    private static double DistanceToSegment(Point2 p, Point2 a, Point2 b)
    {
        var (dx, dz) = (b.X - a.X, b.Z - a.Z);
        var length2 = (dx * dx) + (dz * dz);
        var t = length2 < 1e-9 ? 0.0 : Math.Clamp((((p.X - a.X) * dx) + ((p.Z - a.Z) * dz)) / length2, 0.0, 1.0);
        var (qx, qz) = (a.X + (t * dx) - p.X, a.Z + (t * dz) - p.Z);
        return Math.Sqrt((qx * qx) + (qz * qz));
    }
}
