using Godot;
using Golferoo.Sim.Terrain;

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
    private ShaderMaterial? _terrainMaterial;
    private ShaderMaterial? _waterMaterial;
    private TreeLayer? _trees;

    /// <summary>Sommets par cellule dans chaque direction (plus fin que la grille).</summary>
    public int VerticesPerCell { get; set; } = 2;

    public int ChunksX { get; private set; }

    public int ChunksZ { get; private set; }

    public int ChunkCount => ChunksX * ChunksZ;

    /// <summary>Construit tout le terrain depuis la grille.</summary>
    public void Build(TerrainGrid grid)
    {
        _grid = grid;
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
        _textureA = ImageTexture.CreateFromImage(_imageA);
        _textureB = ImageTexture.CreateFromImage(_imageB);

        _terrainMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/terrain.gdshader") };
        _terrainMaterial.SetShaderParameter("weights_a", _textureA);
        _terrainMaterial.SetShaderParameter("weights_b", _textureB);
        _terrainMaterial.SetShaderParameter("grid_size", new Vector2((float)(grid.CellsX * grid.CellSize), (float)(grid.CellsZ * grid.CellSize)));
        _waterMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/water.gdshader") };

        for (var j = 0; j < ChunksZ; j++)
        {
            for (var i = 0; i < ChunksX; i++)
            {
                BuildChunk(i, j);
            }
        }

        _trees = new TreeLayer { Name = "Trees" };
        AddChild(_trees);
        _trees.Build(grid);
    }

    /// <summary>
    /// Reconstruit les blocs touchés par le rectangle de cellules modifiées. La Catmull-Rom d'une cellule lit les coins
    /// c-1 à c+2 : modifier les coins d'une cellule change la hauteur jusqu'à 2 cellules autour, d'où la marge de 2,
    /// qui peut déborder sur le bloc voisin.
    /// </summary>
    public void RebuildChunks(Rect2I cellRect)
    {
        if (_grid is null)
        {
            return;
        }

        var x0 = Math.Max(cellRect.Position.X - 2, 0);
        var z0 = Math.Max(cellRect.Position.Y - 2, 0);
        var x1 = Math.Min(cellRect.End.X + 2, _grid.CellsX);
        var z1 = Math.Min(cellRect.End.Y + 2, _grid.CellsZ);
        if (x1 <= x0 || z1 <= z0)
        {
            return;
        }

        UpdateWeights(x0, z0, x1, z1);
        _textureA!.Update(_imageA);
        _textureB!.Update(_imageB);
        for (var j = z0 / ChunkCells; j <= (z1 - 1) / ChunkCells; j++)
        {
            for (var i = x0 / ChunkCells; i <= (x1 - 1) / ChunkCells; i++)
            {
                BuildChunk(i, j);
            }
        }

        _trees?.Build(_grid);
    }

    private void UpdateWeights(int x0, int z0, int x1, int z1)
    {
        for (var cz = z0; cz < z1; cz++)
        {
            for (var cx = x0; cx < x1; cx++)
            {
                var s = (int)_grid!.GetCellSurface(cx, cz);
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
        ReplaceChunk(_waterChunks, ci, cj, $"Water_{ci}_{cj}", BuildWaterMesh(grid, cx0, cz0, nx, nz), _waterMaterial!);
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

    private static ArrayMesh? BuildWaterMesh(TerrainGrid grid, int cx0, int cz0, int nx, int nz)
    {
        var cells = new List<(int X, int Z)>();
        var level = double.MinValue;
        for (var cz = cz0; cz < cz0 + nz; cz++)
        {
            for (var cx = cx0; cx < cx0 + nx; cx++)
            {
                if (grid.GetCellSurface(cx, cz) != Surface.Water)
                {
                    continue;
                }

                var c = grid.CellCenter(cx, cz);
                level = Math.Max(level, grid.Sample(c.X, c.Z));
                cells.Add((cx, cz));
            }
        }

        if (cells.Count == 0)
        {
            return null;
        }

        var y = (float)level + 0.05f;
        var vertices = new Vector3[cells.Count * 4];
        var normals = new Vector3[cells.Count * 4];
        var indices = new int[cells.Count * 6];
        var cs = (float)grid.CellSize;
        for (var n = 0; n < cells.Count; n++)
        {
            var (cx, cz) = cells[n];
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
