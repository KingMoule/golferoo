using Golferoo.Sim.Buildings;
using Golferoo.Sim.Economy;
using Golferoo.Sim.Pathing;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim;

/// <summary>Issue d'une pose de bâtiment (<see cref="World.CheckBuilding"/>, <see cref="World.PlaceBuilding"/>).</summary>
public enum BuildingPlacement : byte
{
    Placed,

    /// <summary>Le pavillon ne se pose pas ici, ou le monde n'a pas de terrain.</summary>
    Unknown,

    /// <summary>Trop peu de trous ouverts.</summary>
    Locked,

    OutOfMap,

    /// <summary>Une cellule de l'emprise n'est pas du rough ou du sous-bois (départ, fairway, green, bunker, eau, chemin), ou la porte n'est pas franchissable.</summary>
    BadSurface,

    /// <summary>L'emprise touche un autre bâtiment ou la porte d'un autre.</summary>
    Occupied,

    /// <summary>Un golfeur ou une balle est dans l'emprise.</summary>
    Golfer,

    /// <summary>La porte n'a plus de chemin vers le départ du trou 1.</summary>
    NoPath,

    /// <summary>Solde insuffisant (découvert de C14 compris).</summary>
    Funds,
}

/// <summary>
/// Bâtiments et aménagements posés par le joueur (C17 lot 1) : snack-bar, pro shop, hangar d'entretien et banc. Plusieurs exemplaires de chaque genre,
/// sur le rough et sous-bois seulement ; le défrichage est facturé aux tarifs de C14 ; la vente rend la moitié du prix. Le pavillon reste à part (<see cref="Clubhouse"/>).
/// </summary>
public sealed partial class World
{
    private readonly List<Building> _placed = [];

    /// <summary>Taille, prix et déblocage des genres (<c>data/buildings/buildings.json</c>).</summary>
    public BuildingParams BuildingRules { get; private set; } = BuildingParams.Default;

    /// <summary>Bâtiments et bancs posés, dans l'ordre de pose (le pavillon n'y est pas).</summary>
    public IReadOnlyList<Building> PlacedBuildings => _placed;

    /// <summary>Vrai si assez de trous sont ouverts pour poser ce genre (<see cref="OpenHoles"/> au moins le seuil du genre).</summary>
    public bool IsUnlocked(BuildingKind kind) => BuildingRules.Spec(kind) is { } s && OpenHoles >= s.UnlockHoles;

    /// <summary>Prix de pose, hors défrichage ; nul pour le pavillon.</summary>
    public long? BuildingCost(BuildingKind kind) => BuildingRules.Spec(kind)?.Cost;

    /// <summary>Pose un bâtiment dont l'angle nord-ouest est la cellule (<paramref name="minX"/>, <paramref name="minZ"/>), la porte du côté <paramref name="side"/> (le quart de tour échange largeur et profondeur).</summary>
    public BuildingPlacement PlaceBuilding(BuildingKind kind, int minX, int minZ, DoorSide side)
    {
        var result = CheckBuilding(kind, minX, minZ, side, out var building);
        if (result != BuildingPlacement.Placed || building is null || Terrain is not { } terrain || Flow is not { } flow)
        {
            return result;
        }

        var cost = BuildingRules.Spec(kind)!.Cost;
        var cleared = ClearFootprint(terrain, building.Footprint, out var doomed, out var clearCost, out var undergrowth);
        if (!TrySpend(LedgerCategory.Buildings, cost + clearCost))
        {
            return BuildingPlacement.Funds;
        }

        if (doomed.Count > 0)
        {
            Trees.RemoveWhere(t => doomed.Contains(t.Id));
            NotifyTrees(terrain, building.Footprint.MinX * terrain.CellSize, building.Footprint.MinZ * terrain.CellSize, (building.Footprint.MaxX + 1) * terrain.CellSize, (building.Footprint.MaxZ + 1) * terrain.CellSize);
        }

        if (undergrowth)
        {
            foreach (var (x, z) in cleared)
            {
                terrain.PaintCell(x, z, Surface.Rough);
            }

            TerrainEdited?.Invoke(building.Footprint);
        }

        _placed.Add(building);
        flow.ChangeObstacle(null, building.Footprint);
        RefreshBenchBeauty();
        return BuildingPlacement.Placed;
    }

    /// <summary>
    /// Vend le bâtiment dont l'emprise contient la cellule (<paramref name="cellX"/>, <paramref name="cellZ"/>) : rend la moitié du prix (le défrichage n'est pas remboursé),
    /// libère l'emprise et le passage. Faux s'il n'y a rien à vendre (le pavillon ne se vend pas).
    /// </summary>
    public bool SellBuilding(int cellX, int cellZ)
    {
        var index = _placed.FindIndex(b => b.Footprint.Contains(cellX, cellZ));
        if (index < 0 || Flow is not { } flow)
        {
            return false;
        }

        var building = _placed[index];
        _placed.RemoveAt(index);
        Ledger.Post(LedgerCategory.Buildings, BuildingRules.Spec(building.Kind)!.Cost / 2);
        flow.ChangeObstacle(building.Footprint, null);
        RefreshBenchBeauty();
        return true;
    }

    /// <summary>Dit si la pose réussirait (sauf par manque d'argent), sans rien changer.</summary>
    public BuildingPlacement CheckBuilding(BuildingKind kind, int minX, int minZ, DoorSide side) => CheckBuilding(kind, minX, minZ, side, out _);

