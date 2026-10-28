using Godot;
using Golferoo.Sim;
using Golferoo.Sim.Terrain;

namespace Golferoo.Game.Tools;

/// <summary>
/// Outil de pose (V pour l'activer, B ou T l'éteignent) : un clic sur une cellule de départ y pose le départ, un clic sur le green y pose le trou.
/// Ne fait que poster <see cref="World.PlaceTee"/> et <see cref="World.PlaceHole"/> : la sim refuse et valide.
/// Deux boutons, actifs avec l'outil : « Nouveau trou » (N : un clic sur un départ, puis un clic sur un green, poste <see cref="World.AddHole"/>)
/// et « Retirer le dernier trou » (Suppr : <see cref="World.RemoveLastHole"/>).
/// </summary>
public partial class PlaceTool : Node
{
    private Main? _main;
    private Label? _label;
    private Control? _panel;
    private int _refusedSeen;
    private bool _newHole;
    private Point2? _pendingTee;

    private const string PoseText = "Pose : clic sur le départ (Tee) pour le poser, clic sur le green pour poser le trou";

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
        _label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, Text = PoseText, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _panel = panel;
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddChild(_label);
        column.AddChild(NewButton("NewHoleButton", "Nouveau trou (N)", BeginNewHole));
        column.AddChild(NewButton("RemoveHoleButton", "Retirer le dernier trou (Suppr)", RemoveLastHole));
        panel.AddChild(column);
        layer.AddChild(panel);
        AddChild(layer);
    }

    private static Button NewButton(string name, string text, Action pressed)
    {
        var button = new Button { Name = name, Text = text, FocusMode = Control.FocusModeEnum.None };
        button.Pressed += pressed;
        return button;
    }

    /// <summary>Premier temps de « Nouveau trou » : le prochain clic doit tomber sur une cellule de départ.</summary>
    public void BeginNewHole()
    {
        _newHole = true;
        _pendingTee = null;
        _label!.Text = "Nouveau trou : clic sur une cellule de départ (Tee)";
    }

    /// <summary>Met en file le retrait du dernier trou ; dit pourquoi si la sim refuse.</summary>
    public void RemoveLastHole()
    {
        EndNewHole();
        _main!.ShowMessage(_main.World.RemoveLastHole()
            ? "Dernier trou retiré."
            : "Impossible de retirer le dernier trou : il en faut au moins un, et aucun groupe en jeu ne doit l'avoir dans sa partie.");
    }

    private void EndNewHole()
    {
        _newHole = false;
        _pendingTee = null;
        if (_label is not null)
        {
            _label.Text = PoseText;
        }
    }

    private static string Describe(IReadOnlyList<HoleIssue> issues) => issues.Count == 0
        ? "Impossible d'ajouter un trou : 18 trous au plus."
        : "Ce trou ne convient pas : " + string.Join(", ", issues.Select(i => i switch
        {
            HoleIssue.TeeTooSmall => "départ trop petit",
            HoleIssue.FairwayTooShort => "pas assez de fairway entre les deux",
            HoleIssue.GreenTooSmall => "green trop petit",
            HoleIssue.TeeNotPlaced => "le départ n'est pas sur une cellule de départ",
            HoleIssue.HoleNotOnGreen => "le drapeau n'est pas sur un green",
            HoleIssue.TooClose => "départ et drapeau trop proches",
            HoleIssue.NoPath => "aucun chemin à pied",
            _ => i.ToString(),
        })) + ".";

    private void NewHoleClick(TerrainGrid grid, Point2 point)
    {
        var surface = grid.SurfaceAt(point.X, point.Z);
        if (_pendingTee is not { } tee)
        {
            if (surface != Surface.Tee)
            {
                _main!.ShowMessage("Cette cellule n'est pas un départ : cliquez sur une cellule de départ (Tee).");
                return;
            }

            _pendingTee = point;
            _label!.Text = "Nouveau trou : clic sur le green où poser le drapeau";
            return;
        }

        if (surface != Surface.Green)
        {
            _main!.ShowMessage("Cette cellule n'est pas un green : cliquez sur le green du nouveau trou.");
            return;
        }

        var world = _main!.World;
        _main.ShowMessage(world.AddHole(tee, point) ? $"Trou {grid.Course.Count + 1} ajouté (par {TerrainGrid.DeducedPar(tee, point)})." : Describe(world.NewHoleIssues(tee, point)));
        EndNewHole();
    }

    public override void _Process(double delta)
    {
        if (_main?.World is not { } world)
        {
            return;
        }

        if (world.HoleCommandsRefused != _refusedSeen)
        {
            if (world.HoleCommandsRefused > _refusedSeen)
            {
                _main.ShowMessage("Trou refusé à l'application : un groupe a pris ce trou dans sa partie, ou la place a changé.");
            }

            _refusedSeen = world.HoleCommandsRefused;
        }
    }

    public void SetActive(bool active)
    {
        Active = active;
        if (!active)
        {
            EndNewHole();
        }

        _panel?.SetVisible(active);
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
        else if (Active && key == Key.N)
        {
            BeginNewHole();
            return true;
        }
        else if (Active && key == Key.Delete)
        {
            RemoveLastHole();
            return true;
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
            if (_newHole)
            {
                NewHoleClick(grid, point);
                return true;
            }

            switch (grid.SurfaceAt(point.X, point.Z))
            {
                case Surface.Tee:
                    _main.World.PlaceTee(1, point);
                    break;
                case Surface.Green:
                    _main.World.PlaceHole(1, point);
                    break;
                default:
                    break;
            }
        }

        return true;
    }
}
