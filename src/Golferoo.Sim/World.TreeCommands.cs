using Golferoo.Sim.Economy;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim;

/// <summary>
/// Commandes sur les arbres, en file comme <see cref="PaintSurface"/> : appliquées au début du tick suivant, dans l'ordre,
/// comptées dans <see cref="Checksum"/>, débitées au grand livre (catégorie Arbres) aux prix de <see cref="Economy"/>. Une commande refusée
/// (arbre absent, lieu interdit, ou limite de découvert, qui lève aussi <see cref="Ledger.PurchaseRefused"/>) est comptée dans
/// <see cref="TreeCommandsRefused"/> et ne coûte rien.
/// Un tronc tombé et une souche n'arrêtent pas la balle dans ce lot et ne comptent pas dans <see cref="TreeStand.MaxTopNear"/> :
/// la collision avec les troncs couchés est hors périmètre.
/// </summary>
public sealed partial class World
{
    private readonly Queue<TreeCommand> _treeCommands = new();

    /// <summary>Levé après une commande qui a changé des arbres, avec le rectangle de cellules touché (le rendu reconstruit ces blocs).</summary>
    public event Action<CellRect>? TreesEdited;

    /// <summary>Levé quand un arbre vient de tomber (état <see cref="TreeState.Fallen"/>, direction dans <see cref="Tree.FallYaw"/>) : le rendu joue la chute.</summary>
    public event Action<Tree>? TreeFelled;

    /// <summary>Commandes d'arbres refusées depuis le début.</summary>
    public int TreeCommandsRefused { get; private set; }

    /// <summary>Plantations acceptées depuis le début (le tutoriel de C07 y lit l'étape de la pousse).</summary>
    public int TreesPlanted { get; private set; }

    /// <summary>Commandes d'arbres en attente du prochain tick.</summary>
    public int PendingTreeCommands => _treeCommands.Count;

    /// <summary>Retire un arbre debout. Aucun champ de flux n'est recalculé (la surface ne change pas). Coût : abattage, doublé pour un remarquable.</summary>
    public void RazeTree(int id)
    {
        RequireTerrain();
        _treeCommands.Enqueue(new TreeCommand(TreeCommandKind.RazeTree, id, 0, 0, 0, false, TreeSpeciesId.Oak, TreePlantStage.Sapling));
    }

    /// <summary>
    /// Défriche le disque : retire les arbres (sauf les remarquables si <paramref name="keepRemarkable"/>) et change en Rough les cellules de
    /// sous-bois (surface Trees) du disque. Touche les champs de flux comme une peinture.
    /// </summary>
    public void RazeArea(Point2 center, double radius, bool keepRemarkable)
    {
        RequireTerrain();
        RequireRadius(radius);
        _treeCommands.Enqueue(new TreeCommand(TreeCommandKind.RazeArea, 0, center.X, center.Z, radius, keepRemarkable, TreeSpeciesId.Oak, TreePlantStage.Sapling));
    }

