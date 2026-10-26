using Godot;
using Golferoo.Sim;
using Golferoo.Sim.Terrain;

namespace Golferoo.Game.Tools;

/// <summary>
/// Outil de pose (V pour l'activer, B ou T l'éteignent) : un clic sur une cellule de départ y pose le départ, un clic sur le green y pose le trou.
/// Ne fait que poster <see cref="World.PlaceTee"/> et <see cref="World.PlaceHole"/> : la sim refuse et valide.
/// </summary>
public partial class PlaceTool : Node
{
    private Main? _main;
    private Label? _label;

    public bool Active { get; private set; }

    public void Init(Main main)
    {
        _main = main;
        var layer = new CanvasLayer { Name = "PlaceToolLayer" };
        var panel = new PanelContainer { Name = "PlacePanel", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        panel.AnchorLeft = 1f;
        panel.AnchorRight = 1f;
        panel.OffsetLeft = -330f;
        panel.OffsetRight = -8f;
        panel.OffsetTop = 8f;
        _label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, Text = "Pose : clic sur le départ (Tee) pour le poser, clic sur le green pour poser le trou" };
        panel.AddChild(_label);
        layer.AddChild(panel);
        AddChild(layer);
    }

    public void SetActive(bool active)
    {
        Active = active;
        _label?.GetParent<Control>().SetVisible(active);
    }

    /// <summary>Gère une touche ; vrai pour V seulement (B et T éteignent l'outil sans consommer la touche).</summary>
    public bool HandleKey(Key key)
    {
        if (key == Key.V)
        {
            if (!Active)
            {
                _main!.Brush.SetActive(false);
                _main.TreeTool.SetActive(false);
            }

            SetActive(!Active);
            return true;
        }

        if (Active && key is Key.B or Key.T)
        {
            SetActive(false);
        }

        return false;
    }

    /// <summary>Clic gauche : vrai s'il a été consommé par l'outil.</summary>
    public bool HandleClick()
    {
        if (!Active || _main is null)
        {
            return false;
        }

        var camera = _main.Camera.View;
        if (camera is null || !camera.IsInsideTree())
        {
            return true;
        }

        var grid = _main.World.Terrain!;
        var mouse = GetViewport().GetMousePosition();
        if (Brush.TryPick(grid, camera.ProjectRayOrigin(mouse), camera.ProjectRayNormal(mouse), camera.Far, out var hit))
        {
            var point = new Point2(hit.X, hit.Z);
            switch (grid.SurfaceAt(point.X, point.Z))
            {
                case Surface.Tee:
                    _main.World.PlaceTee(point);
                    break;
                case Surface.Green:
                    _main.World.PlaceHole(point);
                    break;
                default:
                    break;
            }
        }

        return true;
    }
}
