using Golferoo.Sim.Economy;
using Golferoo.Sim.Save;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim;

/// <summary>
/// Annulation du dernier coup de pinceau (C14) : la sim garde l'état d'avant des cellules et des coins touchés par les peintures et
/// les reliefs appliqués depuis <see cref="BeginStroke"/>, et ce qu'ils ont coûté. Le jeu délimite le coup (bouton pressé puis relâché)
/// et n'offre l'annulation que 5 secondes réelles. Toute autre commande de terrain (arbres, trous) efface cette mémoire.
/// </summary>
public sealed partial class World
{
    private readonly Dictionary<int, Surface> _strokeSurfaces = [];
    private readonly Dictionary<int, double> _strokeHeights = [];
    private long _strokeSurfacesCost;
    private long _strokeReliefCost;

    /// <summary>Vrai si le dernier coup de pinceau appliqué a changé quelque chose et peut encore être annulé.</summary>
    public bool CanUndoStroke => _strokeSurfaces.Count > 0 || _strokeHeights.Count > 0;

    /// <summary>Ce qu'a coûté le coup de pinceau en cours ou le dernier, en cents (rendu à l'annulation).</summary>
    public long StrokeCost => _strokeSurfacesCost + _strokeReliefCost;

    /// <summary>Met en file le début d'un coup de pinceau : les peintures et reliefs suivants forment le coup qu'annule <see cref="UndoStroke"/>.</summary>
    public void BeginStroke()
    {
        RequireTerrain();
        _edits.Enqueue(new TerrainEdit(default, 0.0, Surface.Rough, 0.0, EditKind.BeginStroke));
    }

    /// <summary>
    /// Met en file l'annulation du dernier coup de pinceau : cellules, hauteurs et argent reviennent à leur état d'avant le coup.
    /// Les arbres rasés par la peinture ne reviennent pas ; ceux plantés par une peinture de sous-bois disparaissent avec lui.
    /// Sans coup à annuler, rien ne se passe.
    /// </summary>
    public void UndoStroke()
    {
        RequireTerrain();
        _edits.Enqueue(new TerrainEdit(default, 0.0, Surface.Rough, 0.0, EditKind.UndoStroke));
    }

    private void ForgetStroke()
    {
        _strokeSurfaces.Clear();
        _strokeHeights.Clear();
        _strokeSurfacesCost = 0;
        _strokeReliefCost = 0;
    }

    /// <summary>Remet le terrain d'avant le coup et rend son prix. Rend le rectangle des cellules touchées, ou null s'il n'y avait rien à annuler.</summary>
    private CellRect? UndoStrokeNow(TerrainGrid terrain)
    {
        // Le sous-bois qui disparaît emporte ses arbres, comme quand on le repeint.
        var lostUnderbrush = _strokeSurfaces.Where(c => c.Value != Surface.Trees && terrain.Surfaces[c.Key] == Surface.Trees).Select(c => c.Key).ToHashSet();
        var rect = terrain.Restore(_strokeSurfaces, _strokeHeights);
        if (lostUnderbrush.Count > 0)
        {
            Trees.RemoveWhere(t => lostUnderbrush.Contains((terrain.CellZOf(t.Z) * terrain.CellsX) + terrain.CellXOf(t.X)));
        }

        if (rect is { } r && _strokeHeights.Count > 0)
        {
            Trees.RefreshTops((r.MinX - 1) * terrain.CellSize, (r.MinZ - 1) * terrain.CellSize, (r.MaxX + 2) * terrain.CellSize, (r.MaxZ + 2) * terrain.CellSize);
        }

        Ledger.Post(LedgerCategory.Surfaces, _strokeSurfacesCost);
        Ledger.Post(LedgerCategory.Relief, _strokeReliefCost);
        ForgetStroke();
        return rect;
    }

    private void MixStroke(ref ulong hash)
    {
        if (!CanUndoStroke && StrokeCost == 0)
        {
            return;
        }

        Mix(ref hash, (ulong)_strokeSurfacesCost);
        Mix(ref hash, (ulong)_strokeReliefCost);
        Mix(ref hash, (ulong)_strokeSurfaces.Count);
        foreach (var key in _strokeSurfaces.Keys.Order())
        {
            Mix(ref hash, ((ulong)(uint)key << 8) | (byte)_strokeSurfaces[key]);
        }

        Mix(ref hash, (ulong)_strokeHeights.Count);
        foreach (var key in _strokeHeights.Keys.Order())
        {
            Mix(ref hash, (ulong)key);
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(_strokeHeights[key]));
        }
    }

    private void WriteStroke(SaveWriter w)
    {
        w.Long(_strokeSurfacesCost);
        w.Long(_strokeReliefCost);
        w.List([.. _strokeSurfaces.Keys.Order()], key =>
        {
            w.Int(key);
            w.Byte((byte)_strokeSurfaces[key]);
        });
        w.List([.. _strokeHeights.Keys.Order()], key =>
        {
            w.Int(key);
            w.Double(_strokeHeights[key]);
        });
    }

    private void ReadStroke(SaveReader r)
    {
        _strokeSurfacesCost = r.Long();
        _strokeReliefCost = r.Long();
        var cells = Terrain is null ? 0 : Terrain.CellsX * Terrain.CellsZ;
        var corners = Terrain is null ? 0 : (Terrain.CellsX + 1) * (Terrain.CellsZ + 1);
        foreach (var (key, surface) in r.List(() => (r.Int(), (Surface)r.Byte())))
        {
            if (key < 0 || key >= cells || (int)surface >= SurfaceTable.Count || !_strokeSurfaces.TryAdd(key, surface))
            {
                throw new InvalidDataException("Sauvegarde incohérente : cellule du coup de pinceau hors du terrain.");
            }
        }

        foreach (var (key, height) in r.List(() => (r.Int(), r.Double())))
        {
            if (key < 0 || key >= corners || !double.IsFinite(height) || !_strokeHeights.TryAdd(key, height))
            {
                throw new InvalidDataException("Sauvegarde incohérente : coin du coup de pinceau hors du terrain.");
            }
        }
    }
}
