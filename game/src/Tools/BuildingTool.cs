using Godot;
using Golferoo.Game.Ui;
using Golferoo.Sim;
using Golferoo.Sim.Buildings;
using Golferoo.Sim.Terrain;

namespace Golferoo.Game.Tools;

/// <summary>
/// Outil de pose des bâtiments (C17), pris depuis le tiroir Bâtiments : le genre choisi suit la souris en fantôme (vert s'il peut être posé, rouge sinon, avec la raison),
/// un clic le pose, la molette ou Tab le tourne d'un quart de tour, un clic droit sur un bâtiment le vend à moitié prix.
/// Ne fait que poster <see cref="World.PlaceBuilding"/> et <see cref="World.SellBuilding"/> : la sim refuse et valide.
/// </summary>
public partial class BuildingTool : Node
{
    /// <summary>Les genres posables, dans l'ordre du tiroir.</summary>
    public static readonly BuildingKind[] Kinds = [BuildingKind.SnackBar, BuildingKind.ProShop, BuildingKind.MaintenanceShed, BuildingKind.Bench];

    private readonly HashSet<BuildingKind> _unlocked = [];
    private Main? _main;

    public bool Active { get; private set; }

    /// <summary>Le genre choisi dans le tiroir ; nul tant qu'aucun bouton n'est pressé.</summary>
    public BuildingKind? Kind { get; private set; }

    public DoorSide Side { get; private set; } = DoorSide.South;

    /// <summary>Ce que montre le fantôme (prix, ou raison du refus), pour le tiroir.</summary>
    public string Status { get; private set; } = "";

    public void Init(Main main)
    {
        _main = main;
        foreach (var kind in Kinds.Where(main.World.IsUnlocked))
        {
            _unlocked.Add(kind);
        }
    }

    public void SetActive(bool active)
    {
        Active = active;
        if (!active)
        {
            Kind = null;
            Status = "";
            _main?.Structures.HideGhost();
        }
    }

    /// <summary>Choisit (ou, pressé de nouveau, lâche) le genre à poser.</summary>
    public void Select(BuildingKind kind)
    {
        Kind = Kind == kind ? null : kind;
        if (Kind is null)
        {
            Status = "";
            _main!.Structures.HideGhost();
        }
    }

    /// <summary>Quart de tour : la porte passe au côté suivant.</summary>
    public void Rotate() => Side = (DoorSide)(((int)Side + 1) % 4);

    /// <summary>Le bâtiment du genre choisi dont le centre est au plus près de <paramref name="point"/>.</summary>
    private Building? Target(Point2 point)
    {
        if (Kind is not { } kind || _main!.World.Terrain is not { } grid || _main.World.BuildingRules.At(kind, 0, 0, Side) is not { } proto)
        {
            return null;
        }

        var f = proto.Footprint;
        return _main.World.BuildingRules.At(
            kind,
            (int)Math.Round((point.X / grid.CellSize) - ((f.MaxX + 1) / 2.0)),
            (int)Math.Round((point.Z / grid.CellSize) - ((f.MaxZ + 1) / 2.0)),
            Side);
    }

    /// <summary>Montre le fantôme sous <paramref name="point"/> et rend le verdict de la sim (le solde n'y compte pas).</summary>
    public BuildingPlacement ShowGhost(Point2 point)
    {
        var world = _main!.World;
        if (Target(point) is not { } target)
        {
            return BuildingPlacement.Unknown;
        }

        var verdict = world.CheckBuilding(target.Kind, target.Footprint.MinX, target.Footprint.MinZ, target.Side);
        _main.Structures.ShowGhost(target, verdict == BuildingPlacement.Placed);
        var name = UiNames.BuildingName(target.Kind);
        Status = verdict == BuildingPlacement.Placed
            ? $"{name}, {UiMoney.Format(world.BuildingCost(target.Kind) ?? 0)} + défrichage : clic pour poser, molette pour tourner, clic droit pour vendre."
            : $"{name} : {UiNames.Refusal(verdict)}";
        return verdict;
    }

