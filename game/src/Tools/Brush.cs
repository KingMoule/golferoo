using Godot;
using Golferoo.Game.Rendering;
using Golferoo.Sim;
using Golferoo.Sim.Terrain;

namespace Golferoo.Game.Tools;

/// <summary>
/// Pinceau de surface et de relief (B pour l'activer, F1 à F8 la surface, F9 le relief, [ et ] le rayon).
/// Clic gauche : peint ou monte ; clic droit en mode relief : baisse. Au plus une retouche en attente à la fois,
/// donc au plus une par tick de simulation. Ne fait que poster des commandes à <see cref="World"/> : aucune règle ici.
/// </summary>
public partial class Brush : Node3D
{
    public const float MinRadius = 2f;
    public const float MaxRadius = 40f;
    public const float RadiusStep = 2f;

    /// <summary>Variation de relief par retouche, en mètres (un tick à 20 Hz : environ 3 m/s en gardant le bouton).</summary>
    public const double ReliefStepMeters = 0.15;

    private static readonly Surface[] FunctionSurfaces =
    [
        Surface.Tee, Surface.Green, Surface.Fairway, Surface.Rough, Surface.Bunker, Surface.Water, Surface.Trees, Surface.Path,
    ];

    private static readonly string[] SurfaceNames = ["Tee", "Green", "Fairway", "Rough", "Bunker", "Eau", "Arbres", "Chemin"];

    private const int RingSegments = 48;

    private Main? _main;
    private Label? _label;
    private MeshInstance3D? _ring;
    private ImmediateMesh? _ringMesh;
    private StandardMaterial3D? _ringMaterial;
    private Vector3? _hit;

    public bool Active { get; private set; }

    /// <summary>Vrai pour le relief (F9), faux pour la peinture de surface.</summary>
    public bool ReliefMode { get; private set; }

    public Surface PaintedSurface { get; private set; } = Surface.Fairway;

    public float Radius { get; private set; } = 6f;

    /// <summary>Dernier point visé sur le terrain, s'il y en a un.</summary>
    public Vector3? Hit => _hit;

    public void Init(Main main)
    {
        _main = main;
        _ringMesh = new ImmediateMesh();
        _ringMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = Colors.White,
            NoDepthTest = true,
            RenderPriority = 10,
        };
        _ring = new MeshInstance3D { Name = "Ring", Mesh = _ringMesh, MaterialOverride = _ringMaterial, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_ring);

        var layer = new CanvasLayer { Name = "BrushPanelLayer" };
        var panel = new PanelContainer { Name = "BrushPanel", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        panel.AnchorLeft = 1f;
        panel.AnchorRight = 1f;
        panel.OffsetLeft = -330f;
        panel.OffsetRight = -8f;
        panel.OffsetTop = 8f;
        _label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddChild(_label);
        layer.AddChild(panel);
        AddChild(layer);
        Refresh();
    }

    /// <summary>Texte du panneau : outil, surface et rayon.</summary>
    public string Describe() =>
        $"Pinceau : {(ReliefMode ? "relief (clic gauche monte, droit baisse)" : "surface")}\nSurface : {SurfaceNames[(int)PaintedSurface]}\nRayon : {Radius:0} m";

    public void SetActive(bool active)
    {
        Active = active;
        Refresh();
    }

    public void SelectSurface(Surface surface)
    {
        PaintedSurface = surface;
        ReliefMode = false;
        Refresh();
    }

    public void SelectRelief()
    {
        ReliefMode = true;
        Refresh();
    }

    public void ChangeRadius(float delta)
    {
        Radius = Mathf.Clamp(Radius + delta, MinRadius, MaxRadius);
        Refresh();
    }

