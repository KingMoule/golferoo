using Godot;
using Golferoo.Sim.Buildings;
using Golferoo.Sim.Terrain;

namespace Golferoo.Game.Rendering;

/// <summary>
/// Les bâtiments posés par le joueur (C17) : un modèle par bâtiment de la sim, à sa position et tourné vers sa porte (même ajustement que le pavillon,
/// <see cref="ClubhouseView.Fit"/>), refait quand la liste de la sim change. Porte aussi le fantôme de l'outil de pose : le modèle du genre choisi,
/// teinté en vert s'il peut être posé, en rouge sinon. Aucune règle ici : tout vient de <see cref="Building"/>.
/// </summary>
public partial class BuildingLayer : Node3D
{
    private readonly List<Building> _shown = [];
    private readonly List<(Node3D Node, Vector3 Center)> _models = [];
    private readonly Dictionary<BuildingKind, (Node3D Node, Aabb Bounds)> _ghosts = [];
    private TerrainGrid _grid = null!;
    private StandardMaterial3D _ghostMaterial = null!;
    private Node3D? _ghost;
    private MeshInstance3D? _doorMarker;

    /// <summary>Nombre de modèles de bâtiments rendus, pour les tests.</summary>
    public int ShownCount => _shown.Count;

    /// <summary>Centres des modèles rendus dans le monde, pour les tests.</summary>
    public IEnumerable<Vector3> ModelCenters => _models.Select(m => m.Node.Transform * m.Center);

    public bool GhostVisible => _ghost is { Visible: true };

    public bool GhostValid { get; private set; }

    public static string ModelPath(BuildingKind kind) => kind switch
    {
        BuildingKind.SnackBar => "res://assets/buildings/snack_bar_01.glb",
        BuildingKind.ProShop => "res://assets/buildings/pro_shop_01.glb",
        BuildingKind.MaintenanceShed => "res://assets/buildings/maintenance_shed_01.glb",
        BuildingKind.Bench => "res://assets/furniture/bench_01.glb",
        _ => "res://assets/buildings/clubhouse_pavilion_v2.glb",
    };

    public void Build(TerrainGrid grid)
    {
        _grid = grid;
        _ghostMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
    }

    /// <summary>Remet les modèles d'aplomb avec la liste de la sim ; ne fait rien tant qu'elle n'a pas changé.</summary>
    public void Update(IReadOnlyList<Building> placed)
    {
        if (placed.Count == _shown.Count && placed.Select((b, i) => ReferenceEquals(b, _shown[i])).All(same => same))
        {
            return;
        }

        foreach (var (node, _) in _models)
        {
            node.QueueFree();
        }

        _models.Clear();
        _shown.Clear();
        _shown.AddRange(placed);
        foreach (var building in placed)
        {
            if (Instantiate(building.Kind) is not { } model)
            {
                continue;
            }

            var bounds = ClubhouseView.BoundsOf(model);
            model.Transform = ClubhouseView.Fit(_grid, bounds, building);
            _models.Add((model, bounds.GetCenter()));
            AddChild(model);
        }
    }

    /// <summary>Montre le fantôme de <paramref name="building"/>, vert (<paramref name="valid"/>) ou rouge.</summary>
    public void ShowGhost(Building building, bool valid)
    {
        if (!_ghosts.TryGetValue(building.Kind, out var entry))
        {
            if (Instantiate(building.Kind) is not { } model)
            {
                return;
            }

            model.Name = "Ghost";
            foreach (var mesh in model.FindChildren("*", "MeshInstance3D", true, false).Cast<MeshInstance3D>())
            {
                mesh.MaterialOverride = _ghostMaterial;
                mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }

            entry = (model, ClubhouseView.BoundsOf(model));
            _ghosts[building.Kind] = entry;
            AddChild(model);
        }

        if (_ghost != entry.Node)
        {
            _ghost?.Hide();
            _ghost = entry.Node;
        }

        _ghostMaterial.AlbedoColor = valid ? new Color(0.25f, 0.85f, 0.35f, 0.55f) : new Color(0.9f, 0.2f, 0.2f, 0.55f);
        GhostValid = valid;
        _ghost.Transform = ClubhouseView.Fit(_grid, entry.Bounds, building);
        _ghost.Visible = true;
        ShowDoorMarker(building);
    }

    public void HideGhost()
    {
        _ghost?.Hide();
        _doorMarker?.Hide();
    }

    /// <summary>Carreau jaune sur la cellule de la porte du fantôme, pour placer le bâtiment face au chemin.</summary>
    private void ShowDoorMarker(Building building)
    {
        if (_doorMarker is null)
        {
            _doorMarker = new MeshInstance3D
            {
                Name = "DoorMarker",
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(1f, 0.85f, 0.1f), NoDepthTest = true },
            };
            AddChild(_doorMarker);
        }

        var size = (float)_grid.CellSize;
        ((StandardMaterial3D)_doorMarker.MaterialOverride!).AlbedoColor = new Color(1f, 0.85f, 0.1f);
        _doorMarker.Mesh = new BoxMesh { Size = new Vector3(size * 0.9f, 0.15f, size * 0.9f) };
        var door = building.DoorPosition(_grid.CellSize);
        _doorMarker.Position = new Vector3((float)door.X, (float)_grid.Sample(door.X, door.Z) + 0.1f, (float)door.Z);
        _doorMarker.Visible = true;
    }

    private static Node3D? Instantiate(BuildingKind kind) => GD.Load<PackedScene>(ModelPath(kind))?.Instantiate() as Node3D;
}
