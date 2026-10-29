using Godot;
using Golferoo.Sim;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;
using Tree = Golferoo.Sim.Trees.Tree;

namespace Golferoo.Game.Tools;

/// <summary>
/// Outil « arbres », pris depuis le tiroir Arbres de la barre du bas. Le clic gauche sur le terrain (un clic que l'interface n'a pas pris)
/// fait ce que dit <see cref="Mode"/> : sélectionner l'arbre sous le curseur, planter un plant ou une motte de <see cref="Species"/>,
/// ou raser la zone au rayon du pinceau (sous-bois compris, remarquables épargnés si <see cref="KeepRemarkable"/>).
/// Les touches d'avant (T, R, X, K, Y, P, M, F, C, [ et ]) restent actives sans être affichées ; F (faire tomber, debug) n'a pas de bouton.
/// Ne fait que poster des commandes à <see cref="World"/> : aucune règle ici ; les coûts affichés viennent de <see cref="World.Economy"/>.
/// </summary>
public partial class TreeTool : Node3D
{
    private const int RingSegments = 48;

    private Main? _main;
    private MeshInstance3D? _ring;
    private ImmediateMesh? _ringMesh;
    private StandardMaterial3D? _ringMaterial;
    private Vector3? _hit;

    /// <summary>Ce que fait un clic gauche : sélectionner un arbre, planter, ou raser la zone (le mode reste jusqu'au choix d'un autre).</summary>
    public enum ClickMode
    {
        Select,
        Sapling,
        Mound,
        RazeArea,
    }

    private const string GhostFolder = "res://assets/trees/";
    private static readonly string[] GhostPrefixes = ["oak", "birch", "conifer"];

    private Node3D? _ghost;
    private TreeSpeciesId? _ghostSpecies;
    private TreePlantStage? _ghostStage;

    public bool Active { get; private set; }

    public ClickMode Mode { get; private set; }

    /// <summary>Id de l'arbre sélectionné, ou -1.</summary>
    public int SelectedId { get; private set; } = -1;

    public TreeSpeciesId Species { get; private set; } = TreeSpeciesId.Oak;

    /// <summary>Si vrai, la zone rasée épargne les arbres remarquables.</summary>
    public bool KeepRemarkable { get; private set; } = true;

    /// <summary>Dernier point visé sur le terrain, s'il y en a un.</summary>
    public Vector3? Hit => _hit;

