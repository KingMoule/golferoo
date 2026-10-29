using Godot;
using Golferoo.Sim;
using Golferoo.Sim.Terrain;

namespace Golferoo.Game.Tools;

/// <summary>
/// Outil de pose, pris depuis le tiroir Trous de la barre du bas (V reste active sans être affichée) : un clic sur une cellule de départ
/// y pose le départ du trou que montre la caméra (<see cref="Main.CameraHole"/>), un clic sur son green y pose le trou.
/// Ne fait que poster <see cref="World.PlaceTee"/> et <see cref="World.PlaceHole"/> : la sim refuse et valide.
/// « Nouveau trou » (un clic sur un départ, puis un clic sur un green, poste <see cref="World.AddHole"/>)
/// et « Supprimer » (<see cref="World.RemoveLastHole"/>) sont des boutons du tiroir ; N et Suppr restent.
/// </summary>
public partial class PlaceTool : Node
{
    private Main? _main;
    private int _refusedSeen;
    private bool _newHole;
    private Point2? _pendingTee;

    public bool Active { get; private set; }

    /// <summary>Vrai entre « Nouveau trou » et la pose de son drapeau.</summary>
    public bool NewHole => _newHole;

    /// <summary>Ce que fera le prochain clic, pour le tiroir.</summary>
    public string Status => !_newHole
        ? $"Trou {_main?.CameraHole ?? 1} : clic sur un départ pour le poser, sur le green pour placer le drapeau."
        : _pendingTee is null ? "Nouveau trou : clic sur une cellule de départ." : "Nouveau trou : clic sur le green où poser le drapeau.";

    public void Init(Main main) => _main = main;

    /// <summary>Premier temps de « Nouveau trou » : le prochain clic doit tomber sur une cellule de départ.</summary>
    public void BeginNewHole()
    {
        _newHole = true;
        _pendingTee = null;
    }

    /// <summary>Revient à la pose du départ et du drapeau du trou montré.</summary>
    public void BeginPose() => EndNewHole();

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
            return;
        }

        if (surface != Surface.Green)
        {
            _main!.ShowMessage("Cette cellule n'est pas un green : cliquez sur le green du nouveau trou.");
            return;
        }

        var world = _main!.World;
        if (!_main.Finances.Allow(world.Economy.NewHole))
        {
            return;
        }

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

    /// <summary>Clic gauche à la position d'écran <paramref name="mouse"/> : vrai s'il a été consommé par l'outil.</summary>
    public bool HandleClick(Vector2 mouse)
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
                    _main.World.PlaceTee(_main.CameraHole, point);
                    break;
                case Surface.Green:
                    _main.World.PlaceHole(_main.CameraHole, point);
                    break;

                default:
                    break;
            }
        }

        return true;
    }
}
