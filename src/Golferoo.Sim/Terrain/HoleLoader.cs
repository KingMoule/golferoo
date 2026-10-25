using System.Globalization;
using System.Text.Json;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim.Terrain;

/// <summary>Données de trou invalides (message en français, précis sur la cause).</summary>
public sealed class HoleDataException : Exception
{
    public HoleDataException(string message)
        : base(message)
    {
    }

    public HoleDataException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

/// <summary>
/// Charge un trou JSON, décrit à la résolution des tuiles d'origine, vers une grille subdivisée d'un facteur f :
/// chaque tuile donne f × f cellules de sa surface, les hauteurs aux coins de cellules sont échantillonnées
/// sur les coins de tuiles par la même Catmull-Rom que <see cref="HeightField"/>.
/// </summary>
public static class HoleLoader
{
    public const int MinFactor = 1;
    public const int MaxFactor = 8;

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

        return Parse(json, factor);
    }

    public static TerrainGrid Parse(string json, int factor)
    {
        if (factor < MinFactor || factor > MaxFactor)
        {
            throw new ArgumentOutOfRangeException(nameof(factor), factor, $"Facteur de subdivision attendu entre {MinFactor} et {MaxFactor}.");
        }

        HoleFile file;
        try
        {
            file = JsonSerializer.Deserialize<HoleFile>(json, JsonOptions)
                ?? throw new HoleDataException("Le fichier de trou est vide.");
        }
        catch (JsonException e)
        {
            throw new HoleDataException($"JSON invalide : {e.Message}", e);
        }

        return Build(file, factor);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static TerrainGrid Build(HoleFile file, int factor)
    {
        var tilesX = file.TilesX;
        var tilesZ = file.TilesZ;
        if (tilesX < 1 || tilesZ < 1)
        {
            throw new HoleDataException($"Dimensions invalides : tilesX={tilesX}, tilesZ={tilesZ} (au moins 1 chacune).");
        }

        if (!(file.TileSize > 0))
        {
            throw new HoleDataException($"tileSize invalide : {file.TileSize} (positif attendu, en mètres).");
        }

        var legend = ParseLegend(file.Legend);
        var tileSurfaces = ParseSurfaces(file.Surfaces, tilesX, tilesZ, legend);
        var tileHeights = ParseHeights(file.Heights, tilesX, tilesZ);

        var tee = ParsePoint(file.Tee, "tee", tilesX, tilesZ);
        var green = ParsePoint(file.Green, "green", tilesX, tilesZ);
        var hole = ParsePoint(file.Hole, "hole", tilesX, tilesZ);
        RequireSurface(tileSurfaces, tilesX, tee, "tee", Surface.Tee);
        RequireSurface(tileSurfaces, tilesX, green, "green", Surface.Green);
        RequireSurface(tileSurfaces, tilesX, hole, "hole", Surface.Green);

        var cellsX = tilesX * factor;
        var cellsZ = tilesZ * factor;
        var heights = new HeightField(cellsX, cellsZ, file.TileSize / factor);
        for (var j = 0; j <= cellsZ; j++)
        {
            for (var i = 0; i <= cellsX; i++)
            {
                var h = CatmullRom.Evaluate(tileHeights, tilesX + 1, tilesZ + 1, (double)i / factor, (double)j / factor, out _, out _);
                heights.SetCornerHeight(i, j, h);
            }
        }

        var terrain = new TerrainGrid(
            heights,
            file.Name ?? "sans nom",
            ToWorld(tee, file.TileSize),
            ToWorld(green, file.TileSize),
            ToWorld(hole, file.TileSize));
        for (var cz = 0; cz < cellsZ; cz++)
        {
            for (var cx = 0; cx < cellsX; cx++)
            {
                terrain.PaintCell(cx, cz, tileSurfaces[((cz / factor) * tilesX) + (cx / factor)]);
            }
        }

        terrain.IsolatedTrees = ParseTrees(file.Trees, tilesX, tilesZ, file.TileSize);
        return terrain;
    }

    /// <summary>Arbres isolés : position en coordonnées de tuile (comme tee, green, hole), âge en années.</summary>
    private static List<TreePlacement> ParseTrees(List<TreeFile>? trees, int tilesX, int tilesZ, double tileSize)
    {
        var result = new List<TreePlacement>();
        if (trees is null)
        {
            return result;
        }

        for (var i = 0; i < trees.Count; i++)
        {
            var t = trees[i];
            var where = $"'trees[{i}]'";
            var species = TreeSpeciesTable.FindByName(t.Species ?? string.Empty)
                ?? throw new HoleDataException($"{where} : essence inconnue '{t.Species}'.");
            if (!(t.X >= 0 && t.X < tilesX && t.Z >= 0 && t.Z < tilesZ))
            {
                throw new HoleDataException(string.Create(CultureInfo.InvariantCulture, $"{where} hors carte : ({t.X}, {t.Z}) en tuiles."));
            }

            if (!(t.Age >= 0 && t.Age <= 1000))
            {
                throw new HoleDataException(string.Create(CultureInfo.InvariantCulture, $"{where} : âge invalide {t.Age} (années, entre 0 et 1000)."));
            }

            result.Add(new TreePlacement(t.X * tileSize, t.Z * tileSize, species.Id, (int)Math.Round(t.Age * 12.0)));
        }

        return result;
    }

    private static Point2 ToWorld(Point2 tile, double tileSize) => new(tile.X * tileSize, tile.Z * tileSize);

    private static Dictionary<char, Surface> ParseLegend(Dictionary<string, string>? legend)
    {
        if (legend is null || legend.Count == 0)
        {
            throw new HoleDataException("Légende des surfaces absente ('legend').");
        }

        var map = new Dictionary<char, Surface>();
        foreach (var (key, name) in legend)
        {
            if (key.Length != 1)
            {
                throw new HoleDataException($"Légende : la clé '{key}' doit être une seule lettre.");
            }

            // Nom exact d'une surface : Enum.TryParse accepterait aussi « 1 » ou « Tee, Green ».
            var match = Enum.GetNames<Surface>().FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            if (match is null || !Enum.TryParse<Surface>(match, out var surface))
            {
                throw new HoleDataException($"Légende : surface inconnue '{name}' pour la lettre '{key}'.");
            }

            map[key[0]] = surface;
        }

        return map;
    }

    private static Surface[] ParseSurfaces(string[]? rows, int tilesX, int tilesZ, Dictionary<char, Surface> legend)
    {
        if (rows is null || rows.Length != tilesZ)
        {
            throw new HoleDataException($"Dimensions incohérentes : {tilesZ} lignes de surfaces attendues, {rows?.Length ?? 0} trouvées.");
        }

        var result = new Surface[tilesX * tilesZ];
        for (var z = 0; z < tilesZ; z++)
        {
            var row = rows[z];
            if (row is null)
            {
                throw new HoleDataException($"Ligne de surfaces {z} absente (null).");
            }

            if (row.Length != tilesX)
            {
                throw new HoleDataException($"Dimensions incohérentes : la ligne de surfaces {z} a {row.Length} caractères, {tilesX} attendus.");
            }

            for (var x = 0; x < tilesX; x++)
            {
                if (!legend.TryGetValue(row[x], out var surface))
                {
                    throw new HoleDataException($"Lettre de surface inconnue '{row[x]}' à la tuile ({x}, {z}) : absente de la légende.");
                }

                result[(z * tilesX) + x] = surface;
            }
        }

        return result;
    }

    private static double[] ParseHeights(double[][]? rows, int tilesX, int tilesZ)
    {
        if (rows is null || rows.Length != tilesZ + 1)
        {
            throw new HoleDataException($"Dimensions incohérentes : {tilesZ + 1} lignes de hauteurs attendues, {rows?.Length ?? 0} trouvées.");
        }

        var result = new double[(tilesX + 1) * (tilesZ + 1)];
        for (var z = 0; z <= tilesZ; z++)
        {
            if (rows[z] is null || rows[z].Length != tilesX + 1)
            {
                throw new HoleDataException($"Dimensions incohérentes : la ligne de hauteurs {z} a {rows[z]?.Length ?? 0} valeurs, {tilesX + 1} attendues.");
            }

            for (var x = 0; x <= tilesX; x++)
            {
                if (!double.IsFinite(rows[z][x]))
                {
                    throw new HoleDataException($"Hauteur non finie au coin ({x}, {z}).");
                }

                result[(z * (tilesX + 1)) + x] = rows[z][x];
            }
        }

        return result;
    }

    private static Point2 ParsePoint(double[]? value, string name, int tilesX, int tilesZ)
    {
        if (value is null || value.Length != 2)
        {
            throw new HoleDataException($"'{name}' doit être [x, z] en coordonnées de tuile.");
        }

        var (x, z) = (value[0], value[1]);
        if (!(x >= 0 && x < tilesX && z >= 0 && z < tilesZ))
        {
            throw new HoleDataException(string.Create(
                CultureInfo.InvariantCulture,
                $"'{name}' hors carte : ({x}, {z}) pour une carte de {tilesX} × {tilesZ} tuiles."));
        }

        return new Point2(x, z);
    }

    private static void RequireSurface(Surface[] tiles, int tilesX, Point2 p, string name, Surface expected)
    {
        var actual = tiles[((int)p.Z * tilesX) + (int)p.X];
        if (actual != expected)
        {
            throw new HoleDataException(string.Create(
                CultureInfo.InvariantCulture,
                $"'{name}' en ({p.X}, {p.Z}) doit être sur une tuile {expected}, trouvé {actual}."));
        }
    }

    private sealed class HoleFile
    {
        public string? Name { get; set; }

        public double TileSize { get; set; }

        public int TilesX { get; set; }

        public int TilesZ { get; set; }

        public Dictionary<string, string>? Legend { get; set; }

        public double[][]? Heights { get; set; }

        public string[]? Surfaces { get; set; }

        public double[]? Tee { get; set; }

        public double[]? Green { get; set; }

        public double[]? Hole { get; set; }

        public List<TreeFile>? Trees { get; set; }
    }

    private sealed class TreeFile
    {
        public double X { get; set; }

        public double Z { get; set; }

        public string? Species { get; set; }

        public double Age { get; set; }
    }
}
