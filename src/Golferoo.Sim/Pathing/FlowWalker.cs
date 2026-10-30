using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Pathing;

public enum WalkResult
{
    Moving,
    Arrived,

    /// <summary>La cellule n'est pas reliée à la destination : l'appelant décide (nouvelle destination, téléportation).</summary>
    Stuck,
}

/// <summary>Déplacement d'un golfeur le long d'un champ de flux.</summary>
public static class FlowWalker
{
    /// <summary>Vitesse de marche de base en m/s, sur fairway : trois fois le pas réel (1,3 m/s), c'est un jeu de simulation (Guillaume, 2026-10-26).</summary>
    public const double BaseSpeed = 3.9;

    /// <summary>
    /// Avance d'un pas de <paramref name="dt"/> secondes vers <paramref name="target"/> en suivant <paramref name="field"/>
    /// (calculé vers la cellule du point, ou la plus proche franchissable), à <see cref="BaseSpeed"/> × <paramref name="speedFactor"/>. Ne pose jamais le golfeur sur une cellule infranchissable.
    /// </summary>
    public static WalkResult Advance(TerrainGrid terrain, FlowField field, ref Point2 pos, Point2 target, double dt, double speedFactor = 1.0)
    {
        var cx = terrain.CellXOf(pos.X);
        var cz = terrain.CellZOf(pos.Z);
        if (!terrain.IsWalkable(cx, cz))
        {
            pos = GridQueries.NearestWalkablePoint(terrain, pos);
            cx = terrain.CellXOf(pos.X);
            cz = terrain.CellZOf(pos.Z);
        }

        var cost = SurfaceTable.Get(terrain.GetCellSurface(cx, cz)).WalkCost;
        var step = BaseSpeed * speedFactor / cost * dt;

        if (cx == field.DestX && cz == field.DestZ)
        {
            var aim = terrain.CellXOf(target.X) == cx && terrain.CellZOf(target.Z) == cz ? target : terrain.CellCenter(cx, cz);
            var ex = aim.X - pos.X;
            var ez = aim.Z - pos.Z;
            var dist = Math.Sqrt((ex * ex) + (ez * ez));
            if (dist <= step)
            {
                pos = aim;
                return WalkResult.Arrived;
            }

            pos = new Point2(pos.X + (ex / dist * step), pos.Z + (ez / dist * step));
            return WalkResult.Moving;
        }

        if (!field.IsReachable(cx, cz))
        {
            return WalkResult.Stuck;
        }

        if (field.TryDirection(pos.X, pos.Z, out var dx, out var dz) && TryMove(terrain, ref pos, dx, dz, step))
        {
            return WalkResult.Moving;
        }

        if (field.TryCellDirection(cx, cz, out dx, out dz) && TryMove(terrain, ref pos, dx, dz, step))
        {
            return WalkResult.Moving;
        }

        return WalkResult.Moving; // bloqué un tick par le garde-fou ; la direction de cellule finit toujours par passer
    }

    private static bool TryMove(TerrainGrid terrain, ref Point2 pos, double dx, double dz, double step)
    {
        var cx = terrain.CellXOf(pos.X);
        var cz = terrain.CellZOf(pos.Z);
        if (Allowed(terrain, cx, cz, pos.X + (dx * step), pos.Z + (dz * step)))
        {
            pos = new Point2(pos.X + (dx * step), pos.Z + (dz * step));
            return true;
        }

        // On glisse le long de l'obstacle : composante x seule, puis z seule.
        if (Allowed(terrain, cx, cz, pos.X + (dx * step), pos.Z))
        {
            pos = new Point2(pos.X + (dx * step), pos.Z);
            return true;
        }

        if (Allowed(terrain, cx, cz, pos.X, pos.Z + (dz * step)))
        {
            pos = new Point2(pos.X, pos.Z + (dz * step));
            return true;
        }

        return false;
    }

    private static bool Allowed(TerrainGrid terrain, int cx, int cz, double nx, double nz)
    {
        var mx = terrain.CellXOf(nx);
        var mz = terrain.CellZOf(nz);
        if (nx < 0 || nz < 0 || nx >= terrain.CellsX * terrain.CellSize || nz >= terrain.CellsZ * terrain.CellSize)
        {
            return false;
        }

        if (mx == cx && mz == cz)
        {
            return true;
        }

        if (!terrain.IsWalkable(mx, mz))
        {
            return false;
        }

        return mx == cx || mz == cz || (terrain.IsWalkable(mx, cz) && terrain.IsWalkable(cx, mz));
    }
}
