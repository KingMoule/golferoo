namespace Golferoo.Sim.Trees;

/// <summary>Stade d'un arbre planté.</summary>
public enum TreePlantStage : byte
{
    /// <summary>Jeune plant, tout juste planté (âge 0).</summary>
    Sapling = 0,

    /// <summary>Motte : arbre déjà de 8 à 10 ans, plus cher.</summary>
    Mound = 1,
}

/// <summary>
/// Coûts des travaux sur les arbres. Constantes seules : l'économie n'existe pas encore (pas de caisse), <see cref="World.TreeSpending"/>
/// cumule les dépenses.
/// </summary>
public static class TreeCosts
{
    public const int Sapling = 50;

    public const int Mound = 800;

    /// <summary>Abattage d'un arbre debout.</summary>
    public const int Raze = 200;

    /// <summary>Multiplicateur de l'abattage d'un arbre remarquable.</summary>
    public const int RemarkableMultiplier = 2;

    /// <summary>Défrichage, par mètre carré de sous-bois (surface Trees) converti en Rough.</summary>
    public const int ClearPerSquareMeter = 5;

    /// <summary>Dégagement d'un tronc tombé (il devient une souche).</summary>
    public const int ClearFallen = 150;

    /// <summary>Âge d'une motte, en mois, de 8 à 10 ans selon l'identifiant de l'arbre planté.</summary>
    public static int MoundAgeMonths(int salt) => 96 + (int)((uint)salt % 25u);

    public static int PlantCost(TreePlantStage stage) => stage == TreePlantStage.Mound ? Mound : Sapling;

    public static int RazeCost(Tree tree) => Raze * (tree.Remarkable ? RemarkableMultiplier : 1);
}
