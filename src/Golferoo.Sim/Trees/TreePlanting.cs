namespace Golferoo.Sim.Trees;

/// <summary>Stade d'un arbre planté.</summary>
public enum TreePlantStage : byte
{
    /// <summary>Jeune plant, tout juste planté (âge 0).</summary>
    Sapling = 0,

    /// <summary>Motte : arbre déjà de 8 à 10 ans, plus cher.</summary>
    Mound = 1,
}

/// <summary>Règles de plantation. Les prix des travaux sur les arbres sont des données (<see cref="Economy.EconomyParams"/>).</summary>
public static class TreePlanting
{
    /// <summary>Âge d'une motte, en mois, de 8 à 10 ans selon l'identifiant de l'arbre planté.</summary>
    public static int MoundAgeMonths(int salt) => 96 + (int)((uint)salt % 25u);
}
