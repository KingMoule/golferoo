using Golferoo.Sim.Golfers;
using Golferoo.Sim.Pathing;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim;

public sealed partial class World
{
    private readonly Queue<TerrainEdit> _edits = new();

    /// <summary>Levé au début d'un tick, après l'application d'une retouche, avec le rectangle de cellules modifiées (le rendu reconstruit).</summary>
    public event Action<CellRect>? TerrainEdited;

    /// <summary>Nombre de retouches en attente du prochain tick.</summary>
    public int PendingEdits => _edits.Count;

    /// <summary>
    /// Met en file la peinture de <paramref name="surface"/> sur les cellules dont le centre est dans le rayon (mètres, coordonnées monde).
    /// Appliquée au début du tick suivant, sans coût ni annulation. Une peinture qui ne laisserait aucune cellule
    /// franchissable sur la carte est ignorée. Golfeurs et balles au repos dans une cellule devenue infranchissable sont
    /// reposés sur la cellule franchissable la plus proche, sans pénalité ; une balle en vol qui retombe dans une telle
    /// cellule est reposée de même à l'atterrissage.
    /// </summary>
    public void PaintSurface(Point2 center, double radius, Surface surface)
    {
        RequireTerrain();
        if (!double.IsFinite(radius) || radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "Le rayon doit être fini et positif.");
        }

        _edits.Enqueue(new TerrainEdit(center, radius, surface, 0.0, IsPaint: true));
    }

    /// <summary>
    /// Met en file une montée (ou baisse si négatif) du relief, profil en cosinus dans le rayon. Delta borné par appel
    /// (<see cref="TerrainGrid.MaxRaiseDelta"/>), hauteur bornée (<see cref="TerrainGrid.MinHeight"/> à <see cref="TerrainGrid.MaxHeight"/>).
    /// Appliquée au début du tick suivant.
    /// </summary>
    public void RaiseTerrain(Point2 center, double radius, double deltaMeters)
    {
        RequireTerrain();
        if (!double.IsFinite(radius) || radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "Le rayon doit être fini et positif.");
        }

        if (!double.IsFinite(deltaMeters))
        {
            throw new ArgumentOutOfRangeException(nameof(deltaMeters), deltaMeters, "Le delta doit être fini.");
        }

        _edits.Enqueue(new TerrainEdit(center, radius, Surface.Rough, deltaMeters, IsPaint: false));
    }

    private void ApplyPendingEdits()
    {
        if (_edits.Count == 0 || Terrain is null)
        {
            return;
        }

        var terrain = Terrain;
        while (_edits.Count > 0)
        {
            var e = _edits.Dequeue();
            CellRect? rect;
            if (e.IsPaint)
            {
                // Arbres posés sur du sous-bois dans la zone : ceux qui perdent leur sous-bois sont retirés.
                var onUnderbrush = e.Surface == Surface.Trees ? null : TreesOnUnderbrush(terrain, e.Center, e.Radius);
                rect = terrain.KeepsWalkableCell(e.Center, e.Radius, e.Surface)
                    ? terrain.PaintDisc(e.Center, e.Radius, e.Surface)
                    : null;
                if (rect is { } painted)
                {
                    if (e.Surface == Surface.Trees)
                    {
                        WoodlandGenerator.Fill(Trees, terrain, painted, NextWoodlandRng(), e.Center, e.Radius);
                    }
                    else if (onUnderbrush is { Count: > 0 })
                    {
                        Trees.RemoveWhere(t => onUnderbrush.Contains(t.Id) && terrain.SurfaceAt(t.X, t.Z) != Surface.Trees);
                    }
                }
            }
            else
            {
                rect = terrain.RaiseDisc(e.Center, e.Radius, e.Delta);
            }

            if (rect is { } r)
            {
                if (e.IsPaint)
                {
                    RelocateStranded(terrain, r);
                }

                TerrainEdited?.Invoke(r);
            }
        }

    }

    private HashSet<int> TreesOnUnderbrush(TerrainGrid terrain, Point2 center, double radius)
    {
        var near = new List<Tree>();
        Trees.Query(center.X, center.Z, radius + terrain.CellSize, near);
        var ids = new HashSet<int>();
        foreach (var t in near)
        {
            if (terrain.SurfaceAt(t.X, t.Z) == Surface.Trees)
            {
                ids.Add(t.Id);
            }
        }

        return ids;
    }

    /// <summary>
    /// Repose sur la cellule franchissable la plus proche les golfeurs, et les balles au repos, qui se trouvent
    /// dans une cellule devenue infranchissable. Une balle en vol garde la trajectoire déjà calculée.
    /// </summary>
    private void RelocateStranded(TerrainGrid terrain, CellRect rect)
    {
        foreach (var group in _groups)
        {
            foreach (var g in group.Golfers)
            {
                Relocate(terrain, rect, g);
            }
        }

        foreach (var g in _roamers)
        {
            Relocate(terrain, rect, g);
        }
    }

    private static void Relocate(TerrainGrid terrain, CellRect rect, Golfer g)
    {
        var cx = terrain.CellXOf(g.Position.X);
        var cz = terrain.CellZOf(g.Position.Z);
        if (rect.Contains(cx, cz) && !terrain.IsWalkable(cx, cz))
        {
            g.Position = GridQueries.NearestWalkablePoint(terrain, g.Position);
        }

        if (g.State == GolferState.BallInFlight)
        {
            return;
        }

        cx = terrain.CellXOf(g.Ball.X);
        cz = terrain.CellZOf(g.Ball.Z);
        if (rect.Contains(cx, cz) && !terrain.IsWalkable(cx, cz))
        {
            g.Ball = GridQueries.NearestWalkablePoint(terrain, g.Ball);
        }
    }

    private void MixPendingEdits(ref ulong hash)
    {
        Mix(ref hash, (ulong)_edits.Count);
        foreach (var e in _edits)
        {
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(e.Center.X));
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(e.Center.Z));
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(e.Radius));
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(e.Delta));
            Mix(ref hash, (ulong)e.Surface);
            Mix(ref hash, e.IsPaint ? 1UL : 0UL);
        }
    }

    private readonly record struct TerrainEdit(Point2 Center, double Radius, Surface Surface, double Delta, bool IsPaint);
}
