namespace Golferoo.Sim.Trees;

/// <summary>Essence d'arbre. Les valeurs numériques servent d'index stable : ne pas réordonner.</summary>
public enum TreeSpeciesId : byte
{
    Deciduous = 0,
    Pine = 1,
}

/// <summary>Famille de l'essence (la couronne se comporte autrement selon les saisons, lot 2).</summary>
public enum TreeKind : byte
{
    Broadleaf,
    Conifer,
}

/// <summary>
/// Essence : dimensions d'un adulte, d'un jeune plant, et âge adulte. Hauteur et couronne d'un arbre se déduisent
/// de l'essence et de l'âge seulement (<see cref="Tree.Height"/>), tableau « Références » de specs/C02-arbres-echelle.md.
/// </summary>
/// <param name="AdultHeight">Hauteur adulte, en mètres.</param>
/// <param name="AdultCrownDiameter">Diamètre de la couronne adulte (à la base pour un conifère), en mètres.</param>
/// <param name="TrunkDiameter">Diamètre du tronc adulte, en mètres.</param>
/// <param name="FirstBranchHeight">Hauteur des premières branches d'un adulte, en mètres.</param>
/// <param name="AdultAgeMonths">Âge à partir duquel l'arbre a ses dimensions adultes, en mois.</param>
/// <param name="SaplingHeight">Hauteur à la plantation, en mètres.</param>
/// <param name="SaplingCrownDiameter">Diamètre de couronne à la plantation, en mètres.</param>
/// <param name="SaplingTrunkDiameter">Diamètre du tronc à la plantation, en mètres.</param>
/// <param name="CrownDensities">Densité de la couronne par saison (indexée par <see cref="Season"/>) : 1 est une couronne pleine d'été.</param>
public sealed record TreeSpecies(
    TreeSpeciesId Id,
    string Name,
    TreeKind Kind,
    double AdultHeight,
    double AdultCrownDiameter,
    double TrunkDiameter,
    double FirstBranchHeight,
    int AdultAgeMonths,
    double SaplingHeight,
    double SaplingCrownDiameter,
    double SaplingTrunkDiameter,
    double[] CrownDensities)
{
    /// <summary>Densité de la couronne à la saison donnée ; la balle y est arrêtée avec la probabilité 1 − exp(−k · densité · longueur).</summary>
    public double CrownDensity(Season season) => CrownDensities[(int)season];
}

/// <summary>Table des essences du thème Parkland, comme <see cref="Terrain.SurfaceTable"/>.</summary>
public static class TreeSpeciesTable
{
    private static readonly TreeSpecies[] Table =
    [
        // Feuillu (chêne, érable) : 12 à 20 m, couronne 8 à 14 m, tronc 0,4 à 0,8 m, adulte vers 40 ans.
        new(TreeSpeciesId.Deciduous, "Feuillu", TreeKind.Broadleaf, AdultHeight: 16.0, AdultCrownDiameter: 11.0, TrunkDiameter: 0.6, FirstBranchHeight: 3.0, AdultAgeMonths: 40 * 12, SaplingHeight: 2.0, SaplingCrownDiameter: 1.0, SaplingTrunkDiameter: 0.05, CrownDensities: [0.4, 1.0, 0.6, 0.15]),
        // Pin : 15 à 25 m, couronne 4 à 8 m à la base, tronc 0,3 à 0,6 m, adulte vers 50 ans.
        new(TreeSpeciesId.Pine, "Pin", TreeKind.Conifer, AdultHeight: 20.0, AdultCrownDiameter: 6.0, TrunkDiameter: 0.45, FirstBranchHeight: 4.0, AdultAgeMonths: 50 * 12, SaplingHeight: 2.0, SaplingCrownDiameter: 1.0, SaplingTrunkDiameter: 0.05, CrownDensities: [0.95, 1.0, 0.95, 0.9]),
    ];

    /// <summary>Plus grand rayon de couronne d'un adulte, toutes essences, en mètres.</summary>
    public static double MaxCrownRadius { get; } = Table.Max(t => t.AdultCrownDiameter) * 0.5;

    public static int Count => Table.Length;

    public static TreeSpecies Get(TreeSpeciesId id)
    {
        var index = (int)id;
        if (index < 0 || index >= Table.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "Essence inconnue.");
        }

        return Table[index];
    }

    /// <summary>Essence d'après son nom, sans tenir compte de la casse ; null si inconnue.</summary>
    public static TreeSpecies? FindByName(string name)
    {
        foreach (var s in Table)
        {
            if (string.Equals(s.Id.ToString(), name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return s;
            }
        }

        return null;
    }
}
