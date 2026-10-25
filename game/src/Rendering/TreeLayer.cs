using Godot;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Game.Rendering;

/// <summary>
/// Arbres de la simulation (<see cref="TreeStand"/>), à leur taille réelle, un MultiMesh par bloc de terrain et par variante.
/// Modèle : si <c>res://assets/trees/tree.glb</c> existe (produit par <c>tools/blender/tree_lowpoly.py</c> : c'est l'arbre
/// affiché, à retoucher là, un seul MeshInstance3D), il sert de modèle pour toutes les essences ; sinon quatre variantes
/// procédurales (<see cref="ProceduralMeshes.Tree"/>). Le modèle garde sa hauteur native (mesurée sur son AABB) ;
/// chaque arbre est mis à l'échelle de sa hauteur (verticale) et de son diamètre de couronne (horizontale), pied au sol.
/// Les matériaux du modèle sont remplacés par <c>tree.gdshader</c> (même couleur) qui trame les couronnes autour des
/// points focaux (<see cref="SetFocus"/>, <see cref="SetTrackedBall"/>).
/// </summary>
public partial class TreeLayer : Node3D
{
    public const string ExternalTreePath = "res://assets/trees/tree.glb";

    /// <summary>Emplacement du point focal du curseur.</summary>
    public const int CursorSlot = 0;

    /// <summary>Emplacement du point focal de la balle suivie.</summary>
    public const int BallSlot = 1;

    /// <summary>Rayon d'éclaircie autour du curseur, en mètres (à l'échelle de l'écran).</summary>
    public const float CursorFocusRadius = 6f;

    /// <summary>Rayon d'éclaircie autour de la balle suivie, en mètres.</summary>
    public const float BallFocusRadius = 4f;

    /// <summary>Hauteur au-dessus du sol du point focal (un golfeur : 1 m), en mètres.</summary>
    private const float FocusLift = 1f;

    private static readonly string[] FocusParameters = ["tree_focus_a", "tree_focus_b"];
    private static bool _globalsDeclared;
    private static Shader? _shader;

    private readonly record struct Model(Mesh Mesh, Vector3 Offset, float Height, float Width);

    private readonly Dictionary<(int X, int Z), Node3D> _blocks = [];
    private readonly Dictionary<(int X, int Z), int> _blockCounts = [];
    private readonly Dictionary<(int X, int Z), (double Height, double Crown)> _blockSums = [];
    private TerrainGrid? _grid;
    private TreeStand? _stand;
    private Model[] _models = [];
    private float _blockMeters;

    /// <summary>Arbres affichés (debout) dans tous les blocs.</summary>
    public int TreeCount { get; private set; }

    /// <summary>Nombre, hauteur moyenne et largeur de couronne moyenne des instances rendues, mesurées sur leurs transformations.</summary>
    public (int Count, double MeanHeight, double MeanCrown) RenderedStats()
    {
        var h = _blockSums.Values.Sum(v => v.Height);
        var c = _blockSums.Values.Sum(v => v.Crown);
        return (TreeCount, TreeCount == 0 ? 0 : h / TreeCount, TreeCount == 0 ? 0 : c / TreeCount);
    }

    /// <summary>Blocs de terrain reconstruits depuis la construction, hors construction initiale (cumul, pour les vérifications).</summary>
    public int BlockRebuilds { get; private set; }

    /// <summary>Vrai si l'arbre externe a été chargé.</summary>
    public bool UsesExternalTree { get; private set; }

    /// <summary>Hauteur native du modèle de la variante, en mètres (avant mise à l'échelle).</summary>
    public float NativeHeight(int variant = 0) => _models[variant % _models.Length].Height;

    /// <summary>Construit tous les blocs depuis la sim. <paramref name="blockCells"/> : côté d'un bloc de terrain, en cellules.</summary>
    public void Build(TerrainGrid grid, TreeStand stand, int blockCells)
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        EnsureGlobals();
        _grid = grid;
        _stand = stand;
        _blockMeters = (float)(blockCells * grid.CellSize);
        _blocks.Clear();
        _blockCounts.Clear();
        _blockSums.Clear();
        _models = LoadModels(out var external);
        UsesExternalTree = external;

