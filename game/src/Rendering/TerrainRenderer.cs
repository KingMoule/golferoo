using Godot;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;
using Golferoo.Sim.Trees;

namespace Golferoo.Game.Rendering;

/// <summary>
/// Rendu du terrain : un <see cref="ArrayMesh"/> par bloc de 32 x 32 cellules, un maillage d'eau plat par bloc,
/// et deux textures de poids (un texel par cellule) lues par le shader pour choisir la surface.
/// Lit uniquement <see cref="TerrainGrid"/> ; aucune règle de jeu ici.
/// </summary>
public partial class TerrainRenderer : Node3D
{
    public const int ChunkCells = 32;

    private TerrainGrid? _grid;
    private MeshInstance3D?[,] _terrainChunks = new MeshInstance3D?[0, 0];
    private MeshInstance3D?[,] _waterChunks = new MeshInstance3D?[0, 0];
    private Image? _imageA;
    private Image? _imageB;
    private ImageTexture? _textureA;
    private ImageTexture? _textureB;
    private ImageTexture? _turfTexture;
    private long _turfVersion = -1;
    private ulong _turfUploadMs;
    private ShaderMaterial? _terrainMaterial;
    private ShaderMaterial? _waterMaterial;
    private ShaderMaterial? _puddleMaterial;
    private float[] _pondLevels = [];
    private TreeLayer? _trees;

    /// <summary>Sommets par cellule dans chaque direction (plus fin que la grille).</summary>
    public int VerticesPerCell { get; set; } = 2;

    public int ChunksX { get; private set; }

    public int ChunksZ { get; private set; }

    public int ChunkCount => ChunksX * ChunksZ;

    /// <summary>Blocs reconstruits par <see cref="RebuildChunks"/> depuis la construction (cumul, pour les vérifications).</summary>
    public int RebuiltChunks { get; private set; }

    /// <summary>Couche d'arbres (lit les arbres de la simulation).</summary>
    public TreeLayer? Trees => _trees;

    /// <summary>Fleurs de printemps dans le rough.</summary>
    public FlowerLayer? Flowers { get; private set; }

    /// <summary>Construit tout le terrain depuis la grille, et les arbres depuis le <paramref name="stand"/> de la simulation.</summary>
    public void Build(TerrainGrid grid, TreeStand stand)
    {
        _grid = grid;
        _puddles = null;
        _puddlesFor = (-1, -1, -1);
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        ChunksX = (grid.CellsX + ChunkCells - 1) / ChunkCells;
        ChunksZ = (grid.CellsZ + ChunkCells - 1) / ChunkCells;
        _terrainChunks = new MeshInstance3D?[ChunksX, ChunksZ];
        _waterChunks = new MeshInstance3D?[ChunksX, ChunksZ];

        _imageA = Image.CreateEmpty(grid.CellsX, grid.CellsZ, false, Image.Format.Rgba8);
        _imageB = Image.CreateEmpty(grid.CellsX, grid.CellsZ, false, Image.Format.Rgba8);
        UpdateWeights(0, 0, grid.CellsX, grid.CellsZ);
        _pondLevels = new float[grid.CellsX * grid.CellsZ];
        UpdatePondLevels();
        _textureA = ImageTexture.CreateFromImage(_imageA);
        _textureB = ImageTexture.CreateFromImage(_imageB);
        _turfTexture = ImageTexture.CreateFromImage(TurfImage());
        _turfVersion = grid.Turf.Version;
        _turfUploadMs = Time.GetTicksMsec();

        _terrainMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/terrain.gdshader") };
        _terrainMaterial.SetShaderParameter("turf_state", _turfTexture);
        var water = GD.Load<Shader>("res://shaders/water.gdshader");
        _waterMaterial = new ShaderMaterial { Shader = water };
        _waterMaterial.SetShaderParameter("shore_mask", true);
        _puddleMaterial = new ShaderMaterial { Shader = water };
        foreach (var material in new[] { _terrainMaterial, _waterMaterial })
        {
            // Terrain et plans d'eau lisent le même champ des surfaces : la rive de l'un est celle de l'autre.
            material.SetShaderParameter("weights_a", _textureA);
            material.SetShaderParameter("weights_b", _textureB);
            material.SetShaderParameter("grid_size", new Vector2((float)(grid.CellsX * grid.CellSize), (float)(grid.CellsZ * grid.CellSize)));
        }

        for (var j = 0; j < ChunksZ; j++)
        {
            for (var i = 0; i < ChunksX; i++)
            {
                BuildChunk(i, j);
            }
        }

        Flowers = new FlowerLayer { Name = "Flowers" };
        AddChild(Flowers);
        Flowers.Init(grid);

        _trees = new TreeLayer { Name = "Trees" };
        AddChild(_trees);
        _trees.Build(grid, stand, ChunkCells);
    }

