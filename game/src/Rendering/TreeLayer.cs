using Godot;
using Golferoo.Sim.Terrain;

namespace Golferoo.Game.Rendering;

/// <summary>
/// Arbres, un par cellule d'arbres, via un MultiMesh par variante. Si <c>res://assets/trees/tree.glb</c> existe,
/// son premier MeshInstance3D sert d'unique modèle (pied à l'origine, environ 4 m) ; sinon quatre variantes procédurales.
/// Variante, rotation, échelle et décalage dépendent seulement du hachage de la cellule.
/// </summary>
public partial class TreeLayer : Node3D
{
    public const string ExternalTreePath = "res://assets/trees/tree.glb";

    public int TreeCount { get; private set; }

    /// <summary>Vrai si l'arbre externe a été chargé.</summary>
    public bool UsesExternalTree { get; private set; }

    public void Build(TerrainGrid grid)
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        var external = LoadExternalTree();
        UsesExternalTree = external is not null;
        var variants = external is null ? ProceduralMeshes.TreeVariants : 1;
        var lists = new List<Transform3D>[variants];
        for (var i = 0; i < variants; i++)
        {
            lists[i] = new List<Transform3D>();
        }

        var count = 0;
        for (var cz = 0; cz < grid.CellsZ; cz++)
        {
            for (var cx = 0; cx < grid.CellsX; cx++)
            {
                if (grid.GetCellSurface(cx, cz) != Surface.Trees)
                {
                    continue;
                }

                var h = Hash(cx, cz);
                var jx = (((h & 0xFF) / 255f) - 0.5f) * (float)grid.CellSize * 0.6f;
                var jz = ((((h >> 8) & 0xFF) / 255f) - 0.5f) * (float)grid.CellSize * 0.6f;
                var scale = 0.8f + ((((h >> 16) & 0xFF) / 255f) * 0.7f);
                var yaw = (((h >> 24) & 0xFF) / 255f) * Mathf.Tau;
                var variant = (int)(Hash(cz, cx) % (uint)variants);
                var c = grid.CellCenter(cx, cz);
                var x = (float)c.X + jx;
                var z = (float)c.Z + jz;
                var y = (float)grid.Sample(x, z);
                var basis = Basis.FromEuler(new Vector3(0, yaw, 0)).Scaled(new Vector3(scale, scale, scale));
                lists[variant].Add(new Transform3D(basis, new Vector3(x, y, z)));
                count++;
            }
        }

        TreeCount = count;
        for (var i = 0; i < variants; i++)
        {
            if (lists[i].Count > 0)
            {
                AddLayer($"Trees{i}", external ?? ProceduralMeshes.Tree(i), lists[i]);
            }
        }
    }

    /// <summary>Premier MeshInstance3D du .glb, ou null s'il manque ou ne contient aucun maillage.</summary>
    private static Mesh? LoadExternalTree()
    {
        if (!ResourceLoader.Exists(ExternalTreePath))
        {
            return null;
        }

        var root = GD.Load<PackedScene>(ExternalTreePath)?.Instantiate();
        if (root is null)
        {
            GD.PushWarning($"{ExternalTreePath} : scène illisible, arbres procéduraux utilisés");
            return null;
        }

        try
        {
            var mesh = FindMesh(root)?.Mesh;
            if (mesh is null)
            {
                GD.PushWarning($"{ExternalTreePath} : aucun MeshInstance3D, arbres procéduraux utilisés");
            }
            else
            {
                GD.Print($"ASSET {ExternalTreePath} externe {mesh.GetSurfaceCount()} surfaces");
            }

            return mesh;
        }
        finally
        {
            root.Free();
        }
    }

    private static MeshInstance3D? FindMesh(Node node)
    {
        if (node is MeshInstance3D { Mesh: not null } mi)
        {
            return mi;
        }

        foreach (var child in node.GetChildren())
        {
            var found = FindMesh(child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private void AddLayer(string name, Mesh mesh, List<Transform3D> transforms)
    {
        var multi = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = mesh,
            InstanceCount = transforms.Count,
        };
        for (var i = 0; i < transforms.Count; i++)
        {
            multi.SetInstanceTransform(i, transforms[i]);
        }

        AddChild(new MultiMeshInstance3D { Name = name, Multimesh = multi });
    }

    private static uint Hash(int x, int z)
    {
        unchecked
        {
            var h = ((uint)x * 73856093u) ^ ((uint)z * 19349663u);
            h ^= h >> 13;
            h *= 0x5bd1e995u;
            h ^= h >> 15;
            return h;
        }
    }
}
