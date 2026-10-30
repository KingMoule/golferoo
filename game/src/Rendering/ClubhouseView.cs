using Godot;
using Golferoo.Sim.Buildings;
using Golferoo.Sim.Terrain;

namespace Golferoo.Game.Rendering;

/// <summary>
/// Le pavillon : le modèle <c>clubhouse_pavilion_v2.glb</c>, mis à l'échelle de l'emprise de la sim et tourné pour que sa façade
/// (+Z dans le modèle) regarde la porte ; refait quand la sim déplace le pavillon. Porte aussi l'aperçu de l'emprise (valide ou non)
/// du déplacement. Aucune règle ici : tout vient de <see cref="Building"/>.
/// </summary>
public partial class ClubhouseView : Node3D
{
    public const string ModelPath = "res://assets/buildings/clubhouse_pavilion_v2.glb";

    private TerrainGrid _grid = null!;
    private Node3D? _model;
    private Aabb _bounds;
    private Building? _shown;
    private MeshInstance3D _preview = null!;
    private StandardMaterial3D _previewMaterial = null!;

    /// <summary>Le pavillon actuellement rendu ; nul avant la première mise à jour ou sans pavillon.</summary>
    public Building? Shown => _shown;

    /// <summary>Centre du modèle dans le monde (au centre de l'emprise de la sim), pour les tests.</summary>
    public Vector3 ModelCenter => _model is null ? Vector3.Zero : _model.Transform * _bounds.GetCenter();

    public bool PreviewVisible => _preview.Visible;

    public bool PreviewValid { get; private set; }

    public void Build(TerrainGrid grid)
    {
        _grid = grid;
        if (GD.Load<PackedScene>(ModelPath)?.Instantiate() is Node3D model)
        {
            _model = model;
            AddChild(model);
            _bounds = BoundsOf(model);
        }

        _previewMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        _preview = new MeshInstance3D { Name = "ClubhousePreview", Mesh = new BoxMesh(), MaterialOverride = _previewMaterial, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_preview);
    }

    /// <summary>Pose le modèle sur <paramref name="building"/> ; ne fait rien si le pavillon n'a pas changé.</summary>
    public void Update(Building? building)
    {
        if (building is null || building == _shown || _model is null)
        {
            return;
        }

        _shown = building;
        var cell = _grid.CellSize;
        var center = building.Position(cell);
        var width = (building.Footprint.MaxX - building.Footprint.MinX + 1) * cell;
        var depth = (building.Footprint.MaxZ - building.Footprint.MinZ + 1) * cell;
        var side = building.Side is DoorSide.East or DoorSide.West;

        // Le modèle (façade +Z) tient dans l'emprise tournée selon la porte.
        var fit = (float)Math.Min((side ? depth : width) / _bounds.Size.X, (side ? width : depth) / _bounds.Size.Z);
        var yaw = building.Side switch
        {
            DoorSide.North => Mathf.Pi,
            DoorSide.East => Mathf.Pi / 2f,
            DoorSide.West => -Mathf.Pi / 2f,
            _ => 0f,
        };
        var basis = Basis.FromEuler(new Vector3(0f, yaw, 0f)).Scaled(Vector3.One * fit);
        var mid = _bounds.GetCenter();
        var ground = GroundOf(building);
        _model.Transform = new Transform3D(basis, new Vector3((float)center.X, ground, (float)center.Z) - (basis * new Vector3(mid.X, _bounds.Position.Y, mid.Z)));
    }

    /// <summary>Montre l'emprise de <paramref name="building"/> en vert (valide) ou rouge (refusée).</summary>
    public void ShowPreview(Building building, bool valid)
    {
        var cell = _grid.CellSize;
        var center = building.Position(cell);
        var size = new Vector3((float)((building.Footprint.MaxX - building.Footprint.MinX + 1) * cell), 3f, (float)((building.Footprint.MaxZ - building.Footprint.MinZ + 1) * cell));
        ((BoxMesh)_preview.Mesh).Size = size;
        _preview.Position = new Vector3((float)center.X, GroundOf(building) + (size.Y / 2f), (float)center.Z);
        _previewMaterial.AlbedoColor = valid ? new Color(0.25f, 0.85f, 0.35f, 0.35f) : new Color(0.9f, 0.2f, 0.2f, 0.35f);
        PreviewValid = valid;
        _preview.Visible = true;
    }

    public void HidePreview() => _preview.Visible = false;

    /// <summary>Le sol sous l'emprise : sa plus haute cellule, pour que le bâtiment ne s'enfonce jamais.</summary>
    private float GroundOf(Building b)
    {
        var f = b.Footprint;
        var cell = _grid.CellSize;
        var high = double.MinValue;
        foreach (var (x, z) in new[] { (f.MinX, f.MinZ), (f.MaxX, f.MinZ), (f.MinX, f.MaxZ), (f.MaxX, f.MaxZ), ((f.MinX + f.MaxX) / 2, (f.MinZ + f.MaxZ) / 2) })
        {
            high = Math.Max(high, _grid.Sample((x + 0.5) * cell, (z + 0.5) * cell));
        }

        return (float)high;
    }

    private static Aabb BoundsOf(Node3D root)
    {
        var bounds = new Aabb();
        var first = true;
        foreach (var node in root.FindChildren("*", "MeshInstance3D", true, false))
        {
            var mesh = (MeshInstance3D)node;
            var box = mesh.GetAabb();
            var local = mesh.Transform;
            for (var p = mesh.GetParent(); p is Node3D parent && parent != root; p = p.GetParent())
            {
                local = parent.Transform * local;
            }

            box = local * box;
            bounds = first ? box : bounds.Merge(box);
            first = false;
        }

        return bounds;
    }
}