    /// <summary>Pose le genre choisi sous <paramref name="point"/> ; dit pourquoi si la sim refuse.</summary>
    public BuildingPlacement TryPlace(Point2 point)
    {
        var world = _main!.World;
        if (Target(point) is not { } target)
        {
            return BuildingPlacement.Unknown;
        }

        var (x, z) = (target.Footprint.MinX, target.Footprint.MinZ);
        var result = world.CheckBuilding(target.Kind, x, z, target.Side);
        if (result == BuildingPlacement.Placed && !_main.Finances.Allow(world.BuildingCost(target.Kind) ?? 0))
        {
            return BuildingPlacement.Funds;
        }

        result = world.PlaceBuilding(target.Kind, x, z, target.Side);
        var name = UiNames.BuildingName(target.Kind);
        if (result == BuildingPlacement.Placed)
        {
            _main.Birdie.Post(Birdie.Club, $"{name} posé : {UiMoney.Format(world.BuildingCost(target.Kind) ?? 0)}.", "#bâtiments");
        }
        else
        {
            Sound.Audio.Play("ui_refuse");
            _main.ShowMessage($"{name} refusé : {UiNames.Refusal(result)}");
        }

        return result;
    }

    /// <summary>Vend le bâtiment sous <paramref name="point"/> (moitié du prix rendue) ; faux s'il n'y en a pas.</summary>
    public bool TrySell(Point2 point)
    {
        var world = _main!.World;
        if (world.Terrain is not { } grid)
        {
            return false;
        }

        var (x, z) = (grid.CellXOf(point.X), grid.CellZOf(point.Z));
        if (world.PlacedBuildings.FirstOrDefault(b => b.Footprint.Contains(x, z)) is not { } building || !world.SellBuilding(x, z))
        {
            return false;
        }

        _main!.Birdie.Post(Birdie.Club, $"{UiNames.BuildingName(building.Kind)} vendu : {UiMoney.Format((world.BuildingCost(building.Kind) ?? 0) / 2)} rendus.", "#bâtiments");
        return true;
    }

    public override void _Process(double delta)
    {
        if (_main?.World is not { } world)
        {
            return;
        }

        foreach (var kind in Kinds.Where(k => !_unlocked.Contains(k) && world.IsUnlocked(k)))
        {
            _unlocked.Add(kind);
            _main.Birdie.Post(Birdie.Club, $"{UiNames.BuildingName(kind)} débloqué : {UiNames.BuildingTip(kind)}", "#bâtiments");
        }

        if (Active && Kind is not null && Pick(GetViewport().GetMousePosition()) is { } point)
        {
            ShowGhost(point);
        }
    }

    private Point2? Pick(Vector2 mouse)
    {
        var camera = _main!.Camera.View;
        if (camera is null || !camera.IsInsideTree() || !Brush.TryPick(_main.World.Terrain!, camera.ProjectRayOrigin(mouse), camera.ProjectRayNormal(mouse), camera.Far, out var hit))
        {
            return null;
        }

        return new Point2(hit.X, hit.Z);
    }

    /// <summary>Gère une touche ; ne consomme rien, mais V, B et T (les autres outils) éteignent celui-ci.</summary>
    public void HandleKey(Key key)
    {
        if (Active && key is Key.V or Key.B or Key.T)
        {
            SetActive(false);
        }
    }

    /// <summary>Clic gauche : pose le genre choisi ; vrai s'il a été consommé.</summary>
    public bool HandleClick(Vector2 mouse)
    {
        if (!Active || Kind is null)
        {
            return false;
        }

        if (Pick(mouse) is { } point)
        {
            TryPlace(point);
        }

        return true;
    }

    /// <summary>Clic droit : vend le bâtiment visé ; vrai s'il a été consommé.</summary>
    public bool HandleRightClick(Vector2 mouse)
    {
        if (!Active)
        {
            return false;
        }

        if (Pick(mouse) is { } point && !TrySell(point))
        {
            _main!.ShowMessage("Aucun bâtiment à vendre ici.");
        }

        return true;
    }
}