    /// <summary>Teinte de l'herbe par saison (multipliée aux couleurs du shader de terrain), indexée par <see cref="Season"/> : l'été est la référence.</summary>
    private static readonly Vector3[] SeasonTints =
    [
        new(0.95f, 1.08f, 0.85f), // printemps : vert vif
        new(1f, 1f, 1f),
        new(1.12f, 1.02f, 0.75f), // automne : un peu jaune
        new(1.05f, 0.9f, 0.8f), // hiver : terne
    ];

    private (double Wetness, long Heights, long Surfaces) _puddlesFor = (-1, -1, -1);
    private MeshInstance3D? _puddles;

    /// <summary>Cellules en flaque affichées.</summary>
    public int PuddleCells { get; private set; }

    /// <summary>
    /// Redessine les flaques (un carré d'eau par cellule, au niveau de sa flaque, shader d'eau) si l'humidité, le relief
    /// ou les surfaces ont changé : au plus une fois par jour de jeu hors retouches. Rend vrai s'il a fallu redessiner.
    /// </summary>
    public bool RefreshPuddles()
    {
        if (_grid is null)
        {
            return false;
        }

        var key = (_grid.Soil.Wetness, _grid.HeightVersion, _grid.SurfaceVersion);
        if (key == _puddlesFor)
        {
            return false;
        }

        _puddlesFor = key;
        _puddles?.QueueFree();
        _puddles = null;
        var cells = new List<(int X, int Z, float Y)>();
        if (_grid.Soil.Wetness > 0)
        {
            for (var cz = 0; cz < _grid.CellsZ; cz++)
            {
                for (var cx = 0; cx < _grid.CellsX; cx++)
                {
                    if (_grid.Puddles.LevelAt(cx, cz) is { } level)
                    {
                        cells.Add((cx, cz, (float)level));
                    }
                }
            }
        }

        PuddleCells = cells.Count;
        if (cells.Count > 0)
        {
            _puddles = new MeshInstance3D { Name = "Puddles", Mesh = Quads(_grid, cells), MaterialOverride = _puddleMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(_puddles);
        }

        return true;
    }

    /// <summary>Surcouche d'état du gazon (touche H) : rouge usé, vert sain, jaune pour l'herbe trop haute.</summary>
    public void SetTurfOverlay(bool on) => _terrainMaterial?.SetShaderParameter("overlay", on ? 1f : 0f);

    /// <summary>
    /// Envoie l'état du gazon au GPU : tout de suite si la hauteur ou les consignes ont changé (jour, consigne),
    /// sinon au plus une fois par seconde (l'usure change à chaque pas de golfeur).
    /// </summary>
    public void RefreshTurf()
    {
        if (_grid is null || _turfTexture is null)
        {
            return;
        }

        var now = Time.GetTicksMsec();
        if (_grid.Turf.Version == _turfVersion && now - _turfUploadMs < 1000)
        {
            return;
        }

        _turfVersion = _grid.Turf.Version;
        _turfUploadMs = now;
        _turfTexture.Update(TurfImage());
    }

    /// <summary>RG8, un texel par cellule : R hauteur brute (64 = référence, soit 0,25), G usure.</summary>
    private Image TurfImage()
    {
        var turf = _grid!.Turf;
        var data = new byte[_grid.CellsX * _grid.CellsZ * 2];
        var i = 0;
        for (var cz = 0; cz < _grid.CellsZ; cz++)
        {
            for (var cx = 0; cx < _grid.CellsX; cx++)
            {
                data[i++] = turf.Height(cx, cz);
                data[i++] = turf.Wear(cx, cz);
            }
        }

        return Image.CreateFromData(_grid.CellsX, _grid.CellsZ, false, Image.Format.Rg8, data);
    }

    /// <summary>Sol mouillé : le terrain s'assombrit (0 sec, 1 pluie du jour).</summary>
    public void SetWetness(float wetness) => _terrainMaterial?.SetShaderParameter("wetness", wetness);

    /// <summary>
    /// Couleurs du jour : teinte de l'herbe et fleurs selon la saison, feuillage des arbres selon la date (<see cref="TreeLayer.SetFoliage"/>).
    /// Rend le nombre de blocs d'arbres reconstruits.
    /// </summary>
    public int SetDate(Date date)
    {
        SetSeason(date.Season);
        return _trees?.SetFoliage(Foliage.Position(date)) ?? 0;
    }

    /// <summary>Teinte de l'herbe et fleurs de la saison.</summary>
    public void SetSeason(Season season)
    {
        _terrainMaterial?.SetShaderParameter("season_tint", SeasonTints[(int)season]);
        Flowers?.SetSpring(season == Season.Spring);
    }

    /// <summary>
    /// Reconstruit les blocs touchés par le rectangle de cellules modifiées. La Catmull-Rom d'une cellule lit les coins
    /// c-1 à c+2 : modifier les coins d'une cellule change la hauteur jusqu'à 2 cellules autour, d'où la marge de 2,
    /// qui peut déborder sur le bloc voisin. Les arbres des mêmes blocs sont refaits aussi (plantés, retirés par la peinture,
    /// ou troncs reposés sur un sol modifié). Rend le nombre de blocs de terrain reconstruits.
    /// </summary>
    public int RebuildChunks(Rect2I cellRect)
    {
        if (_grid is null)
        {
            return 0;
        }

        var x0 = Math.Max(cellRect.Position.X - 2, 0);
        var z0 = Math.Max(cellRect.Position.Y - 2, 0);
        var x1 = Math.Min(cellRect.End.X + 2, _grid.CellsX);
        var z1 = Math.Min(cellRect.End.Y + 2, _grid.CellsZ);
        if (x1 <= x0 || z1 <= z0)
        {
            return 0;
        }

        UpdateWeights(x0, z0, x1, z1);
        _textureA!.Update(_imageA);
        _textureB!.Update(_imageB);
        var rebuilt = 0;
        for (var j = z0 / ChunkCells; j <= (z1 - 1) / ChunkCells; j++)
        {
            for (var i = x0 / ChunkCells; i <= (x1 - 1) / ChunkCells; i++)
            {
                BuildChunk(i, j);
                rebuilt++;
            }
        }

        // Un étang à cheval sur plusieurs blocs change de niveau partout à la fois.
        foreach (var (i, j) in UpdatePondLevels())
        {
            BuildWater(i, j);
        }

        RebuiltChunks += rebuilt;
        _trees?.RebuildCells(x0, z0, x1, z1);
        Flowers?.Invalidate();

        return rebuilt;
    }

    /// <summary>Met à jour les textures de poids.</summary>
    private void UpdateWeights(int x0, int z0, int x1, int z1)
    {
        for (var cz = z0; cz < z1; cz++)
        {
            for (var cx = x0; cx < x1; cx++)
            {
                var surface = _grid!.GetCellSurface(cx, cz);
                var s = (int)surface;
                var a = s < 4 ? OneHot(s) : new Color(0, 0, 0, 0);
                var b = s >= 4 ? OneHot(s - 4) : new Color(0, 0, 0, 0);
                _imageA!.SetPixel(cx, cz, a);
                _imageB!.SetPixel(cx, cz, b);
            }
        }
    }

    private static Color OneHot(int channel) => new(
        channel == 0 ? 1f : 0f,
        channel == 1 ? 1f : 0f,
        channel == 2 ? 1f : 0f,
        channel == 3 ? 1f : 0f);

    private void BuildChunk(int ci, int cj)
    {
        var grid = _grid!;
        var cx0 = ci * ChunkCells;
        var cz0 = cj * ChunkCells;
        var nx = Math.Min(ChunkCells, grid.CellsX - cx0);
        var nz = Math.Min(ChunkCells, grid.CellsZ - cz0);

        ReplaceChunk(_terrainChunks, ci, cj, $"Terrain_{ci}_{cj}", BuildTerrainMesh(grid, cx0, cz0, nx, nz), _terrainMaterial!);
        BuildWater(ci, cj);
    }

    private void BuildWater(int ci, int cj)
    {
        var grid = _grid!;
        var cx0 = ci * ChunkCells;
        var cz0 = cj * ChunkCells;
        var nx = Math.Min(ChunkCells, grid.CellsX - cx0);
        var nz = Math.Min(ChunkCells, grid.CellsZ - cz0);
        ReplaceChunk(_waterChunks, ci, cj, $"Water_{ci}_{cj}", BuildWaterMesh(grid, _pondLevels, cx0, cz0, nx, nz), _waterMaterial!);
    }

    /// <summary>
    /// Niveau de chaque étang (cellules d'eau reliées par un côté) : le plus haut centre de ses cellules, plus 10 cm (le shader d'eau coupe la rive, le plan peut dépasser un peu du sol) ; NaN hors de l'eau.
    /// Un seul niveau par étang, même à cheval sur plusieurs blocs : sinon la rive suit les marches du fond. Rend les blocs dont le niveau a changé.
    /// </summary>
    private HashSet<(int I, int J)> UpdatePondLevels()
    {
        var grid = _grid!;
        var (w, h) = (grid.CellsX, grid.CellsZ);
        var levels = new float[w * h];
        Array.Fill(levels, float.NaN);
        var pond = new List<int>();
        var stack = new Stack<int>();
        for (var start = 0; start < levels.Length; start++)
        {
            if (!float.IsNaN(levels[start]) || grid.GetCellSurface(start % w, start / w) != Surface.Water)
            {
                continue;
            }

            pond.Clear();
            levels[start] = 0f;
            stack.Push(start);
            var top = double.MinValue;
            while (stack.TryPop(out var c))
            {
                pond.Add(c);
                var center = grid.CellCenter(c % w, c / w);
                top = Math.Max(top, grid.Sample(center.X, center.Z));
                Visit(c % w > 0 ? c - 1 : -1);
                Visit(c % w < w - 1 ? c + 1 : -1);
                Visit(c - w);
                Visit(c + w);
            }

            foreach (var c in pond)
            {
                levels[c] = (float)top + 0.1f;
            }
        }

        void Visit(int n)
        {
            if (n >= 0 && n < levels.Length && float.IsNaN(levels[n]) && grid.GetCellSurface(n % w, n / w) == Surface.Water)
            {
                levels[n] = 0f;
                stack.Push(n);
            }
        }

        var changed = new HashSet<(int I, int J)>();
        for (var c = 0; c < levels.Length; c++)
        {
            if (levels[c].Equals(_pondLevels[c]))
            {
                continue;
            }

            // Le plan déborde d'une cellule : la voisine peut tomber dans le bloc d'à côté.
            for (var dz = -1; dz <= 1; dz++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var (x, z) = ((c % w) + dx, (c / w) + dz);
                    if (x >= 0 && z >= 0 && x < w && z < h)
                    {
                        changed.Add((x / ChunkCells, z / ChunkCells));
                    }
                }
            }
        }

        _pondLevels = levels;
        return changed;
    }