    /// <summary>Gère une touche ; vrai si elle appartient au pinceau.</summary>
    public bool HandleKey(Key key)
    {
        if (key == Key.B)
        {
            SetActive(!Active);
            return true;
        }

        if (!Active)
        {
            return false;
        }

        if (key >= Key.F1 && key <= Key.F8)
        {
            SelectSurface(FunctionSurfaces[key - Key.F1]);
            return true;
        }

        switch (key)
        {
            case Key.F9:
                SelectRelief();
                return true;
            case Key.Bracketleft:
                ChangeRadius(-RadiusStep);
                return true;
            case Key.Bracketright:
                ChangeRadius(RadiusStep);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Point où le rayon (origine, direction) touche le terrain : marche par pas d'un mètre jusqu'à passer sous la surface, puis bissection.
    /// Faux si le rayon ne touche pas la carte.
    /// </summary>
    public static bool TryPick(TerrainGrid grid, Vector3 origin, Vector3 direction, float maxDistance, out Vector3 hit)
    {
        hit = default;
        var dir = direction.Normalized();
        const float step = 1f;
        var previousT = 0f;
        for (var t = 0f; t <= maxDistance; t += step)
        {
            var p = origin + (dir * t);
            if (p.Y > (float)grid.Sample(p.X, p.Z))
            {
                previousT = t;
                continue;
            }

            if (t == 0f)
            {
                return false;
            }

            var lo = previousT;
            var hi = t;
            for (var i = 0; i < 24; i++)
            {
                var mid = (lo + hi) * 0.5f;
                var q = origin + (dir * mid);
                if (q.Y > (float)grid.Sample(q.X, q.Z))
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            var h = origin + (dir * hi);
            if (h.X < 0 || h.Z < 0 || h.X > grid.CellsX * grid.CellSize || h.Z > grid.CellsZ * grid.CellSize)
            {
                return false;
            }

            hit = new Vector3(h.X, (float)grid.Sample(h.X, h.Z), h.Z);
            return true;
        }

        return false;
    }

    public override void _Process(double delta)
    {
        if (_main is null || _ring is null)
        {
            return;
        }

        // Le curseur au sol éclaircit les couronnes même quand le pinceau est éteint.
        var picked = PickUnderMouse(out var hit);
        _main!.TerrainView.Trees?.SetFocus(TreeLayer.CursorSlot, picked ? hit : null, TreeLayer.CursorFocusRadius);
        _hit = Active && picked ? hit : null;

        _ring.Visible = _hit is not null;
        if (_hit is { } h)
        {
            DrawRing(h);
            Apply(h);
        }
    }

    /// <summary>Poste une retouche au point visé, sauf si la précédente attend encore son tick. Vrai si postée.</summary>
    public bool Apply(Vector3 hit)
    {
        var world = _main!.World;
        if (!Active || world.PendingEdits > 0)
        {
            return false;
        }

        var center = new Point2(hit.X, hit.Z);
        if (ReliefMode)
        {
            if (Input.IsMouseButtonPressed(MouseButton.Left))
            {
                world.RaiseTerrain(center, Radius, ReliefStepMeters);
                return true;
            }

            if (Input.IsMouseButtonPressed(MouseButton.Right))
            {
                world.RaiseTerrain(center, Radius, -ReliefStepMeters);
                return true;
            }

            return false;
        }

        if (Input.IsMouseButtonPressed(MouseButton.Left))
        {
            world.PaintSurface(center, Radius, PaintedSurface);
            return true;
        }

        return false;
    }

    private bool PickUnderMouse(out Vector3 hit)
    {
        hit = default;
        var camera = _main!.Camera.View;
        if (camera is null || !camera.IsInsideTree())
        {
            return false;
        }

        var mouse = GetViewport().GetMousePosition();
        return TryPick(_main.World.Terrain!, camera.ProjectRayOrigin(mouse), camera.ProjectRayNormal(mouse), camera.Far, out hit);
    }

    private void DrawRing(Vector3 center)
    {
        var grid = _main!.World.Terrain!;
        _ringMaterial!.AlbedoColor = ReliefMode ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 1f, 1f);
        _ringMesh!.ClearSurfaces();
        _ringMesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
        for (var i = 0; i <= RingSegments; i++)
        {
            var a = i * Mathf.Tau / RingSegments;
            var x = center.X + (Mathf.Cos(a) * Radius);
            var z = center.Z + (Mathf.Sin(a) * Radius);
            _ringMesh.SurfaceAddVertex(new Vector3(x, (float)grid.Sample(x, z) + 0.15f, z));
        }

        _ringMesh.SurfaceEnd();
    }

    private void Refresh()
    {
        if (_label is not null)
        {
            _label.Text = Describe();
            _label.GetParent<Control>().Visible = Active;
        }
    }
}
