using System.Globalization;
using System.Text.Json;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Terrain;

/// <summary>
/// Charge un parcours (C12). Un fichier de parcours donne la taille de la carte en tuiles, la surface de fond et une liste de trous :
/// fichier de trou (<c>data/holes/*.json</c>, chemin relatif au fichier de parcours), décalage en tuiles, rotation d'un quart de tour
/// (<c>rotation</c> de 0 à 3, sens horaire vu du dessus) et numéro. Un fichier de trou seul se charge comme un parcours d'un trou.
/// Des trous ne se chevauchent que sur du rough ou des bois ; sinon <see cref="HoleDataException"/>, avec le numéro du trou.
/// </summary>
public static class CourseLoader
{
    public static TerrainGrid Load(string path, int factor)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException e)
        {
            throw new HoleDataException($"Lecture impossible de '{path}' : {e.Message}", e);
        }

        return Parse(json, Path.GetDirectoryName(Path.GetFullPath(path))!, factor);
    }

    /// <summary>
    /// Parcours ou trou seul selon la présence de la liste <c>holes</c> ; <paramref name="baseDir"/> sert aux chemins des trous,
    /// sauf si <paramref name="readHole"/> lit le JSON d'un trou d'après son chemin relatif (jeu exporté : res://).
    /// </summary>
    public static TerrainGrid Parse(string json, string baseDir, int factor, Func<string, string>? readHole = null)
    {
        CourseFile file;
        try
        {
            file = JsonSerializer.Deserialize<CourseFile>(json, JsonOptions) ?? throw new HoleDataException("Le fichier de parcours est vide.");
        }
        catch (JsonException e)
        {
            throw new HoleDataException($"JSON invalide : {e.Message}", e);
        }

        return file.Holes is null ? HoleLoader.Parse(json, factor) : Build(file, baseDir, factor, readHole);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Même plafond que la lecture de <c>HeightField</c> : une carte plus grande ne se sauverait pas.</summary>
    private const int MaxCells = 4096;

    private static bool IsFree(Surface s) => s is Surface.Rough or Surface.Trees;

    private static TerrainGrid Build(CourseFile file, string baseDir, int factor, Func<string, string>? readHole)
    {
        if (file.TilesX < 1 || file.TilesZ < 1)
        {
            throw new HoleDataException($"Dimensions invalides : tilesX={file.TilesX}, tilesZ={file.TilesZ} (au moins 1 chacune).");
        }

        if (file.TilesX * (long)factor > MaxCells || file.TilesZ * (long)factor > MaxCells)
        {
            throw new HoleDataException($"Carte trop grande : {file.TilesX} × {file.TilesZ} tuiles au facteur {factor} (au plus {MaxCells} cellules par côté).");
        }

        var ground = Enum.GetNames<Surface>().FirstOrDefault(n => string.Equals(n, file.Ground ?? "Rough", StringComparison.OrdinalIgnoreCase));
        if (ground is null)
        {
            throw new HoleDataException($"Surface de fond inconnue : '{file.Ground}'.");
        }

        var list = file.Holes!;
        if (list.Count is < 1 or > Course.MaxHoles)
        {
            throw new HoleDataException($"Un parcours compte de 1 à {Course.MaxHoles} trous ({list.Count} trouvés).");
        }

        var numbers = list.Select(h => h.Number).Order().ToArray();
        for (var i = 0; i < numbers.Length; i++)
        {
            if (numbers[i] != i + 1)
            {
                throw new HoleDataException($"Numéros de trous : 1 à {list.Count} sans trou manquant ni double, trouvé {string.Join(", ", numbers)}.");
            }
        }

        var surface = Enum.Parse<Surface>(ground);
        var cellsX = file.TilesX * factor;
        var cellsZ = file.TilesZ * factor;
        var cells = new Surface[cellsX * cellsZ];
        Array.Fill(cells, surface);
        var corners = new double[(cellsX + 1) * (cellsZ + 1)];
        var locked = new bool[corners.Length];
        var trees = new List<TreePlacement>();
        var holes = new List<CourseHole>();
        double? tileSize = null;

        foreach (var entry in list.OrderBy(h => h.Number))
        {
            var n = entry.Number;
            if (entry.File is null || entry.Rotation is < 0 or > 3)
            {
                throw new HoleDataException($"Trou {n} : fichier absent ou rotation hors de 0 à 3.");
            }

            TerrainGrid source;
            try
            {
                source = readHole is null ? HoleLoader.Load(Path.Combine(baseDir, entry.File), factor) : HoleLoader.Parse(readHole(entry.File), factor);
            }
            catch (HoleDataException e)
            {
                throw new HoleDataException($"Trou {n} : {e.Message}", e);
            }

            if (source.Blank)
            {
                throw new HoleDataException($"Trou {n} : une terre vierge ne se pose pas dans un parcours.");
            }

            tileSize ??= source.CellSize * factor;
            if (Math.Abs(source.CellSize * factor - tileSize.Value) > 1e-9)
            {
                throw new HoleDataException($"Trou {n} : taille de tuile {source.CellSize * factor} m, {tileSize} m attendus comme pour le trou 1.");
            }

            var piece = Piece.From(source);
            for (var k = 0; k < entry.Rotation; k++)
            {
                piece = piece.Rotated();
            }

            var (ox, oz) = (entry.X * factor, entry.Z * factor);
            if (ox < 0 || oz < 0 || ox + piece.CellsX > cellsX || oz + piece.CellsZ > cellsZ)
            {
                throw new HoleDataException($"Trou {n} : sort de la carte ({piece.CellsX / factor} × {piece.CellsZ / factor} tuiles en ({entry.X}, {entry.Z}) sur {file.TilesX} × {file.TilesZ}).");
            }

            Paste(piece, ox, oz, n, factor, cells, cellsX, corners, locked);
            var (mx, mz) = (ox * source.CellSize, oz * source.CellSize);
            holes.Add(new CourseHole(n, Shift(piece.Tee, mx, mz), Shift(piece.Green, mx, mz), Shift(piece.Hole, mx, mz), source.Par));
            trees.AddRange(piece.Trees.Select(t => t with { X = t.X + mx, Z = t.Z + mz }));
        }

        OpenPassages(cells, cellsX, cellsZ, tileSize!.Value / factor, holes.SelectMany(h => new[] { h.Tee, h.Green }).Select(p => ((int)(p.X / (tileSize!.Value / factor)), (int)(p.Z / (tileSize.Value / factor)))).ToList());
        var heights = new HeightField(cellsX, cellsZ, tileSize!.Value / factor);
        for (var j = 0; j <= cellsZ; j++)
        {
            for (var i = 0; i <= cellsX; i++)
            {
                heights.SetCornerHeight(i, j, corners[(j * (cellsX + 1)) + i]);
            }
        }

        var terrain = new TerrainGrid(heights, file.Name ?? "sans nom", new Course(holes));
        for (var cz = 0; cz < cellsZ; cz++)
        {
            for (var cx = 0; cx < cellsX; cx++)
            {
                terrain.PaintCell(cx, cz, cells[(cz * cellsX) + cx]);
            }
        }

        terrain.IsolatedTrees = trees;
        return terrain;
    }

    private static Point2 Shift(Point2 p, double dx, double dz) => new(p.X + dx, p.Z + dz);

    /// <summary>Coût d'une cellule de bois dans <see cref="OpenPassages"/>, en cellules de marche : on perce un mur plutôt que d'en faire le tour dès que le détour dépasse ce nombre de cellules.</summary>
    private const int WoodCost = 25;

    /// <summary>Deux éléments (départ ou green) à moins de cette distance en mètres, sur chaque axe, sont aussi reliés : la balle d'un trou peut finir sur le green d'un autre.</summary>
    private const double PassageReach = 150;

    /// <summary>
    /// Les bordures de bois de trous voisins peuvent fermer un enclos que personne ne quitte à pied (sur « Le neuf », les trous 4 à 8), ou n'ouvrir qu'un détour de plus d'un kilomètre
    /// entre un green et le départ suivant. Dans l'ordre du jeu (départ, green, départ suivant…), le plus court chemin entre deux éléments consécutifs, un mur coûtant
    /// <see cref="WoodCost"/> cellules par cellule de bois, passe en rough là où il perce un mur. L'eau ne s'ouvre jamais.
    /// </summary>
    private static void OpenPassages(Surface[] cells, int cellsX, int cellsZ, double cellSize, List<(int X, int Z)> features)
    {
        var cost = new int[cells.Length];
        var from = new int[cells.Length];
        var near = PassageReach / cellSize;
        var pairs = from a in Enumerable.Range(0, features.Count)
                    from b in Enumerable.Range(a + 1, features.Count - a - 1)
                    where b == a + 1 || Math.Abs(features[a].X - features[b].X) <= near && Math.Abs(features[a].Z - features[b].Z) <= near
                    select (features[a], features[b]);
        foreach (var (first, second) in pairs)
        {
            var start = (first.Z * cellsX) + first.X;
            var goal = (second.Z * cellsX) + second.X;
            Array.Fill(cost, int.MaxValue);
            cost[start] = 0;
            from[start] = -1;
            var queue = new PriorityQueue<int, int>();
            queue.Enqueue(start, 0);
            while (queue.TryDequeue(out var i, out var c) && i != goal)
            {
                if (c > cost[i])
                {
                    continue;
                }

                var (x, z) = (i % cellsX, i / cellsX);
                foreach (var j in new[] { x > 0 ? i - 1 : -1, x < cellsX - 1 ? i + 1 : -1, z > 0 ? i - cellsX : -1, z < cellsZ - 1 ? i + cellsX : -1 })
                {
                    var step = j < 0 ? 0 : SurfaceTable.Get(cells[j]).Walkable ? 1 : cells[j] == Surface.Trees ? WoodCost : 0;
                    if (step > 0 && c + step < cost[j])
                    {
                        cost[j] = c + step;
                        from[j] = i;
                        queue.Enqueue(j, cost[j]);
                    }
                }
            }

            for (var i = goal; i >= 0 && cost[i] != int.MaxValue; i = from[i])
            {
                if (cells[i] == Surface.Trees)
                {
                    cells[i] = Surface.Rough;
                }
            }
        }
    }

    /// <summary>Pose le trou : rough et bois cèdent la place, deux surfaces de jeu au même endroit lèvent l'erreur. Les hauteurs d'un coin déjà pris par un trou précédent ne changent pas.</summary>
    private static void Paste(Piece p, int ox, int oz, int number, int factor, Surface[] cells, int cellsX, double[] corners, bool[] locked)
    {
        var width = cellsX + 1;
        for (var cz = 0; cz < p.CellsZ; cz++)
        {
            for (var cx = 0; cx < p.CellsX; cx++)
            {
                var src = p.Cells[(cz * p.CellsX) + cx];
                ref var dst = ref cells[((oz + cz) * cellsX) + ox + cx];
                if (!IsFree(src) && !IsFree(dst))
                {
                    throw new HoleDataException(string.Create(
                        CultureInfo.InvariantCulture,
                        $"Trou {number} : chevauche un autre trou en tuile ({(ox + cx) / factor}, {(oz + cz) / factor}) ({src} sur {dst})."));
                }

                if (!IsFree(src) || (src == Surface.Trees && dst == Surface.Rough))
                {
                    dst = src;
                }
            }
        }

        for (var j = 0; j <= p.CellsZ; j++)
        {
            for (var i = 0; i <= p.CellsX; i++)
            {
                var at = ((oz + j) * width) + ox + i;
                if (!locked[at])
                {
                    corners[at] = p.Corners[(j * (p.CellsX + 1)) + i];
                }
            }
        }

        for (var cz = 0; cz < p.CellsZ; cz++)
        {
            for (var cx = 0; cx < p.CellsX; cx++)
            {
                if (!IsFree(p.Cells[(cz * p.CellsX) + cx]))
                {
                    foreach (var (di, dj) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
                    {
                        locked[((oz + cz + dj) * width) + ox + cx + di] = true;
                    }
                }
            }
        }
    }

    /// <summary>Un trou en cellules, hors du terrain, pour le tourner avant de le poser.</summary>
    private sealed record Piece(int CellsX, int CellsZ, double CellSize, Surface[] Cells, double[] Corners, Point2 Tee, Point2 Green, Point2 Hole, List<TreePlacement> Trees)
    {
        public static Piece From(TerrainGrid t)
        {
            var corners = new double[(t.CellsX + 1) * (t.CellsZ + 1)];
            for (var j = 0; j <= t.CellsZ; j++)
            {
                for (var i = 0; i <= t.CellsX; i++)
                {
                    corners[(j * (t.CellsX + 1)) + i] = t.Heights.GetCornerHeight(i, j);
                }
            }

            return new Piece(t.CellsX, t.CellsZ, t.CellSize, t.Surfaces.ToArray(), corners, t.Tee, t.Green, t.Hole, [.. t.IsolatedTrees]);
        }

        /// <summary>Quart de tour horaire vu du dessus (x vers l'est, z vers le sud) : (x, z) devient (profondeur - z, x).</summary>
        public Piece Rotated()
        {
            var depth = CellsZ * CellSize;
            Point2 R(Point2 p) => new(depth - p.Z, p.X);
            var cells = new Surface[Cells.Length];
            for (var cz = 0; cz < CellsZ; cz++)
            {
                for (var cx = 0; cx < CellsX; cx++)
                {
                    cells[(cx * CellsZ) + (CellsZ - 1 - cz)] = Cells[(cz * CellsX) + cx];
                }
            }

            var corners = new double[Corners.Length];
            for (var j = 0; j <= CellsZ; j++)
            {
                for (var i = 0; i <= CellsX; i++)
                {
                    corners[(i * (CellsZ + 1)) + (CellsZ - j)] = Corners[(j * (CellsX + 1)) + i];
                }
            }

            return new Piece(CellsZ, CellsX, CellSize, cells, corners, R(Tee), R(Green), R(Hole), [.. Trees.Select(t => { var r = R(new Point2(t.X, t.Z)); return t with { X = r.X, Z = r.Z }; })]);
        }
    }

    private sealed class CourseFile
    {
        public string? Name { get; set; }

        public int TilesX { get; set; }

        public int TilesZ { get; set; }

        public string? Ground { get; set; }

        public List<HoleEntry>? Holes { get; set; }
    }

    private sealed class HoleEntry
    {
        public string? File { get; set; }

        public int X { get; set; }

        public int Z { get; set; }

        public int Rotation { get; set; }

        public int Number { get; set; }
    }
}
