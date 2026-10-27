namespace Golferoo.Sim.Trees;

/// <summary>État d'un arbre.</summary>
public enum TreeState : byte
{
    Standing = 0,
    Fallen = 1,
    Stump = 2,
}

/// <summary>
/// Arbre de la simulation. Hauteur et couronne ne sont pas stockées : elles se déduisent de l'essence et de l'âge
/// par une fonction pure (<see cref="Height"/>, <see cref="CrownDiameter"/>). <paramref name="FallYaw"/> : direction de chute
/// en radians dans le plan (cos = X, sin = Z), significative seulement pour un arbre <see cref="TreeState.Fallen"/>.
/// </summary>
public readonly record struct Tree(int Id, double X, double Z, TreeSpeciesId Species, int AgeMonths, TreeState State = TreeState.Standing, bool Remarkable = false, float FallYaw = 0f)
{
    /// <summary>Un arbre est remarquable à partir de ce multiple de l'âge adulte de son essence.</summary>
    public const double RemarkableAgeFactor = 1.5;

    /// <summary>Sous cette hauteur, un arbre est jeune : il pousse plus vite après une pluie de printemps (M9), la tempête l'épargne.</summary>
    public const double YoungMaxHeight = 4.0 * TreeSpeciesTable.SizeScale;

    /// <summary>Vrai si un arbre de cette essence et de cet âge est remarquable (âge d'au moins 1,5 fois l'âge adulte).</summary>
    public static bool IsRemarkableAge(TreeSpecies species, int ageMonths) => ageMonths >= RemarkableAgeFactor * species.AdultAgeMonths;

    /// <summary>Avancement de la croissance, de 0 (plant) à 1 (adulte) ; courbe rapide au début, qui s'aplatit.</summary>
    public static double Growth(TreeSpecies species, int ageMonths)
    {
        var t = Math.Clamp((double)ageMonths / species.AdultAgeMonths, 0.0, 1.0);
        return 1.0 - ((1.0 - t) * (1.0 - t));
    }

    /// <summary>Hauteur en mètres d'un arbre de cette essence et de cet âge : du plant à l'adulte, jamais au-delà.</summary>
    public static double Height(TreeSpecies species, int ageMonths) =>
        species.SaplingHeight + ((species.AdultHeight - species.SaplingHeight) * Growth(species, ageMonths));

    /// <summary>Diamètre de la couronne en mètres, même courbe que la hauteur.</summary>
    public static double CrownDiameter(TreeSpecies species, int ageMonths) =>
        species.SaplingCrownDiameter + ((species.AdultCrownDiameter - species.SaplingCrownDiameter) * Growth(species, ageMonths));

    /// <summary>Rayon du tronc en mètres, du plant à l'adulte.</summary>
    public static double TrunkRadius(TreeSpecies species, int ageMonths) =>
        0.5 * (species.SaplingTrunkDiameter + ((species.TrunkDiameter - species.SaplingTrunkDiameter) * Growth(species, ageMonths)));

    /// <summary>Hauteur de la base de la couronne en mètres : les premières branches de l'adulte, à l'échelle de la hauteur de l'arbre.</summary>
    public static double CrownBase(TreeSpecies species, int ageMonths) =>
        species.FirstBranchHeight * Height(species, ageMonths) / species.AdultHeight;

    public double Height() => Height(TreeSpeciesTable.Get(Species), AgeMonths);

    public double CrownDiameter() => CrownDiameter(TreeSpeciesTable.Get(Species), AgeMonths);

    public double CrownRadius() => CrownDiameter() * 0.5;
}
