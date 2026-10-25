using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Trees;

/// <summary>Arbre isolé listé par le fichier du trou (position en mètres, âge en mois).</summary>
public readonly record struct TreePlacement(double X, double Z, TreeSpeciesId Species, int AgeMonths);

/// <summary>
/// Boisement déterministe : des arbres d'âges et d'essences variés dans les cellules de surface <see cref="Surface.Trees"/>,
/// séparés d'au moins <see cref="SpacingFactor"/> fois la somme des rayons de leurs couronnes (tirage de Poisson-disk
/// par jets successifs sur une grille jitterée). Même graine, même forêt.
/// </summary>
public static class WoodlandGenerator
{
    /// <summary>Distance minimale entre deux arbres, en part de la somme des rayons de leurs couronnes (plancher de la spec C02, garanti aussi par les arbres isolés posés à la main hors boisement).</summary>
    public const double MinSpacingFactor = 0.7;

    /// <summary>Espacement visé dans un massif : à peu près le diamètre de la couronne, soit la somme des rayons (environ 100 arbres à l'hectare).</summary>
    public const double SpacingFactor = 1.0;

    /// <summary>Pas de la grille de jets, en mètres.</summary>
    private const double Pitch = 3.0;

    /// <summary>Plus grand rayon de couronne possible : borne de la recherche des voisins.</summary>
    private const double MaxCrownRadius = 7.0;

    /// <summary>Bornes cumulées du tirage de l'essence : 40 % de chêne, puis 20 % de bouleau, le reste (40 %) de sapin.</summary>
    private const double OakShare = 0.4;

    private const double BirchUpTo = 0.6;

    /// <summary>Plante des arbres dans les cellules de <paramref name="rect"/> dont la surface est Trees ; avec <paramref name="center"/>, seulement à moins de <paramref name="radius"/> mètres de ce centre (le rectangle englobe le disque peint).</summary>
    public static int Fill(TreeStand stand, TerrainGrid terrain, CellRect rect, DeterministicRng rng, Point2? center = null, double radius = double.PositiveInfinity)
    {
        var x0 = rect.MinX * terrain.CellSize;
        var z0 = rect.MinZ * terrain.CellSize;
        var x1 = (rect.MaxX + 1) * terrain.CellSize;
        var z1 = (rect.MaxZ + 1) * terrain.CellSize;
        var nx = Math.Max(1, (int)Math.Ceiling((x1 - x0) / Pitch));
        var nz = Math.Max(1, (int)Math.Ceiling((z1 - z0) / Pitch));

        var order = new int[nx * nz];
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }

        for (var i = order.Length - 1; i > 0; i--)
        {
            var j = (int)(rng.NextUInt64() % (ulong)(i + 1));
            (order[i], order[j]) = (order[j], order[i]);
        }

        var near = new List<Tree>();
        var added = 0;
        foreach (var k in order)
        {
            var x = x0 + (((k % nx) + rng.NextDouble()) * Pitch);
            var z = z0 + (((k / nx) + rng.NextDouble()) * Pitch);
            var pick = rng.NextDouble();
            var species = pick < OakShare ? TreeSpeciesId.Oak : pick < BirchUpTo ? TreeSpeciesId.Birch : TreeSpeciesId.Fir;
            var spec = TreeSpeciesTable.Get(species);
            var age = (int)(spec.AdultAgeMonths * (0.5 + (rng.NextDouble() * 1.1)));
            if (x >= x1 || z >= z1 || terrain.SurfaceAt(x, z) != Surface.Trees ||
                !rect.Contains(terrain.CellXOf(x), terrain.CellZOf(z)))
            {
                continue;
            }

            if (center is { } c && (((x - c.X) * (x - c.X)) + ((z - c.Z) * (z - c.Z))) > radius * radius)
            {
                continue;
            }

            var crown = Tree.CrownDiameter(spec, age) * 0.5;
            near.Clear();
            stand.Query(x, z, crown + MaxCrownRadius, near);
            var free = true;
            foreach (var other in near)
            {
                var min = SpacingFactor * (crown + other.CrownRadius());
                var dx = other.X - x;
                var dz = other.Z - z;
                if ((dx * dx) + (dz * dz) < min * min)
                {
                    free = false;
                    break;
                }
            }

            if (free)
            {
                stand.Add(x, z, species, age);
                added++;
            }
        }

        return added;
    }
}
