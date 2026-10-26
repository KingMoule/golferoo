namespace Golferoo.Sim.Terrain;

/// <summary>Ce qui manque à un trou pour qu'on puisse l'ouvrir aux golfeurs.</summary>
public enum HoleIssue
{
    TeeTooSmall,
    FairwayTooShort,
    GreenTooSmall,
    TeeNotPlaced,
    HoleNotOnGreen,
    TooClose,
    NoPath,
}

/// <summary>
/// Validité d'un trou (C07) : fonction pure du terrain, qui rend la liste des manques, vide si le trou est jouable.
/// Seuils en mètres carrés ou mètres, lisibles par le tutoriel.
/// </summary>
public static class HoleCheck
{
    public const double MinTeeArea = 40.0;
    public const double MinFairwayArea = 500.0;
    public const double MinGreenArea = 300.0;
    public const double MinDistance = 50.0;

    public static List<HoleIssue> Check(TerrainGrid terrain)
    {
        var issues = new List<HoleIssue>();
        if (Area(terrain, Surface.Tee) < MinTeeArea)
        {
            issues.Add(HoleIssue.TeeTooSmall);
        }

        if (Area(terrain, Surface.Fairway) < MinFairwayArea)
        {
            issues.Add(HoleIssue.FairwayTooShort);
        }

        if (Area(terrain, Surface.Green) < MinGreenArea)
        {
            issues.Add(HoleIssue.GreenTooSmall);
        }

        var teeOk = terrain.SurfaceAt(terrain.Tee.X, terrain.Tee.Z) == Surface.Tee;
        var holeOk = terrain.SurfaceAt(terrain.Hole.X, terrain.Hole.Z) == Surface.Green;
        if (!teeOk)
        {
            issues.Add(HoleIssue.TeeNotPlaced);
        }

        if (!holeOk)
        {
            issues.Add(HoleIssue.HoleNotOnGreen);
        }

        if (teeOk && holeOk)
        {
            var dx = terrain.Hole.X - terrain.Tee.X;
            var dz = terrain.Hole.Z - terrain.Tee.Z;
            if ((dx * dx) + (dz * dz) < MinDistance * MinDistance)
            {
                issues.Add(HoleIssue.TooClose);
            }

            if (!Connected(terrain, terrain.Tee, terrain.Hole))
            {
                issues.Add(HoleIssue.NoPath);
            }
        }

        return issues;
    }

    /// <summary>Surface totale des cellules de <paramref name="surface"/>, en mètres carrés.</summary>
    public static double Area(TerrainGrid terrain, Surface surface)
    {
        var n = 0;
        foreach (var s in terrain.Surfaces)
        {
            if (s == surface)
            {
                n++;
            }
        }

        return n * terrain.CellSize * terrain.CellSize;
    }

    // Parcours en largeur sur les cellules franchissables, en quatre voisins.
    private static bool Connected(TerrainGrid terrain, Point2 from, Point2 to)
    {
        var start = (terrain.CellZOf(from.Z) * terrain.CellsX) + terrain.CellXOf(from.X);
        var goal = (terrain.CellZOf(to.Z) * terrain.CellsX) + terrain.CellXOf(to.X);
        if (!terrain.IsWalkable(terrain.CellXOf(from.X), terrain.CellZOf(from.Z)) || !terrain.IsWalkable(terrain.CellXOf(to.X), terrain.CellZOf(to.Z)))
        {
            return false;
        }

        var seen = new bool[terrain.CellsX * terrain.CellsZ];
        var queue = new Queue<int>();
        queue.Enqueue(start);
        seen[start] = true;
        while (queue.Count > 0)
        {
            var i = queue.Dequeue();
            if (i == goal)
            {
                return true;
            }

            var cx = i % terrain.CellsX;
            var cz = i / terrain.CellsX;
            foreach (var (nx, nz) in new[] { (cx + 1, cz), (cx - 1, cz), (cx, cz + 1), (cx, cz - 1) })
            {
                if (terrain.IsWalkable(nx, nz) && !seen[(nz * terrain.CellsX) + nx])
                {
                    seen[(nz * terrain.CellsX) + nx] = true;
                    queue.Enqueue((nz * terrain.CellsX) + nx);
                }
            }
        }

        return false;
    }
}
