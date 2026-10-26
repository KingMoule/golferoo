using Godot;
using Golferoo.Sim;
using Golferoo.Sim.Terrain;

namespace Golferoo.Game.Tutorial;

/// <summary>
/// Contours fantômes du tutoriel de C07 : le départ, le fairway et le green du coude, tracés en pointillé sur la terre vierge, plus un anneau
/// autour des arbres à raser. Indicatifs : le joueur peint où il veut. Lecture seule de la sim ; les contours viennent du fichier du vrai trou.
/// </summary>
public partial class GhostOutline : Node3D
{
    private static readonly (Surface Surface, Color Color)[] Outlined =
    [
        (Surface.Tee, new Color(1f, 1f, 1f)),
        (Surface.Fairway, new Color(0.55f, 1f, 0.5f)),
        (Surface.Green, new Color(0.3f, 1f, 0.7f)),
    ];

    private readonly Dictionary<Surface, MeshInstance3D> _outlines = [];
    private MeshInstance3D? _rings;
    private ImmediateMesh? _ringMesh;
    private Main? _main;

    public void Init(Main main, TerrainGrid reference)
    {
        _main = main;
        var grid = main.World.Terrain!;
        foreach (var (surface, color) in Outlined)
        {
            var mesh = new ImmediateMesh();
            BuildOutline(mesh, grid, reference, surface, color);
            var instance = Line(mesh, $"Outline{surface}");
            instance.Visible = false;
            _outlines[surface] = instance;
        }

        _ringMesh = new ImmediateMesh();
        _rings = Line(_ringMesh, "TreeRings");
        main.World.TreesEdited += _ => RefreshRings();
        RefreshRings();
    }

    /// <summary>N'affiche que les contours de <paramref name="surfaces"/>, et les anneaux si <paramref name="rings"/>.</summary>
    public void Show(IEnumerable<Surface> surfaces, bool rings)
    {
        var wanted = surfaces.ToHashSet();
        foreach (var (surface, instance) in _outlines)
        {
            instance.Visible = wanted.Contains(surface);
        }

        if (_rings is not null)
        {
            _rings.Visible = rings;
        }
    }

    private MeshInstance3D Line(ImmediateMesh mesh, string name)
    {
        var instance = new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                NoDepthTest = true,
                RenderPriority = 9,
            },
        };
        AddChild(instance);
        return instance;
    }

    // Arêtes de cellule entre la surface et son voisinage, une sur deux (pointillé).
    private static void BuildOutline(ImmediateMesh mesh, TerrainGrid grid, TerrainGrid reference, Surface surface, Color color)
    {
        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        mesh.SurfaceSetColor(color);
        var size = (float)reference.CellSize;
        for (var cz = 0; cz < reference.CellsZ; cz++)
        {
            for (var cx = 0; cx < reference.CellsX; cx++)
            {
                if (reference.GetCellSurface(cx, cz) != surface)
                {
                    continue;
                }

                var x0 = cx * size;
                var z0 = cz * size;
                var edges = new (int Dx, int Dz, float Ax, float Az, float Bx, float Bz)[]
                {
                    (1, 0, x0 + size, z0, x0 + size, z0 + size),
                    (-1, 0, x0, z0, x0, z0 + size),
                    (0, 1, x0, z0 + size, x0 + size, z0 + size),
                    (0, -1, x0, z0, x0 + size, z0),
                };
                foreach (var e in edges)
                {
                    var nx = cx + e.Dx;
                    var nz = cz + e.Dz;
                    var outside = nx < 0 || nz < 0 || nx >= reference.CellsX || nz >= reference.CellsZ || reference.GetCellSurface(nx, nz) != surface;
                    if (outside && ((cx + cz) % 2 == 0))
                    {
                        mesh.SurfaceAddVertex(new Vector3(e.Ax, (float)grid.Sample(e.Ax, e.Az) + 0.3f, e.Az));
                        mesh.SurfaceAddVertex(new Vector3(e.Bx, (float)grid.Sample(e.Bx, e.Bz) + 0.3f, e.Bz));
                    }
                }
            }
        }

        mesh.SurfaceEnd();
    }

    /// <summary>Un anneau rouge autour de chaque arbre du passage encore debout.</summary>
    private void RefreshRings()
    {
        if (_ringMesh is null || _main is null)
        {
            return;
        }

        var world = _main.World;
        var grid = world.Terrain!;
        _ringMesh.ClearSurfaces();
        var any = false;
        foreach (var p in grid.IsolatedTrees)
        {
            if (!world.Trees.Trees.Any(t => t.X == p.X && t.Z == p.Z))
            {
                continue;
            }

            if (!any)
            {
                _ringMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
                _ringMesh.SurfaceSetColor(new Color(1f, 0.3f, 0.25f));
                any = true;
            }

            const int segments = 32;
            const float radius = 5f;
            for (var i = 0; i < segments; i++)
            {
                foreach (var k in new[] { i, i + 1 })
                {
                    var a = k * Mathf.Tau / segments;
                    var x = (float)p.X + (Mathf.Cos(a) * radius);
                    var z = (float)p.Z + (Mathf.Sin(a) * radius);
                    _ringMesh.SurfaceAddVertex(new Vector3(x, (float)grid.Sample(x, z) + 0.4f, z));
                }
            }
        }

        if (any)
        {
            _ringMesh.SurfaceEnd();
        }
    }
}
