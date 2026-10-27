namespace Golferoo.Sim.Terrain;

/// <summary>
/// Fleurs de printemps dans le rough : une touffe sur <see cref="CellsPerTuft"/> cellules de rough, choisies par hachage de la
/// cellule (stable d'une partie à l'autre). La sim s'en sert pour la beauté (<see cref="BeautyMap"/>), le rendu pour les poser.
/// </summary>
public static class Flowers
{
    /// <summary>Une touffe pour tant de cellules de rough, en moyenne.</summary>
    public const int CellsPerTuft = 3;

    /// <summary>Hachage de la cellule : ses bits règlent aussi la variante, la place, la taille et la couleur de la touffe.</summary>
    public static uint Hash(int x, int z)
    {
        unchecked
        {
            var h = ((uint)x * 2654435761u) ^ ((uint)z * 2246822519u);
            h ^= h >> 15;
            h *= 0x2c1b3c6du;
            h ^= h >> 12;
            return h;
        }
    }

    /// <summary>La cellule porte une touffe de fleurs au printemps.</summary>
    public static bool HasTuft(TerrainGrid grid, int cx, int cz) =>
        grid.GetCellSurface(cx, cz) == Surface.Rough && Hash(cx, cz) % CellsPerTuft == 0;
}
