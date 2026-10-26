using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Tutorial;

/// <summary>Les étapes du tutoriel de C07, dans l'ordre. L'accueil du pro est du rendu : il précède <see cref="ClearTrees"/>.</summary>
public enum TutorialStep
{
    ClearTrees,
    PlantSapling,
    PaintTee,
    PaintFairway,
    PaintGreen,
    PlaceTeeAndHole,
    Done,
}

/// <summary>
/// Étape courante du tutoriel, déduite de l'état du monde : la première dont la condition n'est pas remplie, donc défaire
/// un green ramène à l'étape du green. Aucun état propre. Le tutoriel est fini dès que le trou est valide (<see cref="World.HoleOpen"/>).
/// </summary>
public static class TutorialSteps
{
    public static TutorialStep Current(World world)
    {
        if (world.HoleOpen)
        {
            return TutorialStep.Done;
        }

        var terrain = world.Terrain ?? throw new InvalidOperationException("Le tutoriel exige un terrain.");
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
