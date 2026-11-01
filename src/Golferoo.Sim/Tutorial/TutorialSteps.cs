using Golferoo.Sim.Buildings;
using Golferoo.Sim.Economy;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Tutorial;

/// <summary>
/// Les étapes du tutoriel, dans l'ordre : C07 jusqu'au trou valide, C19 ensuite (le club vit, première paire, premier green fee, une pensée lue, le snack-bar offert).
/// L'accueil du pro est du rendu : il précède <see cref="ClearTrees"/>.
/// </summary>
public enum TutorialStep
{
    ClearTrees,
    PlantSapling,
    PaintTee,
    PaintFairway,
    PaintGreen,
    PlaceTeeAndHole,

    /// <summary>Le trou valide s'ouvre, le club est activé : aucun visiteur n'est encore arrivé.</summary>
    OpenHole,

    /// <summary>Un groupe est arrivé et n'a pas encore joué son premier coup.</summary>
    WatchPair,

    /// <summary>La paire a joué son premier coup ; aucun green fee n'a encore été encaissé.</summary>
    FirstGreenFee,

    /// <summary>Un green fee est tombé ; le joueur n'a pas encore lu la pensée d'un golfeur.</summary>
    ReadThought,

    /// <summary>Le bon de pose du snack-bar attend d'être employé.</summary>
    PlaceSnackBar,
    Done,
}

/// <summary>
/// Étape courante du tutoriel, déduite de l'état du monde : la première dont la condition n'est pas remplie, donc défaire
/// un green ramène à l'étape du green. Aucun état propre (seuls <see cref="Objectives.GameState.ClubActive"/> et <see cref="Objectives.GameState.ThoughtRead"/>, sauvés, y servent).
/// </summary>
public static class TutorialSteps
{
    public static TutorialStep Current(World world)
    {
        var terrain = world.Terrain ?? throw new InvalidOperationException("Le tutoriel exige un terrain.");
        return world.HoleOpen ? Club(world) : Build(world, terrain);
    }

    /// <summary>La suite de C19 : chaque étape se déclenche par son évènement et reste passée ensuite.</summary>
    private static TutorialStep Club(World world)
    {
        if (world.Visitors.Next == 0)
        {
            return TutorialStep.OpenHole;
        }

        if (!world.Ledger.HasEarned(LedgerCategory.GreenFees))
        {
            return world.Groups.Any(g => g.FromArrivals && g.Golfers.Any(o => o.Strokes > 0)) ? TutorialStep.FirstGreenFee : TutorialStep.WatchPair;
        }

        if (!world.State.ThoughtRead)
        {
            return TutorialStep.ReadThought;
        }

        return world.State.Vouchers(BuildingKind.SnackBar) > 0 ? TutorialStep.PlaceSnackBar : TutorialStep.Done;
    }

    private static TutorialStep Build(World world, TerrainGrid terrain)
    {
        if (CorridorTreesLeft(world, terrain) > 0)
        {
            return TutorialStep.ClearTrees;
        }

        if (world.TreesPlanted == 0)
        {
            return TutorialStep.PlantSapling;
        }

        if (HoleCheck.Area(terrain, Surface.Tee) < HoleCheck.MinTeeArea)
        {
            return TutorialStep.PaintTee;
        }

        if (HoleCheck.Area(terrain, Surface.Fairway) < HoleCheck.MinFairwayArea)
        {
            return TutorialStep.PaintFairway;
        }

        return HoleCheck.Area(terrain, Surface.Green) < HoleCheck.MinGreenArea ? TutorialStep.PaintGreen : TutorialStep.PlaceTeeAndHole;
    }

    /// <summary>Arbres du fichier du trou (ceux du passage) encore debout à leur place.</summary>
    public static int CorridorTreesLeft(World world, TerrainGrid terrain)
    {
        var left = 0;
        foreach (var p in terrain.IsolatedTrees)
        {
            foreach (var t in world.Trees.Trees)
            {
                if (t.X == p.X && t.Z == p.Z)
                {
                    left++;
                    break;
                }
            }
        }

        return left;
    }
}