    private void ReplaceChunk(MeshInstance3D?[,] store, int ci, int cj, string name, ArrayMesh? mesh, Material material)
    {
        // Retirer avant de libérer : sinon le nouveau bloc hérite d'un nom renommé par Godot.
        if (store[ci, cj] is { } old)
        {
            RemoveChild(old);
            old.QueueFree();
        }

        store[ci, cj] = null;
        if (mesh is null)
        {
            return;
        }

        var instance = new MeshInstance3D { Name = name, Mesh = mesh, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(instance);
        store[ci, cj] = instance;
    }

    private ArrayMesh BuildTerrainMesh(TerrainGrid grid, int cx0, int cz0, int nx, int nz)
    {
        var v = Math.Max(1, VerticesPerCell);
        var vx = (nx * v) + 1;
        var vz = (nz * v) + 1;
        var step = grid.CellSize / v;
        var vertices = new Vector3[vx * vz];
        var normals = new Vector3[vx * vz];
        for (var k = 0; k < vz; k++)
        {
            var z = (cz0 * grid.CellSize) + (k * step);
            for (var i = 0; i < vx; i++)
            {
                var x = (cx0 * grid.CellSize) + (i * step);
                var n = grid.SampleNormal(x, z);
                vertices[(k * vx) + i] = new Vector3((float)x, (float)grid.Sample(x, z), (float)z);
                normals[(k * vx) + i] = new Vector3((float)n.X, (float)n.Y, (float)n.Z);
            }
        }

        var indices = new int[(vx - 1) * (vz - 1) * 6];
        var p = 0;
        for (var k = 0; k < vz - 1; k++)
        {
            for (var i = 0; i < vx - 1; i++)
            {
                var a = (k * vx) + i;
                var b = a + 1;
                var c = a + vx;
                var d = c + 1;
                indices[p++] = a;
                indices[p++] = b;
                indices[p++] = c;
                indices[p++] = b;
                indices[p++] = d;
                indices[p++] = c;
            }
        }

        return MakeMesh(vertices, normals, indices);
    }

    /// <summary>
    /// Plan d'eau du bloc : les cellules d'eau et celles qui les bordent, car la rive lissée du shader passe en dehors des cellules
    /// d'eau aux coins rentrants. Le shader d'eau découpe la rive ; chaque cellule prend le niveau de l'étang voisin (<paramref name="levels"/>).
    /// </summary>
    private static ArrayMesh? BuildWaterMesh(TerrainGrid grid, float[] levels, int cx0, int cz0, int nx, int nz)
    {
        var cells = new List<(int X, int Z, float Y)>();
        for (var cz = cz0; cz < cz0 + nz; cz++)
        {
            for (var cx = cx0; cx < cx0 + nx; cx++)
            {
                var level = float.NaN;
                for (var dz = -1; dz <= 1; dz++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var (x, z) = (cx + dx, cz + dz);
                        var y = x >= 0 && z >= 0 && x < grid.CellsX && z < grid.CellsZ ? levels[(z * grid.CellsX) + x] : float.NaN;
                        if (!float.IsNaN(y) && !(y <= level))
                        {
                            level = y;
                        }
                    }
                }

                if (!float.IsNaN(level))
                {
                    cells.Add((cx, cz, level));
                }
            }
        }

        if (cells.Count == 0)
        {
            return null;
        }

        return Quads(grid, cells);
    }

