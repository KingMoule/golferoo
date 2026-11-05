using Golferoo.Sim.Economy;
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
    /// Appliquée au début du tick suivant, débitée selon les cellules qui changent réellement (<see cref="EconomyParams.PaintCost"/>)
    /// ou refusée entière par la limite de découvert (<see cref="Ledger.PurchaseRefused"/>). Une peinture qui ne laisserait aucune cellule
    /// franchissable sur la carte est ignorée. Annulable avec le reste du coup de pinceau (<see cref="UndoStroke"/>). Golfeurs et balles au repos dans une cellule devenue infranchissable sont
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

        _edits.Enqueue(new TerrainEdit(center, radius, surface, 0.0, EditKind.Paint));
    }

    /// <summary>
    /// Met en file un coup de bulldozer : le disque repeint en rough et tous ses arbres abattus, remarquables compris, au prix de la peinture et de l'abattage.
    /// Une seule retouche, annulable avec le reste du coup de pinceau comme une peinture (les arbres reviennent).
    /// </summary>
    public void Bulldoze(Point2 center, double radius)
    {
        RequireTerrain();
        if (!double.IsFinite(radius) || radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "Le rayon doit être fini et positif.");
        }

        _edits.Enqueue(new TerrainEdit(center, radius, Surface.Rough, 0.0, EditKind.Bulldoze));
    }

    /// <summary>Ce que coûterait maintenant la peinture de <paramref name="surface"/> sur ce disque, abattage des arbres retirés compris, en cents (le devis que montre l'outil).</summary>
    public long QuotePaint(Point2 center, double radius, Surface surface) => Quote(center, radius, surface, bulldoze: false);

    /// <summary>Ce que coûterait maintenant un coup de bulldozer sur ce disque, en cents.</summary>
    public long QuoteBulldoze(Point2 center, double radius) => Quote(center, radius, Surface.Rough, bulldoze: true);

    /// <summary>Ce que coûterait maintenant <see cref="RaiseTerrain"/> sur ce disque, en cents.</summary>
    public long QuoteRaise(Point2 center, double radius, double deltaMeters) => ReliefPrice(RequireTerrain().RaiseVolume(center, radius, deltaMeters));

    private long Quote(Point2 center, double radius, Surface surface, bool bulldoze)
    {
        var terrain = RequireTerrain();
        return PerCell(terrain, terrain.CountPaint(center, radius, surface), Economy.PaintCost(surface))
            + TreesLostToPaint(terrain, center, radius, surface, bulldoze).Sum(TreeRemovalCost);
    }

    /// <summary>
    /// Met en file une montée (ou baisse si négatif) du relief, profil en cosinus dans le rayon. Delta borné par appel
    /// (<see cref="TerrainGrid.MaxRaiseDelta"/>), hauteur bornée (<see cref="TerrainGrid.MinHeight"/> à <see cref="TerrainGrid.MaxHeight"/>).
    /// Appliquée au début du tick suivant, débitée selon le volume réellement déplacé (<see cref="TerrainGrid.RaiseVolume"/>) ou refusée entière
    /// par la limite de découvert. Annulable avec le reste du coup de pinceau (<see cref="UndoStroke"/>).
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

        _edits.Enqueue(new TerrainEdit(center, radius, Surface.Rough, deltaMeters, EditKind.Raise));
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
            if (e.Kind == EditKind.BeginStroke)
            {
                ForgetStroke();
                continue;
            }

            if (e.Kind == EditKind.UndoStroke)
            {
                rect = UndoStrokeNow(terrain);
            }
            else if (e.Kind is EditKind.Paint or EditKind.Bulldoze)
            {
                if (TouchesBuildings(terrain, e.Center, e.Radius))
                {
                    continue; // l'emprise du pavillon ne se repeint pas
                }

                // Prix : cellules qui changent réellement, au tarif de la surface d'arrivée, plus l'abattage des arbres que la peinture retire.
                // Refusée par la limite de découvert, rien ne change.
                var bulldoze = e.Kind == EditKind.Bulldoze;
                var lost = TreesLostToPaint(terrain, e.Center, e.Radius, e.Surface, bulldoze);
                var cost = PerCell(terrain, terrain.CountPaint(e.Center, e.Radius, e.Surface), Economy.PaintCost(e.Surface));
                var treesCost = lost.Sum(TreeRemovalCost);
                if (!terrain.KeepsWalkableCell(e.Center, e.Radius, e.Surface) || !Ledger.TrySpend(LedgerCategory.Surfaces, cost, LedgerCategory.Trees, treesCost, OverdraftFloor))
                {
                    continue;
                }

                _strokeSurfacesCost += cost;
                _strokeTreesCost += treesCost;
                foreach (var t in lost)
                {
                    _strokeTrees.Add((t, terrain.SurfaceAt(t.X, t.Z)));
                }

                rect = terrain.PaintDisc(e.Center, e.Radius, e.Surface, _strokeSurfaces);
                if (lost.Count > 0)
                {
                    var ids = lost.Select(t => t.Id).ToHashSet();
                    Trees.RemoveWhere(t => ids.Contains(t.Id));
                    NotifyTrees(terrain, e.Center.X - e.Radius, e.Center.Z - e.Radius, e.Center.X + e.Radius, e.Center.Z + e.Radius);
                }

                if (rect is { } painted && e.Surface == Surface.Trees)
                {
                    WoodlandGenerator.Fill(Trees, terrain, painted, NextWoodlandRng(), e.Center, e.Radius);
                }
            }
            else
            {
                if (TouchesBuildings(terrain, e.Center, e.Radius))
                {
                    continue; // ni ne se modèle
                }

                var cost = ReliefPrice(terrain.RaiseVolume(e.Center, e.Radius, e.Delta));
                if (!TrySpend(LedgerCategory.Relief, cost))
                {
                    continue;
                }

                _strokeReliefCost += cost;
                rect = terrain.RaiseDisc(e.Center, e.Radius, e.Delta, _strokeHeights);
                if (rect is { } raised)
                {
                    // Le sol sous les troncs a bougé : les cimes avec lui.
                    Trees.RefreshTops((raised.MinX - 1) * terrain.CellSize, (raised.MinZ - 1) * terrain.CellSize, (raised.MaxX + 2) * terrain.CellSize, (raised.MaxZ + 2) * terrain.CellSize);
                }
            }

            if (rect is { } r)
            {
                if (e.Kind != EditKind.Raise)
                {
                    RelocateStranded(terrain, r);
                }

                TerrainEdited?.Invoke(r);
            }
        }

    }

    /// <summary>
    /// Arbres que la peinture de <paramref name="surface"/> sur ce disque retire, par identifiant : ceux dont la cellule change et qui n'y tiennent plus
    /// (<see cref="CanStand"/>), ou qui perdent le sous-bois qui les portait. Le bulldozer (<paramref name="all"/>) retire en plus tous ceux du disque.
    /// </summary>
    private List<Tree> TreesLostToPaint(TerrainGrid terrain, Point2 center, double radius, Surface surface, bool all)
    {
        var near = new List<Tree>();
        Trees.Query(center.X, center.Z, radius + terrain.CellSize, near);
        var r2 = radius * radius;
        var lost = new List<Tree>();
        foreach (var t in near)
        {
            var (cx, cz) = (terrain.CellXOf(t.X), terrain.CellZOf(t.Z));
            var c = terrain.CellCenter(cx, cz);
            var before = terrain.GetCellSurface(cx, cz);
            var repainted = before != surface && Dist2(c.X, c.Z, center.X, center.Z) <= r2;
            if ((all && Dist2(t.X, t.Z, center.X, center.Z) <= r2) || (repainted && (before == Surface.Trees || !CanStand(surface))))
            {
                lost.Add(t);
            }
        }

        lost.Sort((a, b) => a.Id.CompareTo(b.Id));
        return lost;
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
            Mix(ref hash, (ulong)e.Kind);
        }
    }

    private readonly record struct TerrainEdit(Point2 Center, double Radius, Surface Surface, double Delta, EditKind Kind);

    /// <summary>Genre d'une retouche en file. Les valeurs entrent dans la sauvegarde et la somme de contrôle : ne pas réordonner.</summary>
    private enum EditKind : byte
    {
        Raise,
        Paint,
        BeginStroke,
        UndoStroke,
        Bulldoze,
    }
}
