using Golferoo.Sim.Buildings;
using Golferoo.Sim.Economy;
using Golferoo.Sim.Golfers;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim;

/// <summary>
/// Visites (C17 lot 2) : un visiteur du vivier se voit proposer le pro shop avant son premier départ et le snack-bar au virage. Un tirage selon son profil
/// décide s'il y va ; il marche jusqu'à la porte du bâtiment le plus proche à portée, y paie (grand livre, catégorie Bâtiments), repart plus content
/// (<see cref="MoodEvent.ServiceVisited"/>) et reprend sa marche. Un genre n'est proposé qu'une fois par partie, jamais à un groupe déjà signalé lent.
/// Le hangar baisse l'entretien mensuel et sert de départ aux tondeuses ; le banc embellit les environs.
/// </summary>
public sealed partial class World
{
    /// <summary>Beauté ajoutée par les bancs posés (nulle sans banc), lue par <see cref="BeautyMap"/>.</summary>
    internal BeautyAccent? BenchAccent { get; private set; }

    /// <summary>Porte du hangar le plus proche de <paramref name="from"/>, ou celle du pavillon sans hangar : d'où partent les tondeuses. Nul sans pavillon.</summary>
    public Point2? MowerDepot(Point2 from)
    {
        if (Terrain is not { } terrain)
        {
            return null;
        }

        var sheds = _placed.Where(b => b.Kind == BuildingKind.MaintenanceShed).Select(b => b.DoorPosition(terrain.CellSize)).ToList();
        return sheds.Count > 0 ? sheds.MinBy(d => Dist2(d, from)) : Clubhouse?.DoorPosition(terrain.CellSize);
    }

    /// <summary>Le monde a au moins un hangar : l'entretien mensuel baisse (une fois, quel que soit leur nombre).</summary>
    public bool HasMaintenanceShed => _placed.Any(b => b.Kind == BuildingKind.MaintenanceShed);

    private void RefreshBenchBeauty()
    {
        var cs = Terrain?.CellSize ?? 1.0;
        var spots = _placed.Where(b => b.Kind == BuildingKind.Bench).Select(b => b.Position(cs)).ToList();
        BenchAccent = spots.Count == 0 ? null : new BeautyAccent(spots, BuildingRules.BenchBeauty.Weight, BuildingRules.BenchBeauty.RadiusMeters);
        if (_resolver is PhysicalShotResolver physical)
        {
            physical.BeautyAccent = BenchAccent;
        }
    }

    /// <summary>Entretien mensuel après la remise du hangar.</summary>
    private long WithShed(long maintenance) =>
        HasMaintenanceShed ? maintenance * (10_000 - BuildingRules.ShedMaintenanceBasisPoints) / 10_000 : maintenance;

    /// <summary>Le groupe part de la file : chacun se voit proposer le pro shop.</summary>
    private void OfferProShop(Group group)
    {
        foreach (var g in group.Golfers)
        {
            Offer(group, g, BuildingKind.ProShop);
        }
    }

    /// <summary>
    /// Le groupe vient de finir le trou <see cref="Group.Hole"/> : au virage (le trou du milieu, ou le dernier d'un parcours court, ou
    /// <paramref name="last"/> pour la fin de la partie) chacun se voit proposer le snack-bar.
    /// </summary>
    private void OfferSnackBar(Group group, int completed, bool last)
    {
        var holes = group.Golfers.Select(g => g.Card?.HoleCount ?? 0).Max();
        var turn = holes >= BuildingRules.TurnMinHoles ? (holes + 1) / 2 : holes;
        if (completed < turn && !last)
        {
            return;
        }

        foreach (var g in group.Active)
        {
            Offer(group, g, BuildingKind.SnackBar);
        }
    }

    private void Offer(Group group, Golfer g, BuildingKind kind)
    {
        var bit = (byte)(1 << (int)kind);
        if (!HasMood(g) || g.Quit || group.LeagueRound >= 0 || (g.Offered & bit) != 0 || Terrain is not { } terrain || !_placed.Exists(b => b.Kind == kind))
        {
            return;
        }

        g.Offered |= bit;
        var building = NearestInRange(terrain, g.Position, kind);
        if (building is null || group.Golfers.Any(o => o.Occurrences[(int)MoodEvent.SlowPlay] > 0) || _arrivalRng.NextDouble() >= BuildingRules.Chance(kind, g.Profile))
        {
            return;
        }

        g.VisitKind = kind;
        g.VisitDoor = building.DoorPosition(terrain.CellSize);
        g.Resume = g.State;
        g.State = GolferState.Visiting;
    }

    /// <summary>Le bâtiment de ce genre dont la porte est la plus proche du point et à portée (en cellules), le premier posé en cas d'égalité.</summary>
    private Building? NearestInRange(TerrainGrid terrain, Point2 from, BuildingKind kind)
    {
        var range = (long)BuildingRules.Spec(kind)!.Range;
        var (fx, fz) = CellOf(from);
        Building? best = null;
        var nearest = long.MaxValue;
        foreach (var b in _placed.Where(b => b.Kind == kind))
        {
            var (dx, dz) = b.DoorCell;
            var d = ((long)(dx - fx) * (dx - fx)) + ((long)(dz - fz) * (dz - fz));
            if (d <= range * range && d < nearest)
            {
                (best, nearest) = (b, d);
            }
        }

        return best;
    }

    /// <summary>Les visiteurs en route marchent vers leur porte ; arrivés, ils paient (si le bâtiment est toujours là) et reprennent leur marche.</summary>
    private void StepVisits(TerrainGrid terrain, Group group)
    {
        foreach (var g in group.Golfers)
        {
            if (g.State != GolferState.Visiting || !Walk(terrain, g, g.VisitDoor))
            {
                continue;
            }

            var door = CellOf(g.VisitDoor);
            if (_placed.Exists(b => b.Kind == g.VisitKind && b.DoorCell == door))
            {
                Ledger.Post(LedgerCategory.Buildings, BuildingRules.Spec(g.VisitKind)!.Revenue);
                if (g.VisitKind == BuildingKind.ProShop)
                {
                    g.Skill *= BuildingRules.ProShopSkillFactor;
                }

                Raise(g, MoodEvent.ServiceVisited, group.Hole, g.VisitDoor);
            }

            g.State = g.Resume;
        }
    }
}