    public void Init(Main main)
    {
        _main = main;
        _ringMesh = new ImmediateMesh();
        _ringMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            NoDepthTest = true,
            RenderPriority = 10,
        };
        _ring = new MeshInstance3D { Name = "Ring", Mesh = _ringMesh, MaterialOverride = _ringMaterial, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_ring);
    }

    /// <summary>Modèle 3D du plant (<c>_01</c>) ou de la motte (<c>_03</c>, l'allure d'un jeune arbre) d'une essence : fantôme et vignette.</summary>
    public static string ModelPath(TreeSpeciesId species, TreePlantStage stage) =>
        $"{GhostFolder}{GhostPrefixes[(int)species]}_sapling_{(stage == TreePlantStage.Mound ? "03" : "01")}.glb";

    /// <summary>Ce que fait le clic et l'arbre sélectionné, sans touche.</summary>
    public string Describe()
    {
        var text = $"Clic : {ModeText()}.";
        if (_main is not null && SelectedId >= 0 && _main.World.Trees.TryGet(SelectedId, out var t))
        {
            var spec = TreeSpeciesTable.Get(t.Species);
            text += $" Sélection : {spec.Name}, {t.AgeMonths / 12.0:F0} ans, {t.Height():F1} m{(t.Remarkable ? ", remarquable" : string.Empty)}, {StateName(t.State)}.";
        }

        return text;
    }

    private string ModeText() => Mode switch
    {
        ClickMode.Sapling => $"planter un plant ({TreeSpeciesTable.Get(Species).Name})",
        ClickMode.Mound => $"planter une motte ({TreeSpeciesTable.Get(Species).Name})",
        ClickMode.RazeArea => KeepRemarkable ? "raser la zone, remarquables gardés" : "raser la zone",
        _ => "sélectionner un arbre",
    };

    public void SetActive(bool active)
    {
        Active = active;
        if (!active)
        {
            Mode = ClickMode.Select;
            SelectedId = -1;
        }
    }

    public void SetMode(ClickMode mode) => Mode = mode;

    /// <summary>Choisit l'essence et le stade à planter : le clic suivant plante.</summary>
    public void SetPlanting(TreeSpeciesId species, TreePlantStage stage)
    {
        Species = species;
        Mode = stage == TreePlantStage.Mound ? ClickMode.Mound : ClickMode.Sapling;
    }

    public void SetKeepRemarkable(bool keep) => KeepRemarkable = keep;

    /// <summary>Gère une touche ; vrai si elle appartient à l'outil.</summary>
    public bool HandleKey(Key key)
    {
        if (key == Key.T)
        {
            if (!Active)
            {
                _main!.Brush.SetActive(false);
            }

            SetActive(!Active);
            return true;
        }

        if (!Active)
        {
            return false;
        }

        switch (key)
        {
            case Key.R:
                Mode = ClickMode.Select;
                RazeSelected();
                return true;
            case Key.X:
                Mode = ClickMode.Select;
                RazeAreaAtCursor();
                return true;
            case Key.K:
                KeepRemarkable = !KeepRemarkable;
                return true;
            case Key.Y:
                Species = (TreeSpeciesId)(((int)Species + 1) % TreeSpeciesTable.Count);
                return true;
            case Key.P:
                Mode = ClickMode.Sapling;
                return true;
            case Key.M:
                Mode = ClickMode.Mound;
                return true;
            case Key.F:
                Mode = ClickMode.Select;
                FellSelected();
                return true;
            case Key.C:
                Mode = ClickMode.Select;
                ClearSelected();
                return true;
            case Key.Bracketleft:
                _main!.Brush.ChangeRadius(-Brush.RadiusStep);
                return true;
            case Key.Bracketright:
                _main!.Brush.ChangeRadius(Brush.RadiusStep);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Sélectionne l'arbre d'identifiant <paramref name="id"/> (faux s'il n'existe pas).</summary>
    public bool Select(int id)
    {
        var found = _main!.World.Trees.TryGet(id, out _);
        SelectedId = found ? id : -1;
        return found;
    }

    public bool RazeSelected()
    {
        if (SelectedId < 0 || !_main!.World.Trees.TryGet(SelectedId, out var t) || t.State != TreeState.Standing)
        {
            return false;
        }

        if (!_main.Finances.Allow(_main.World.Economy.RazeCost(t)))
        {
            return false;
        }

        _main.World.RazeTree(SelectedId);
        SelectedId = -1;
        return true;
    }

    public void RazeAreaAt(Point2 center) => _main!.World.RazeArea(center, _main.Brush.Radius, KeepRemarkable);

    public void PlantAt(Point2 position, TreePlantStage stage)
    {
        if (_main!.Finances.Allow(_main.World.Economy.PlantCost(stage)))
        {
            _main.World.PlantTree(position.X, position.Z, Species, stage);
        }
    }

    /// <summary>Fait tomber l'arbre sélectionné vers la droite de l'écran (direction de la caméra), pour être bien vu.</summary>
    public bool FellSelected()
    {
        if (SelectedId < 0 || !_main!.World.Trees.TryGet(SelectedId, out var t) || t.State != TreeState.Standing)
        {
            return false;
        }

        var right = _main.Camera.View is { } cam ? cam.GlobalTransform.Basis.X : Vector3.Right;
        _main.World.FellTree(SelectedId, Math.Atan2(right.Z, right.X));
        return true;
    }

    public bool ClearSelected()
    {
        if (SelectedId < 0 || !_main!.World.Trees.TryGet(SelectedId, out var t) || t.State != TreeState.Fallen)
        {
            return false;
        }

        _main.World.ClearFallen(SelectedId);
        return true;
    }

    private void RazeAreaAtCursor()
    {
        if (_hit is { } h)
        {
            RazeAreaAt(new Point2(h.X, h.Z));
        }
    }

    private void PlantAtCursor(TreePlantStage stage)
    {
        if (_hit is { } h)
        {
            PlantAt(new Point2(h.X, h.Z), stage);
        }
    }

    /// <summary>
    /// Arbre le plus proche du rayon (origine, direction), comparé à son tronc (un segment vertical pour un arbre debout, couché
    /// dans sa direction pour un tronc tombé, court pour une souche). Seuil : le rayon du tronc, au moins 0,8 m, ou le tiers de la couronne.
    /// Rend -1 s'il n'y en a pas.
    /// </summary>
    public static int PickTree(TreeStand stand, Golferoo.Sim.Terrain.TerrainGrid grid, Vector3 origin, Vector3 direction, Vector3 groundHit)
    {
        var near = new List<Tree>();
        stand.Query(groundHit.X, groundHit.Z, 45.0, near);
        var best = -1;
        var bestDistance = double.MaxValue;
        var dir = direction.Normalized();
        foreach (var t in near)
        {
            var ground = new Vector3((float)t.X, (float)grid.Sample(t.X, t.Z), (float)t.Z);
            var length = t.State == TreeState.Stump ? 0.6f : (float)t.Height();
            var tip = t.State == TreeState.Fallen
                ? ground + (new Vector3(Mathf.Cos(t.FallYaw), 0f, Mathf.Sin(t.FallYaw)) * length)
                : ground + (Vector3.Up * length);
            var distance = RaySegmentDistance(origin, dir, ground, tip);
            var threshold = Math.Max(0.8, t.State == TreeState.Standing ? t.CrownRadius() * 0.33 : 0.8);
            if (distance <= threshold && distance < bestDistance)
            {
                bestDistance = distance;
                best = t.Id;
            }
        }

        return best;
    }

    /// <summary>Plus courte distance entre la demi-droite (origine, direction unitaire) et le segment [a, b] (formule exacte, paramètres bornés).</summary>
    private static double RaySegmentDistance(Vector3 origin, Vector3 dir, Vector3 a, Vector3 b)
    {
        var v = b - a;
        var w0 = origin - a;
        var vv = v.Dot(v);
        var dv = dir.Dot(v);
        var dw = dir.Dot(w0);
        var vw = v.Dot(w0);
        var denom = vv - (dv * dv);
        // Point du segment le plus proche de la droite, puis point de la demi-droite le plus proche de ce point.
        var t = denom > 1e-6f ? Mathf.Clamp((vw - (dv * dw)) / denom, 0f, 1f) : 0f;
        var p = a + (v * t);
        var s = Math.Max(0f, (p - origin).Dot(dir));
        return (p - (origin + (dir * s))).Length();
    }

    /// <summary>Un clic gauche que l'interface n'a pas pris : l'action du mode, au point visé.</summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (Active && @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
        {
            ClickAt(click.Position);
        }
    }

    /// <summary>L'action du mode au point du terrain sous la position d'écran <paramref name="screen"/>.</summary>
    public void ClickAt(Vector2 screen)
    {
        var camera = _main!.Camera.View;
        if (camera is null || !camera.IsInsideTree() || !_main.Brush.PickAt(screen, out var hit))
        {
            return;
        }

        var point = new Point2(hit.X, hit.Z);
        switch (Mode)
        {
            case ClickMode.Select:
                SelectedId = PickTree(_main.World.Trees, _main.World.Terrain!, camera.ProjectRayOrigin(screen), camera.ProjectRayNormal(screen), hit);
                break;
            case ClickMode.RazeArea:
                RazeAreaAt(point);
                break;
            default:
                PlantAt(point, Mode == ClickMode.Mound ? TreePlantStage.Mound : TreePlantStage.Sapling);
                break;
        }
    }

    public override void _Process(double delta)
    {
        if (_main is null || _ring is null)
        {
            return;
        }

        _hit = Active && _main.Brush.PickAt(_main.Brush.Cursor, out var hit) ? hit : null;
        Draw();
        DrawGhost();
    }

    /// <summary>Image translucide de la pousse (ou de la motte) que le clic planterait, à l'essence choisie avec Y.</summary>
    private void DrawGhost()
    {
        var stage = Mode == ClickMode.Mound ? TreePlantStage.Mound : TreePlantStage.Sapling;
        if (!Active || Mode is ClickMode.Select or ClickMode.RazeArea || _hit is not { } h)
        {
            _ghost?.SetVisible(false);
            return;
        }

        if (_ghost is null || _ghostSpecies != Species || _ghostStage != stage)
        {
            _ghost?.QueueFree();
            _ghost = GD.Load<PackedScene>(ModelPath(Species, stage))?.Instantiate<Node3D>();
            _ghostSpecies = Species;
            _ghostStage = stage;
            if (_ghost is null)
            {
                return;
            }

            Fade(_ghost);
            AddChild(_ghost);
        }

        _ghost.Visible = true;
        _ghost.Position = new Vector3(h.X, (float)_main!.World.Terrain!.Sample(h.X, h.Z), h.Z);
    }

    private static void Fade(Node node)
    {
        if (node is GeometryInstance3D geometry)
        {
            geometry.Transparency = 0.5f;
            geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }

        foreach (var child in node.GetChildren())
        {
            Fade(child);
        }
    }

    private void Draw()
    {
        _ringMesh!.ClearSurfaces();
        _ring!.Visible = false;
        if (!Active)
        {
            return;
        }

        var grid = _main!.World.Terrain!;
        var drawn = false;
        if (_hit is { } h)
        {
            DrawCircle(grid, h.X, h.Z, _main.Brush.Radius, new Color(0.4f, 1f, 0.4f));
            drawn = true;
        }

        if (SelectedId >= 0 && _main.World.Trees.TryGet(SelectedId, out var t))
        {
            DrawCircle(grid, t.X, t.Z, Math.Clamp(t.CrownRadius(), 1.0, 8.0), new Color(1f, 0.6f, 0.1f));
            drawn = true;
        }

        _ring.Visible = drawn;
    }

    private void DrawCircle(Golferoo.Sim.Terrain.TerrainGrid grid, double cx, double cz, double radius, Color color)
    {
        _ringMesh!.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
        _ringMesh.SurfaceSetColor(color);
        for (var i = 0; i <= RingSegments; i++)
        {
            var a = i * Mathf.Tau / RingSegments;
            var x = cx + (Mathf.Cos(a) * radius);
            var z = cz + (Mathf.Sin(a) * radius);
            _ringMesh.SurfaceAddVertex(new Vector3((float)x, (float)grid.Sample(x, z) + 0.15f, (float)z));
        }

        _ringMesh.SurfaceEnd();
    }

    private static string StateName(TreeState state) => state switch
    {
        TreeState.Standing => "debout",
        TreeState.Fallen => "tombé",
        _ => "souche",
    };
}
