namespace Golferoo.Sim.Terrain;

/// <summary>
/// Flaques : après la pluie, l'eau reste dans les creux du relief. La profondeur de cuvette de chaque cellule (niveau de
/// débordement moins hauteur du centre) vient d'un remplissage depuis les bords et les plans d'eau (« priority flood »),
/// recalculé seulement après une retouche du relief. Une cellule est en flaque si sa cuvette dépasse un seuil qui baisse
/// avec l'humidité du sol (<see cref="SoilGrid.Wetness"/>) : seuls les creux profonds tiennent jusqu'au lendemain.
/// Ni l'eau, ni le sable, ni le sous-bois ne portent de flaque. Le coût de marche n'en dépend pas.
/// </summary>
public sealed class Puddles
{
    private readonly TerrainGrid _terrain;
    private readonly double[] _depth;
    private readonly double[] _spill;
    private long _heightVersion = -1;

    public Puddles(TerrainGrid terrain)
    {
        _terrain = terrain;
        _depth = new double[terrain.CellsX * terrain.CellsZ];
        _spill = new double[_depth.Length];
    }

    /// <summary>Profondeur de cuvette minimale d'une flaque : 0,10 m sous la pluie du jour, 0,30 m le lendemain d'une forte pluie, aucune au sec.</summary>
    public static double Threshold(double wetness) => wetness <= 0.0 ? double.PositiveInfinity : 0.1 + (0.4 * (1.0 - wetness));

    /// <summary>Profondeur de cuvette de la cellule, en mètres.</summary>
    public double DepthAt(int cx, int cz)
    {
        Refresh();
        return _depth[(cz * _terrain.CellsX) + cx];
    }

    /// <summary>Niveau de l'eau de la flaque de la cellule (débordement moins le seuil), ou null si elle n'est pas en flaque.</summary>
    public double? LevelAt(int cx, int cz)
    {
        var threshold = Threshold(_terrain.Soil.Wetness);
        if (double.IsPositiveInfinity(threshold))
        {
            return null; // au sec, pas de calcul des cuvettes
        }

        Refresh();
        var i = (cz * _terrain.CellsX) + cx;
        var surface = _terrain.GetCellSurface(cx, cz);
        if (_depth[i] < threshold || surface is Surface.Water or Surface.Bunker or Surface.Trees)
        {
            return null;
        }

        return _spill[i] - threshold;
    }

    public bool IsPuddle(int cx, int cz) => LevelAt(cx, cz) is not null;

    /// <summary>
    /// Dégagement sans pénalité d'une balle arrêtée dans une flaque : centre de la cellule sèche et franchissable la plus proche,
    /// pas plus près du trou. Rend la balle telle quelle si elle n'est pas dans une flaque.
    /// </summary>
    public Point2 Relief(Point2 ball, Point2 hole)
    {
        var cx0 = _terrain.CellXOf(ball.X);
        var cz0 = _terrain.CellZOf(ball.Z);
        if (!IsPuddle(cx0, cz0))
        {
            return ball;
        }

        var limit = Dist2(ball, hole);
        for (var r = 1; r < Math.Max(_terrain.CellsX, _terrain.CellsZ); r++)
        {
            Point2? best = null;
            var bestD = double.MaxValue;
            for (var dz = -r; dz <= r; dz++)
            {
                for (var dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r)
                    {
                        continue;
                    }

                    var cx = cx0 + dx;
                    var cz = cz0 + dz;
                    if (cx < 0 || cz < 0 || cx >= _terrain.CellsX || cz >= _terrain.CellsZ || !_terrain.IsWalkable(cx, cz)
                        || _terrain.GetCellSurface(cx, cz) == Surface.Water || IsPuddle(cx, cz))
                    {
                        continue;
                    }

                    var p = _terrain.CellCenter(cx, cz);
                    var d = Dist2(p, ball);
                    if (Dist2(p, hole) >= limit && d < bestD)
                    {
                        best = p;
                        bestD = d;
                    }
                }
            }

            if (best is { } found)
            {
                return found;
            }
        }

        return ball;
    }

    private static double Dist2(Point2 a, Point2 b) => ((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z));

    private void Refresh()
    {
        if (_heightVersion == _terrain.HeightVersion)
        {
            return;
        }

        _heightVersion = _terrain.HeightVersion;
        var nx = _terrain.CellsX;
        var nz = _terrain.CellsZ;
        var h = _terrain.Heights;
        var center = new double[nx * nz];
        var done = new bool[nx * nz];
        var queue = new PriorityQueue<int, double>();
        for (var cz = 0; cz < nz; cz++)
        {
            for (var cx = 0; cx < nx; cx++)
            {
                var i = (cz * nx) + cx;
                center[i] = (h.GetCornerHeight(cx, cz) + h.GetCornerHeight(cx + 1, cz) + h.GetCornerHeight(cx, cz + 1) + h.GetCornerHeight(cx + 1, cz + 1)) * 0.25;
                // L'eau s'écoule par les bords de la carte et dans les plans d'eau.
                if (cx == 0 || cz == 0 || cx == nx - 1 || cz == nz - 1 || _terrain.GetCellSurface(cx, cz) == Surface.Water)
                {
                    done[i] = true;
                    _spill[i] = center[i];
                    queue.Enqueue(i, center[i]);
                }
            }
        }

        // Variante « priority flood + file » : une cellule sous le niveau courant (dans une cuvette) passe par une simple file,
        // sans le tas ; seules les cellules qui remontent paient le tas.
        var pit = new Queue<int>();
        while (pit.Count > 0 || queue.Count > 0)
        {
            var i = pit.Count > 0 ? pit.Dequeue() : queue.Dequeue();
            var level = _spill[i];
            var cx = i % nx;
            var cz = i / nx;
            if (cx > 0)
            {
                Visit(i - 1, level);
            }

            if (cx < nx - 1)
            {
                Visit(i + 1, level);
            }

            if (cz > 0)
            {
                Visit(i - nx, level);
            }

            if (cz < nz - 1)
            {
                Visit(i + nx, level);
            }
        }

        void Visit(int n, double level)
        {
            if (done[n])
            {
                return;
            }

            done[n] = true;
            if (center[n] <= level)
            {
                _spill[n] = level;
                pit.Enqueue(n);
            }
            else
            {
                _spill[n] = center[n];
                queue.Enqueue(n, center[n]);
            }
        }

        for (var i = 0; i < center.Length; i++)
        {
            _depth[i] = _spill[i] - center[i];
        }
    }
}