    /// <summary>
    /// Plante un arbre (plant d'âge 0 ou motte de 8 à 10 ans). Refusée sur l'eau, le green, le tee, le bunker, le sous-bois, hors carte,
    /// ou à moins de <see cref="WoodlandGenerator.MinSpacingFactor"/> fois la somme des rayons de couronne d'un arbre debout. Ne touche jamais les champs de flux.
    /// </summary>
    public void PlantTree(double x, double z, TreeSpeciesId species, TreePlantStage stage)
    {
        RequireTerrain();
        if (!double.IsFinite(x) || !double.IsFinite(z))
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, "La position doit être finie.");
        }

        _ = TreeSpeciesTable.Get(species);
        _treeCommands.Enqueue(new TreeCommand(TreeCommandKind.Plant, 0, x, z, 0, false, species, stage));
    }

    /// <summary>Fait tomber un arbre debout, couché vers <paramref name="direction"/> (radians dans le plan : cos = X, sin = Z). Gratuit ; C04 l'appellera par grand vent.</summary>
    public void FellTree(int id, double direction)
    {
        RequireTerrain();
        if (!double.IsFinite(direction))
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "La direction doit être finie.");
        }

        _treeCommands.Enqueue(new TreeCommand(TreeCommandKind.Fell, id, 0, 0, direction, false, TreeSpeciesId.Oak, TreePlantStage.Sapling));
    }

    /// <summary>Dégage un tronc tombé : il devient une souche. Coût : <see cref="EconomyParams.TreeClearFallen"/>.</summary>
    public void ClearFallen(int id)
    {
        RequireTerrain();
        _treeCommands.Enqueue(new TreeCommand(TreeCommandKind.ClearFallen, id, 0, 0, 0, false, TreeSpeciesId.Oak, TreePlantStage.Sapling));
    }

    private static void RequireRadius(double radius)
    {
        if (!double.IsFinite(radius) || radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "Le rayon doit être fini et positif.");
        }
    }

    private void ApplyTreeCommands()
    {
        if (_treeCommands.Count == 0 || Terrain is null)
        {
            return;
        }

        var terrain = Terrain;
        ForgetStroke(); // une autre commande de terrain : le dernier coup de pinceau ne s'annule plus
        while (_treeCommands.Count > 0)
        {
            var c = _treeCommands.Dequeue();
            var ok = c.Kind switch
            {
                TreeCommandKind.RazeTree => ApplyRazeTree(terrain, c.Id),
                TreeCommandKind.RazeArea => ApplyRazeArea(terrain, new Point2(c.X, c.Z), c.Radius, c.Flag),
                TreeCommandKind.Plant => ApplyPlant(terrain, c),
                TreeCommandKind.Fell => ApplyFell(terrain, c.Id, c.Radius),
                TreeCommandKind.ClearFallen => ApplyClearFallen(terrain, c.Id),
                _ => false,
            };
            if (!ok)
            {
                TreeCommandsRefused++;
            }
            else if (c.Kind == TreeCommandKind.Plant)
            {
                TreesPlanted++;
            }
        }
    }

    private bool ApplyRazeTree(TerrainGrid terrain, int id)
    {
        if (!Trees.TryGet(id, out var tree) || tree.State != TreeState.Standing)
        {
            return false;
        }

        if (!TrySpend(LedgerCategory.Trees, Economy.RazeCost(tree)))
        {
            return false;
        }

        Trees.Remove(id);
        NotifyTrees(terrain, tree.X, tree.Z, tree.X, tree.Z);
        return true;
    }

    private bool ApplyRazeArea(TerrainGrid terrain, Point2 center, double radius, bool keepRemarkable)
    {
        var near = new List<Tree>();
        Trees.Query(center.X, center.Z, radius, near);
        var doomed = new HashSet<int>();
        var cost = 0L;
        foreach (var t in near)
        {
            if (keepRemarkable && t.Remarkable)
            {
                continue;
            }

            doomed.Add(t.Id);
            cost += t.State switch
            {
                TreeState.Standing => Economy.RazeCost(t),
                TreeState.Fallen => Economy.TreeClearFallen,
                _ => 0,
            };
        }

        var cells = terrain.CountPaint(center, radius, Surface.Rough, Surface.Trees);
        if (!TrySpend(LedgerCategory.Trees, cost + PerCell(terrain, cells, Economy.TreeClearPerM2)))
        {
            return false;
        }

        if (doomed.Count > 0)
        {
            Trees.RemoveWhere(t => doomed.Contains(t.Id));
        }

        var cleared = terrain.PaintDisc(center, radius, Surface.Rough, Surface.Trees, out _);
        if (cleared is { } rect)
        {
            // Une surface a changé : les champs de flux se recalculent comme après une peinture.
            TerrainEdited?.Invoke(rect);
        }

        if (doomed.Count == 0 && cleared is null)
        {
            return false;
        }

        NotifyTrees(terrain, center.X - radius, center.Z - radius, center.X + radius, center.Z + radius);
        return true;
    }

    private bool ApplyPlant(TerrainGrid terrain, TreeCommand c)
    {
        var width = terrain.CellsX * terrain.CellSize;
        var depth = terrain.CellsZ * terrain.CellSize;
        if (c.X < 0 || c.Z < 0 || c.X >= width || c.Z >= depth)
        {
            return false;
        }

        if (terrain.SurfaceAt(c.X, c.Z) is Surface.Water or Surface.Green or Surface.Tee or Surface.Bunker or Surface.Trees or Surface.Path)
        {
            return false;
        }

        var species = TreeSpeciesTable.Get(c.Species);
        var age = c.Stage == TreePlantStage.Mound ? TreePlanting.MoundAgeMonths(Trees.NextId) : 0;
        var crown = Tree.CrownDiameter(species, age) * 0.5;
        var near = new List<Tree>();
        // Rayon de recherche : un tronc couché s'étend jusqu'à la hauteur de l'arbre depuis son pied.
        Trees.Query(c.X, c.Z, Math.Max(crown + TreeSpeciesTable.MaxCrownRadius, TreeSpeciesTable.MaxHeight + 5.0), near);
        foreach (var other in near)
        {
            if (other.State == TreeState.Stump && Dist2(other.X, other.Z, c.X, c.Z) < StumpClearance * StumpClearance)
            {
                return false;
            }

            if (other.State == TreeState.Fallen && SegmentDistance(c.X, c.Z, other.X, other.Z, other.X + (Math.Cos(other.FallYaw) * other.Height()), other.Z + (Math.Sin(other.FallYaw) * other.Height())) < LogClearance)
            {
                return false;
            }

            if (other.State != TreeState.Standing)
            {
                continue;
            }

            var min = WoodlandGenerator.MinSpacingFactor * (crown + other.CrownRadius());
            var dx = other.X - c.X;
            var dz = other.Z - c.Z;
            if ((dx * dx) + (dz * dz) < min * min)
            {
                return false;
            }
        }

        if (!TrySpend(LedgerCategory.Trees, Economy.PlantCost(c.Stage)))
        {
            return false;
        }

        Trees.Add(c.X, c.Z, c.Species, age);
        NotifyTrees(terrain, c.X, c.Z, c.X, c.Z);
        return true;
    }

    /// <summary>Distance minimale d'une plantation à une souche, en mètres.</summary>
    private const double StumpClearance = 1.5;

    /// <summary>Distance minimale d'une plantation au segment pied-cime d'un tronc couché, en mètres.</summary>
    private const double LogClearance = 1.0;

    private static double Dist2(double x0, double z0, double x1, double z1) => ((x1 - x0) * (x1 - x0)) + ((z1 - z0) * (z1 - z0));

    private static double SegmentDistance(double px, double pz, double ax, double az, double bx, double bz)
    {
        var len2 = Dist2(ax, az, bx, bz);
        var t = len2 <= 0 ? 0.0 : Math.Clamp((((px - ax) * (bx - ax)) + ((pz - az) * (bz - az))) / len2, 0.0, 1.0);
        return Math.Sqrt(Dist2(px, pz, ax + (t * (bx - ax)), az + (t * (bz - az))));
    }

    private bool ApplyFell(TerrainGrid terrain, int id, double direction)
    {
        if (!Trees.TryGet(id, out var tree) || tree.State != TreeState.Standing)
        {
            return false;
        }

        Trees.SetState(id, TreeState.Fallen, (float)direction);
        Trees.TryGet(id, out var fallen);
        var length = fallen.Height();
        NotifyTrees(terrain, tree.X, tree.Z, tree.X + (Math.Cos(direction) * length), tree.Z + (Math.Sin(direction) * length));
        TreeFelled?.Invoke(fallen);
        return true;
    }

    private bool ApplyClearFallen(TerrainGrid terrain, int id)
    {
        if (!Trees.TryGet(id, out var tree) || tree.State != TreeState.Fallen)
        {
            return false;
        }

        if (!TrySpend(LedgerCategory.Trees, Economy.TreeClearFallen))
        {
            return false;
        }

        Trees.SetState(id, TreeState.Stump, tree.FallYaw);
        NotifyTrees(terrain, tree.X, tree.Z, tree.X + (Math.Cos(tree.FallYaw) * tree.Height()), tree.Z + (Math.Sin(tree.FallYaw) * tree.Height()));
        return true;
    }

    private void NotifyTrees(TerrainGrid terrain, double x0, double z0, double x1, double z1)
    {
        var rect = new CellRect(
            Math.Clamp(terrain.CellXOf(Math.Min(x0, x1)), 0, terrain.CellsX - 1),
            Math.Clamp(terrain.CellZOf(Math.Min(z0, z1)), 0, terrain.CellsZ - 1),
            Math.Clamp(terrain.CellXOf(Math.Max(x0, x1)), 0, terrain.CellsX - 1),
            Math.Clamp(terrain.CellZOf(Math.Max(z0, z1)), 0, terrain.CellsZ - 1));
        TreesEdited?.Invoke(rect);
    }

    private void MixTreeCommands(ref ulong hash)
    {
        MixEconomy(ref hash);
        Mix(ref hash, (ulong)TreeCommandsRefused);
        Mix(ref hash, (ulong)_treeCommands.Count);
        foreach (var c in _treeCommands)
        {
            Mix(ref hash, ((ulong)c.Kind << 40) ^ ((ulong)c.Species << 32) ^ ((ulong)c.Stage << 48) ^ (uint)c.Id ^ (c.Flag ? 1UL << 56 : 0UL));
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(c.X));
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(c.Z));
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(c.Radius));
        }
    }

    private enum TreeCommandKind : byte
    {
        RazeTree,
        RazeArea,
        Plant,
        Fell,
        ClearFallen,
    }

    /// <summary>Commande d'arbre en file ; <c>Radius</c> sert aussi de direction de chute pour <see cref="TreeCommandKind.Fell"/>.</summary>
    private readonly record struct TreeCommand(TreeCommandKind Kind, int Id, double X, double Z, double Radius, bool Flag, TreeSpeciesId Species, TreePlantStage Stage);
}
