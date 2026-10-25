using Godot;
using Golferoo.Sim.Terrain;

namespace Golferoo.Game.Rendering;

/// <summary>
/// Fleurs de printemps dans le rough : touffes <c>flowers_spring_01</c> à <c>03</c> (<c>res://assets/bushes/</c>), une sur
/// <see cref="CellsPerTuft"/> cellules de rough choisies par hachage de la cellule (stable d'une partie à l'autre), en MultiMesh.
/// Le matériau <c>Blossom</c> (blanc cassé) prend la couleur de l'instance ; le feuillage garde la sienne. Visible au printemps
/// seulement ; rien dans la sim. Sans les modèles, la couche reste vide.
/// </summary>
public partial class FlowerLayer : Node3D
{
    /// <summary>Une touffe pour tant de cellules de rough, en moyenne.</summary>
    public const int CellsPerTuft = 3;

    /// <summary>Les touffes font 0,5 m : on les grossit pour qu'elles se lisent à la distance de la caméra.</summary>
    private const float MinScale = 2.0f;
    private const float MaxScale = 3.2f;

    private static readonly Color[] Blossoms = [new(1f, 0.85f, 0.2f), new(1f, 1f, 1f), new(1f, 0.55f, 0.75f), new(0.65f, 0.5f, 1f)];

    private TerrainGrid? _grid;
    private Mesh?[] _meshes = [];
    private bool _dirty = true;

    /// <summary>Touffes posées (0 tant que la couche est cachée ou vide).</summary>
    public int Count { get; private set; }

    public void Init(TerrainGrid grid)
    {
        _grid = grid;
        _meshes = [.. Enumerable.Range(1, 3).Select(v => LoadMesh($"res://assets/bushes/flowers_spring_{v:00}.glb"))];
        Visible = false;
    }

    /// <summary>Montre les fleurs au printemps, les cache sinon ; les replace si le terrain a changé entre-temps.</summary>
    public void SetSpring(bool spring)
    {
        Visible = spring;
        if (spring && _dirty)
        {
            Rebuild();
        }
    }

    /// <summary>Le terrain a été retouché : refait les touffes tout de suite si elles sont visibles, sinon au prochain printemps.</summary>
    public void Invalidate()
    {
        _dirty = true;
        if (Visible)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        _dirty = false;
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        Count = 0;
        if (_grid is null)
        {
            return;
        }

        var lists = _meshes.Select(_ => new List<(Transform3D Place, Color Color)>()).ToArray();
        for (var cz = 0; cz < _grid.CellsZ; cz++)
        {
            for (var cx = 0; cx < _grid.CellsX; cx++)
            {
                var h = Hash(cx, cz);
                if (_grid.GetCellSurface(cx, cz) != Surface.Rough || h % CellsPerTuft != 0)
                {
                    continue;
                }

                var variant = (int)((h >> 4) % (uint)_meshes.Length);
                if (_meshes[variant] is null)
                {
                    continue;
                }

                var x = (float)((cx + (((h >> 8) & 0xFF) / 255.0)) * _grid.CellSize);
                var z = (float)((cz + (((h >> 16) & 0xFF) / 255.0)) * _grid.CellSize);
                var scale = MinScale + ((MaxScale - MinScale) * (((h >> 24) & 0x0F) / 15f));
                var yaw = ((h >> 28) & 0x0F) / 16f * Mathf.Tau;
                var basis = Basis.FromEuler(new Vector3(0, yaw, 0)).Scaled(new Vector3(scale, scale, scale));
                lists[variant].Add((new Transform3D(basis, new Vector3(x, (float)_grid.Sample(x, z), z)), Blossoms[(h >> 2) % (uint)Blossoms.Length]));
            }
        }

        for (var v = 0; v < lists.Length; v++)
        {
            if (_meshes[v] is not { } mesh || lists[v].Count == 0)
            {
                continue;
            }

            var multi = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true,
                Mesh = mesh,
                InstanceCount = lists[v].Count,
            };
            for (var i = 0; i < lists[v].Count; i++)
            {
                multi.SetInstanceTransform(i, lists[v][i].Place);
                multi.SetInstanceColor(i, lists[v][i].Color);
            }

            AddChild(new MultiMeshInstance3D { Name = $"Flowers{v}", Multimesh = multi, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            Count += lists[v].Count;
        }
    }

    /// <summary>Maillage du .glb (un seul MeshInstance3D), avec le matériau <c>Blossom</c> teinté par la couleur de l'instance ; null s'il manque.</summary>
    private static Mesh? LoadMesh(string path)
    {
        if (!ResourceLoader.Exists(path) || GD.Load<PackedScene>(path)?.Instantiate() is not { } root)
        {
            return null;
        }

        try
        {
            var found = root.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().Select(m => m.Mesh).FirstOrDefault()
                ?? (root as MeshInstance3D)?.Mesh;
            if (found is not ArrayMesh mesh)
            {
                return null;
            }

            for (var s = 0; s < mesh.GetSurfaceCount(); s++)
            {
                if (mesh.SurfaceGetMaterial(s) is BaseMaterial3D material && material.ResourceName == "Blossom")
                {
                    var tinted = (BaseMaterial3D)material.Duplicate();
                    tinted.VertexColorUseAsAlbedo = true;
                    mesh.SurfaceSetMaterial(s, tinted);
                }
            }

            return mesh;
        }
        finally
        {
            root.Free();
        }
    }

    private static uint Hash(int x, int z)
    {
        unchecked
        {
            var h = ((uint)x * 2654435761u) ^ ((uint)z * 2246822519u);
            h ^= h >> 15;
            h *= 0x2c1b3c6du;
            h ^= h >> 12;
            return h;
        }
    }
}
