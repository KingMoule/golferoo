using Golferoo.Sim.Buildings;
using Golferoo.Sim.Economy;
using Golferoo.Sim.Golfers;
using Golferoo.Sim.Pathing;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim;

/// <summary>
/// Pavillon (C15 lot 1) : posé automatiquement près du départ du trou 1 à la nouvelle partie, déplaçable contre <see cref="EconomyParams.ClubhouseMove"/>.
/// Son emprise bloque les marcheurs (champs de flux) et les arbres ; le champ de flux vers sa porte reste en cache.
/// </summary>
public sealed partial class World
{
    /// <summary>Dénivelé maximal sous l'emprise, en mètres : un terrain « plat ».</summary>
    public const double ClubhouseMaxRelief = 1.0;

    /// <summary>Le pavillon, nul sans terrain, sur un terrain vierge ou si le monde n'en place pas (<c>placeClubhouse: false</c>).</summary>
    public Building? Clubhouse { get; private set; }

    /// <summary>Champ de flux vers la porte du pavillon (en cache) ; nul sans pavillon.</summary>
    public FlowField? ClubhouseField => Clubhouse is { } c && Terrain is { } t && Flow is { } f ? f.Get(c.DoorPosition(t.CellSize)) : null;

    /// <summary>
    /// Déplace le pavillon pour que son centre soit au plus près de <paramref name="center"/>, la porte du côté <paramref name="door"/>.
    /// Refusé sans débit si l'emplacement est invalide (voir <see cref="CanPlaceClubhouse"/>), inchangé, ou si le solde ne le permet pas.
    /// Seuls les champs de flux que le changement touche sont à refaire.
    /// </summary>
    public bool TryMoveClubhouse(Point2 center, DoorSide door)
    {
        if (Terrain is not { Blank: false } terrain || Flow is not { } flow)
        {
            return false;
        }

        var target = Buildings.Clubhouse.At(
            (int)Math.Round((center.X / terrain.CellSize) - (Buildings.Clubhouse.Width / 2.0)),
            (int)Math.Round((center.Z / terrain.CellSize) - (Buildings.Clubhouse.Depth / 2.0)),
            door);
        if (target == Clubhouse || !CanPlaceClubhouse(target) || !TrySpend(LedgerCategory.Buildings, Economy.ClubhouseMove))
        {
            return false;
        }

        flow.ChangeObstacle(Clubhouse?.Footprint, target.Footprint);
        Clubhouse = target;
        _ = ClubhouseField; // le champ vers la nouvelle porte est mis en cache
        return true;
    }

    /// <summary>
    /// Vrai si <paramref name="b"/> tient sur le terrain : emprise et porte dans la carte, sur du fairway, du rough ou un chemin (la porte sur toute surface franchissable),
    /// sol de dénivelé au plus <see cref="ClubhouseMaxRelief"/>, ni arbre ni golfeur sur l'emprise, et un chemin de la porte au départ du trou 1.
    /// </summary>
    public bool CanPlaceClubhouse(Building b)
    {
        if (Terrain is not { } terrain || Flow is not { } flow)
        {
            return false;
        }

        var rect = b.Footprint;
        var (dx, dz) = b.DoorCell;
        if (rect.MinX < 0 || rect.MinZ < 0 || rect.MaxX >= terrain.CellsX || rect.MaxZ >= terrain.CellsZ || !terrain.IsWalkable(dx, dz))
        {
            return false;
        }

        double low = double.MaxValue, high = double.MinValue;
        for (var z = rect.MinZ; z <= rect.MaxZ; z++)
        {
            for (var x = rect.MinX; x <= rect.MaxX; x++)
            {
                if (terrain.GetCellSurface(x, z) is not (Surface.Fairway or Surface.Rough or Surface.Path))
                {
                    return false;
                }

                var h = terrain.Heights.Sample((x + 0.5) * terrain.CellSize, (z + 0.5) * terrain.CellSize);
                low = Math.Min(low, h);
                high = Math.Max(high, h);
            }
        }

        if (high - low > ClubhouseMaxRelief)
        {
            return false;
        }

        // Ni arbre (debout, souche ou tronc couché, au pied) sur l'emprise et sa bordure d'une cellule, ni golfeur ou balle dedans.
        var zone = rect.Expand(1, terrain.CellsX, terrain.CellsZ);
        var middle = b.Position(terrain.CellSize);
        var near = new List<Golferoo.Sim.Trees.Tree>();
        Trees.Query(middle.X, middle.Z, (Math.Max(Buildings.Clubhouse.Width, Buildings.Clubhouse.Depth) + 4) * terrain.CellSize, near);
        if (near.Any(t => zone.Contains(terrain.CellXOf(t.X), terrain.CellZOf(t.Z))))
        {
            return false;
        }

        bool Inside(Point2 p) => rect.Contains(terrain.CellXOf(p.X), terrain.CellZOf(p.Z));
        if (_groups.SelectMany(g => g.Golfers).Concat(_roamers).Any(g => Inside(g.Position) || Inside(g.Ball)))
        {
            return false;
        }

        // Un chemin de la porte au départ du trou 1, l'emprise comprise.
        var field = FlowField.Build(terrain, flow.CostsWith(Clubhouse?.Footprint, rect), dx, dz);
        return field.IsReachable(terrain.CellXOf(terrain.Tee.X), terrain.CellZOf(terrain.Tee.Z));
    }