    /// <summary>Un carré horizontal par cellule, à la hauteur donnée.</summary>
    private static ArrayMesh Quads(TerrainGrid grid, List<(int X, int Z, float Y)> cells)
    {
        var vertices = new Vector3[cells.Count * 4];
        var normals = new Vector3[cells.Count * 4];
        var indices = new int[cells.Count * 6];
        var cs = (float)grid.CellSize;
        for (var n = 0; n < cells.Count; n++)
        {
            var (cx, cz, y) = cells[n];
            var x = cx * cs;
            var z = cz * cs;
            var o = n * 4;
            vertices[o] = new Vector3(x, y, z);
            vertices[o + 1] = new Vector3(x + cs, y, z);
            vertices[o + 2] = new Vector3(x, y, z + cs);
            vertices[o + 3] = new Vector3(x + cs, y, z + cs);
            for (var q = 0; q < 4; q++)
            {
                normals[o + q] = Vector3.Up;
            }

            var t = n * 6;
            indices[t] = o;
            indices[t + 1] = o + 1;
            indices[t + 2] = o + 2;
            indices[t + 3] = o + 1;
            indices[t + 4] = o + 3;
            indices[t + 5] = o + 2;
        }

        return MakeMesh(vertices, normals, indices);
    }

    private static ArrayMesh MakeMesh(Vector3[] vertices, Vector3[] normals, int[] indices)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}