    private BuildingPlacement CheckBuilding(BuildingKind kind, int minX, int minZ, DoorSide side, out Building? building)
    {
        building = BuildingRules.At(kind, minX, minZ, side);
        if (building is null || Terrain is not { Blank: false } terrain || Flow is not { } flow)
        {
            return BuildingPlacement.Unknown;
        }

        if (!IsUnlocked(kind))
        {
            return BuildingPlacement.Locked;
        }

        var rect = building.Footprint;
        if (rect.MinX < 0 || rect.MinZ < 0 || rect.MaxX >= terrain.CellsX || rect.MaxZ >= terrain.CellsZ)
        {
            return BuildingPlacement.OutOfMap;
        }

        var hasDoor = kind != BuildingKind.Bench;
        var (dx, dz) = building.DoorCell;
        if (hasDoor && !terrain.IsWalkable(dx, dz))
        {
            return BuildingPlacement.BadSurface;
        }

        var others = _placed.Concat(Clubhouse is { } club ? [club] : []).ToList();
        if (others.Any(o => Overlaps(o.Footprint, rect) || (o.Kind != BuildingKind.Bench && rect.Contains(o.DoorCell.X, o.DoorCell.Z)) || (hasDoor && o.Footprint.Contains(dx, dz))))
        {
            return BuildingPlacement.Occupied;
        }

        for (var z = rect.MinZ; z <= rect.MaxZ; z++)
        {
            for (var x = rect.MinX; x <= rect.MaxX; x++)
            {
                if (terrain.GetCellSurface(x, z) is not (Surface.Rough or Surface.Trees))
                {
                    return BuildingPlacement.BadSurface;
                }
            }
        }

        bool Inside(Point2 p) => rect.Contains(terrain.CellXOf(p.X), terrain.CellZOf(p.Z));
        if (_groups.SelectMany(g => g.Golfers).Concat(_roamers).Any(g => Inside(g.Position) || Inside(g.Ball)))
        {
            return BuildingPlacement.Golfer;
        }

        // Un chemin du départ du trou 1 à chaque porte, la sienne et celles des bâtiments déjà posés (pavillon compris), l'emprise comprise : une pose n'enferme personne.
        var fromTee = FlowField.Build(terrain, flow.CostsWith(null, rect), terrain.CellXOf(terrain.Tee.X), terrain.CellZOf(terrain.Tee.Z));
        if ((hasDoor && !fromTee.IsReachable(dx, dz)) || others.Any(o => o.Kind != BuildingKind.Bench && !fromTee.IsReachable(o.DoorCell.X, o.DoorCell.Z)))
        {
            return BuildingPlacement.NoPath;
        }

        return BuildingPlacement.Placed;
    }

    private static bool Overlaps(CellRect a, CellRect b) => a.MinX <= b.MaxX && b.MinX <= a.MaxX && a.MinZ <= b.MaxZ && b.MinZ <= a.MaxZ;

    /// <summary>Arbres de l'emprise (abattus aux tarifs de C14) et cellules de sous-bois (facturées au mètre carré) ; <paramref name="clearCost"/> est le défrichage total.</summary>
    private List<(int X, int Z)> ClearFootprint(TerrainGrid terrain, CellRect rect, out HashSet<int> doomed, out long clearCost, out bool undergrowth)
    {
        var middle = new Point2((rect.MinX + rect.MaxX + 1) * 0.5 * terrain.CellSize, (rect.MinZ + rect.MaxZ + 1) * 0.5 * terrain.CellSize);
        var reach = (Math.Max(rect.MaxX - rect.MinX, rect.MaxZ - rect.MinZ) + 3) * terrain.CellSize;
        var near = new List<Tree>();
        Trees.Query(middle.X, middle.Z, reach, near);
        doomed = [];
        clearCost = 0;
        foreach (var t in near.Where(t => rect.Contains(terrain.CellXOf(t.X), terrain.CellZOf(t.Z))).OrderBy(t => t.Id))
        {
            doomed.Add(t.Id);
            clearCost += t.State switch
            {
                TreeState.Standing => Economy.RazeCost(t),
                TreeState.Fallen => Economy.TreeClearFallen,
                _ => 0,
            };
        }

        var cells = new List<(int X, int Z)>();
        for (var z = rect.MinZ; z <= rect.MaxZ; z++)
        {
            for (var x = rect.MinX; x <= rect.MaxX; x++)
            {
                if (terrain.GetCellSurface(x, z) == Surface.Trees)
                {
                    cells.Add((x, z));
                }
            }
        }

        clearCost += PerCell(terrain, cells.Count, Economy.TreeClearPerM2);
        undergrowth = cells.Count > 0;
        return cells;
    }

    /// <summary>Vrai si le disque (centre, rayon en mètres) touche l'emprise d'un bâtiment posé ou du pavillon : une retouche du sol n'y a pas lieu.</summary>
    private bool TouchesBuildings(TerrainGrid terrain, Point2 center, double radius)
    {
        var cs = terrain.CellSize;
        foreach (var r in _placed.Select(b => b.Footprint).Concat(Clubhouse is { } c ? [c.Footprint] : []))
        {
            var dx = center.X - Math.Clamp(center.X, r.MinX * cs, (r.MaxX + 1) * cs);
            var dz = center.Z - Math.Clamp(center.Z, r.MinZ * cs, (r.MaxZ + 1) * cs);
            if ((dx * dx) + (dz * dz) <= radius * radius)
            {
                return true;
            }
        }

        return false;
    }

    private void MixBuildings(ref ulong hash)
    {
        if (_placed.Count == 0)
        {
            return;
        }

        Mix(ref hash, (ulong)_placed.Count);
        foreach (var b in _placed)
        {
            b.Mix(ref hash);
        }
    }
}