    /// <summary>Vrai si le disque (centre, rayon en mètres) touche l'emprise du pavillon : une retouche du sol n'y a pas lieu.</summary>
    private bool TouchesClubhouse(TerrainGrid terrain, Point2 center, double radius)
    {
        if (Clubhouse is not { } c)
        {
            return false;
        }

        var (cs, r) = (terrain.CellSize, c.Footprint);
        var dx = center.X - Math.Clamp(center.X, r.MinX * cs, (r.MaxX + 1) * cs);
        var dz = center.Z - Math.Clamp(center.Z, r.MinZ * cs, (r.MaxZ + 1) * cs);
        return (dx * dx) + (dz * dz) <= radius * radius;
    }

    /// <summary>Place le pavillon au plus près du départ du trou 1 (cellules de centre par anneaux, la porte tournée vers le départ d'abord). Faux s'il n'y a nulle part.</summary>
    private bool PlaceClubhouseNearTee()
    {
        if (Terrain is not { Blank: false } terrain || Flow is not { } flow)
        {
            return false;
        }

        var (tx, tz) = (terrain.CellXOf(terrain.Tee.X), terrain.CellZOf(terrain.Tee.Z));
        for (var ring = 0; ring <= Math.Max(terrain.CellsX, terrain.CellsZ); ring++)
        {
            var spots = new List<(int X, int Z)>();
            for (var z = tz - ring; z <= tz + ring; z++)
            {
                for (var x = tx - ring; x <= tx + ring; x++)
                {
                    if (Math.Max(Math.Abs(x - tx), Math.Abs(z - tz)) == ring)
                    {
                        spots.Add((x, z));
                    }
                }
            }

            foreach (var (x, z) in spots.OrderBy(s => ((long)(s.X - tx) * (s.X - tx)) + ((long)(s.Z - tz) * (s.Z - tz))).ThenBy(s => s.Z).ThenBy(s => s.X))
            {
                var minX = x - (Buildings.Clubhouse.Width / 2);
                var minZ = z - (Buildings.Clubhouse.Depth / 2);
                foreach (var side in SidesFacing(x - tx, z - tz))
                {
                    var b = Buildings.Clubhouse.At(minX, minZ, side);
                    if (CanPlaceClubhouse(b))
                    {
                        flow.ChangeObstacle(null, b.Footprint);
                        Clubhouse = b;
                        _ = ClubhouseField;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>Les quatre côtés, celui qui regarde le départ d'abord (le pavillon est à (<paramref name="dx"/>, <paramref name="dz"/>) cellules du départ).</summary>
    private static DoorSide[] SidesFacing(int dx, int dz)
    {
        DoorSide horizontal = dx > 0 ? DoorSide.West : DoorSide.East;
        DoorSide vertical = dz > 0 ? DoorSide.North : DoorSide.South;
        var opposite = (DoorSide s) => s switch { DoorSide.West => DoorSide.East, DoorSide.East => DoorSide.West, DoorSide.North => DoorSide.South, _ => DoorSide.North };
        return Math.Abs(dx) >= Math.Abs(dz)
            ? [horizontal, vertical, opposite(vertical), opposite(horizontal)]
            : [vertical, horizontal, opposite(horizontal), opposite(vertical)];
    }

    private void MixClubhouse(ref ulong hash) => Clubhouse?.Mix(ref hash);
}