        var keys = new HashSet<(int, int)>();
        var bx = (int)Math.Ceiling(grid.CellsX * grid.CellSize / _blockMeters);
        var bz = (int)Math.Ceiling(grid.CellsZ * grid.CellSize / _blockMeters);
        for (var j = 0; j < bz; j++)
        {
            for (var i = 0; i < bx; i++)
            {
                keys.Add((i, j));
            }
        }

        BuildBlocks(keys);
        BlockRebuilds = 0;
    }

    /// <summary>
    /// Reconstruit les blocs touchés par le rectangle de cellules [x0, x1) x [z0, z1) : arbres plantés ou retirés
    /// par une peinture, ou sol modifié sous les troncs. Rend le nombre de blocs reconstruits.
    /// </summary>
    public int RebuildCells(int x0, int z0, int x1, int z1)
    {
        if (_grid is null || x1 <= x0 || z1 <= z0)
        {
            return 0;
        }

        var cell = (float)_grid.CellSize;
        var keys = new HashSet<(int, int)>();
        for (var j = (int)(z0 * cell / _blockMeters); j <= (int)(((z1 * cell) - 0.001f) / _blockMeters); j++)
        {
            for (var i = (int)(x0 * cell / _blockMeters); i <= (int)(((x1 * cell) - 0.001f) / _blockMeters); i++)
            {
                keys.Add((i, j));
            }
        }

        BuildBlocks(keys);
        BlockRebuilds += keys.Count;
        return keys.Count;
    }

    /// <summary>Point focal à éclaircir (monde, au sol) ou null pour l'éteindre.</summary>
    public void SetFocus(int slot, Vector3? groundPoint, float radius)
    {
        EnsureGlobals();
        var v = groundPoint is { } p ? new Vector4(p.X, p.Y + FocusLift, p.Z, radius) : Vector4.Zero;
        RenderingServer.GlobalShaderParameterSet(FocusParameters[slot], v);
    }

    /// <summary>Entrée pour la balle suivie par la caméra (quand elle existera) : sa position 3D, ou null.</summary>
    public void SetTrackedBall(Vector3? ballPosition) => SetFocus(BallSlot, ballPosition, BallFocusRadius);

    private static void EnsureGlobals()
    {
        if (_globalsDeclared)
        {
            return;
        }

        foreach (var name in FocusParameters)
        {
            RenderingServer.GlobalShaderParameterAdd(name, RenderingServer.GlobalShaderParameterType.Vec4, Vector4.Zero);
        }

        _globalsDeclared = true;
    }

    // Coût : un passage sur tous les arbres de la sim par appel (O(N) quel que soit le nombre de blocs reconstruits).
    // Suffisant pour quelques milliers d'arbres ; à remplacer par une requête par bloc de l'index spatial pour F6.
    private void BuildBlocks(HashSet<(int X, int Z)> keys)
    {
        var grid = _grid!;
        var lists = new Dictionary<(int X, int Z), List<Transform3D>[]>();
        foreach (var key in keys)
        {
            if (_blocks.Remove(key, out var old))
            {
                RemoveChild(old);
                old.QueueFree();
            }

            _blockCounts[key] = 0;
            _blockSums[key] = (0, 0);
            lists[key] = new List<Transform3D>[_models.Length];
        }

        foreach (var t in _stand!.Trees)
        {
            if (t.State != TreeState.Standing)
            {
                continue;
            }

            var key = ((int)(t.X / _blockMeters), (int)(t.Z / _blockMeters));
            if (!lists.TryGetValue(key, out var perVariant))
            {
                continue;
            }

            var h = Hash(t.Id, 17);
            var variant = _models.Length == 1 ? 0 : (int)(h % (uint)_models.Length);
            var model = _models[variant];
            var yaw = (((h >> 8) & 0xFF) / 255f) * Mathf.Tau;
            var sy = (float)t.Height() / model.Height;
            var sxz = (float)t.CrownDiameter() / model.Width;
            var basis = Basis.FromEuler(new Vector3(0, yaw, 0)) * Basis.FromScale(new Vector3(sxz, sy, sxz));
            var y = (float)grid.Sample(t.X, t.Z);
            var place = new Transform3D(basis, new Vector3((float)t.X, y, (float)t.Z));
            (perVariant[variant] ??= []).Add(place * new Transform3D(Basis.Identity, model.Offset));
            _blockCounts[key]++;
            var sums = _blockSums[key];
            _blockSums[key] = (sums.Height + (basis.Y.Length() * model.Height), sums.Crown + (basis.X.Length() * model.Width));
        }

        foreach (var (key, perVariant) in lists)
        {
            var block = new Node3D { Name = $"Block{key.X}_{key.Z}" };
            for (var v = 0; v < perVariant.Length; v++)
            {
                if (perVariant[v] is { Count: > 0 } transforms)
                {
                    block.AddChild(MakeInstance($"Trees{v}", _models[v].Mesh, transforms));
                }
            }

            AddChild(block);
            _blocks[key] = block;
        }

        TreeCount = _blockCounts.Values.Sum();
    }

    private static MultiMeshInstance3D MakeInstance(string name, Mesh mesh, List<Transform3D> transforms)
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

        return new MultiMeshInstance3D { Name = name, Multimesh = multi };
    }

    private static Model[] LoadModels(out bool external)
    {
        var mesh = LoadExternalMesh();
        external = mesh is not null;
        if (mesh is not null)
        {
            return [Describe(mesh)];
        }

        var result = new Model[ProceduralMeshes.TreeVariants];
        for (var v = 0; v < result.Length; v++)
        {
            result[v] = Describe(ProceduralMeshes.Tree(v));
        }

        return result;
    }

    /// <summary>Mesure le modèle (AABB) et remplace ses matériaux par le shader des arbres, à couleur égale.</summary>
    private static Model Describe(Mesh mesh)
    {
        _shader ??= GD.Load<Shader>("res://shaders/tree.gdshader");
        for (var s = 0; s < mesh.GetSurfaceCount(); s++)
        {
            if (mesh is not ArrayMesh array || mesh.SurfaceGetMaterial(s) is ShaderMaterial)
            {
                continue;
            }

            var std = mesh.SurfaceGetMaterial(s) as BaseMaterial3D;
            var material = new ShaderMaterial { Shader = _shader };
            material.SetShaderParameter("albedo", std?.AlbedoColor ?? new Color(0.25f, 0.5f, 0.25f));
            material.SetShaderParameter("use_vertex_color", std?.VertexColorUseAsAlbedo ?? false);
            material.SetShaderParameter("roughness_value", std?.Roughness ?? 1f);
            array.SurfaceSetMaterial(s, material);
        }

        var aabb = mesh.GetAabb();
        var center = aabb.GetCenter();
        return new Model(mesh, new Vector3(-center.X, -aabb.Position.Y, -center.Z), aabb.Size.Y, Math.Max(Math.Max(aabb.Size.X, aabb.Size.Z), 0.01f));
    }

    /// <summary>Maillage du .glb, ou null s'il manque, est illisible, plat, ou ne contient pas exactement un MeshInstance3D.</summary>
    private static Mesh? LoadExternalMesh()
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
            var found = new List<MeshInstance3D>();
            CollectMeshes(root, found);
            if (found.Count != 1)
            {
                GD.PushWarning($"{ExternalTreePath} : {found.Count} MeshInstance3D au lieu d'un seul (à fusionner dans Blender), arbres procéduraux utilisés");
                return null;
            }

            var mesh = found[0].Mesh;
            var aabb = mesh.GetAabb();
            if (aabb.Size.Y <= 0f)
            {
                GD.PushWarning($"{ExternalTreePath} : maillage plat, arbres procéduraux utilisés");
                return null;
            }

            GD.Print($"ASSET {ExternalTreePath} externe {mesh.GetSurfaceCount()} surfaces, hauteur native {aabb.Size.Y:0.###} m, largeur {Math.Max(aabb.Size.X, aabb.Size.Z):0.###} m");
            return mesh;
        }
        finally
        {
            root.Free();
        }
    }

    private static void CollectMeshes(Node node, List<MeshInstance3D> found)
    {
        if (node is MeshInstance3D { Mesh: not null } mi)
        {
            found.Add(mi);
        }

        foreach (var child in node.GetChildren())
        {
            CollectMeshes(child, found);
        }
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
