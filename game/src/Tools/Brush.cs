using Godot;
using Golferoo.Game.Rendering;
using Golferoo.Sim;
using Golferoo.Sim.Terrain;

namespace Golferoo.Game.Tools;

/// <summary>
/// Pinceau de surface, de relief et bulldozer, pris depuis la barre du bas (les touches B, F1 à F9, [ et ] restent, sans être affichées).
/// Clic gauche : peint, monte ou efface ; clic droit en relief : creuse. Un trait ne commence que sur un clic que l'interface n'a pas pris
/// (<see cref="_UnhandledInput"/>) : un clic sur la barre ou un panneau ne touche jamais le terrain.
/// Au plus une retouche en attente à la fois, donc au plus une par tick de simulation. Ne fait que poster des commandes à <see cref="World"/> : aucune règle ici.
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

    private const int RingSegments = 48;

    private Main? _main;
    private MeshInstance3D? _ring;
    private ImmediateMesh? _ringMesh;
    private StandardMaterial3D? _ringMaterial;
    private Vector3? _hit;
    private Vector2? _cursor;
    private MouseButton _stroke;

    public bool Active { get; private set; }

    /// <summary>Vrai pour le relief, faux pour la peinture de surface.</summary>
    public bool ReliefMode { get; private set; }

    /// <summary>Bulldozer : le trait rend la surface au rough et rase les arbres sous le rayon.</summary>
    public bool BulldozerMode { get; private set; }

    public Surface PaintedSurface { get; private set; } = Surface.Fairway;

    public float Radius { get; private set; } = 6f;

    /// <summary>Dernier point visé sur le terrain, s'il y en a un.</summary>
    public Vector3? Hit => _hit;

    /// <summary>Position de la souris à l'écran, suivie par les événements (la fumée en pousse sans vraie souris).</summary>
    public Vector2 Cursor => _cursor ?? GetViewport().GetMousePosition();

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
    }

    public void SetActive(bool active)
    {
        Active = active;
        _stroke = MouseButton.None;
    }

    public void SelectSurface(Surface surface)
    {
        PaintedSurface = surface;
        ReliefMode = false;
        BulldozerMode = false;
    }

    public void SelectRelief()
    {
        ReliefMode = true;
        BulldozerMode = false;
    }

    public void SelectBulldozer()
    {
        BulldozerMode = true;
        ReliefMode = false;
    }

    public void ChangeRadius(float delta) => SetRadius(Radius + delta);

    public void SetRadius(float radius) => Radius = Mathf.Clamp(radius, MinRadius, MaxRadius);

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

    /// <summary>Toute position de souris compte, et un relâché finit le trait, même au-dessus de l'interface.</summary>
    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouse mouse)
        {
            _cursor = mouse.Position;
        }

        if (@event is InputEventMouseButton { Pressed: false } release && release.ButtonIndex == _stroke)
        {
            _stroke = MouseButton.None;
        }
    }

    /// <summary>Un trait ne commence que sur un clic que l'interface n'a pas pris.</summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (Active && @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left or MouseButton.Right } press)
        {
            _stroke = press.ButtonIndex;
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

    /// <summary>Point du terrain sous la position d'écran <paramref name="screen"/>, vu par la caméra du jeu.</summary>
    public bool PickAt(Vector2 screen, out Vector3 hit)
    {
        hit = default;
        var camera = _main!.Camera.View;
        return camera is not null && camera.IsInsideTree()
            && TryPick(_main.World.Terrain!, camera.ProjectRayOrigin(screen), camera.ProjectRayNormal(screen), camera.Far, out hit);
    }

    public override void _Process(double delta)
    {
        if (_main is null || _ring is null)
        {
            return;
        }

        // Le curseur au sol éclaircit les couronnes même quand le pinceau est éteint.
        var picked = PickAt(Cursor, out var hit);
        _main!.TerrainView.Trees?.SetFocus(TreeLayer.CursorSlot, picked ? hit : null, TreeLayer.CursorFocusRadius);
        _hit = Active && picked ? hit : null;

        _ring.Visible = _hit is not null;
        if (_hit is { } h)
        {
            DrawRing(h);
            Apply(h);
        }
    }

    /// <summary>Poste une retouche au point visé pendant un trait, sauf si la précédente attend encore son tick. Vrai si postée.</summary>
    public bool Apply(Vector3 hit)
    {
        var world = _main!.World;
        if (!Active || _stroke == MouseButton.None || world.PendingEdits > 0 || world.PendingTreeCommands > 0)
        {
            return false;
        }

        var center = new Point2(hit.X, hit.Z);
        if (ReliefMode)
        {
            world.RaiseTerrain(center, Radius, _stroke == MouseButton.Left ? ReliefStepMeters : -ReliefStepMeters);
            return true;
        }

        if (_stroke != MouseButton.Left)
        {
            return false;
        }

        if (BulldozerMode)
        {
            world.PaintSurface(center, Radius, Surface.Rough);
            world.RazeArea(center, Radius, keepRemarkable: false);
            return true;
        }

        world.PaintSurface(center, Radius, PaintedSurface);
        return true;
    }

    private void DrawRing(Vector3 center)
    {
        var grid = _main!.World.Terrain!;
        _ringMaterial!.AlbedoColor = BulldozerMode ? Ui.UiTheme.BrickLight : ReliefMode ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 1f, 1f);
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
}
