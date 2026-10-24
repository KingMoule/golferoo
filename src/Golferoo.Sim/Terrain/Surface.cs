namespace Golferoo.Sim.Terrain;

/// <summary>Surface discrète d'une cellule. Les valeurs numériques servent d'index stable : ne pas réordonner.</summary>
public enum Surface : byte
{
    Tee = 0,
    Green = 1,
    Fairway = 2,
    Rough = 3,
    Bunker = 4,
    Water = 5,
    Trees = 6,
    Path = 7,
}

/// <summary>
/// Propriétés physiques et de règles d'une surface.
/// </summary>
/// <param name="Walkable">Un golfeur peut y marcher.</param>
/// <param name="IsHazard">Obstacle au sens des règles (eau, bunker).</param>
/// <param name="PenaltyStrokes">Coups de pénalité quand la balle s'y arrête.</param>
/// <param name="RollResistance">Décélération de roulement, sans unité (fraction de g) : plus c'est grand, plus la balle ralentit.</param>
/// <param name="Restitution">Part de la vitesse normale conservée au rebond (0 = mou, 1 = parfait).</param>
/// <param name="Friction">Part de la vitesse tangentielle perdue au rebond (0 = glisse, 1 = accroche).</param>
public readonly record struct SurfaceProperties(
    bool Walkable,
    bool IsHazard,
    int PenaltyStrokes,
    double RollResistance,
    double Restitution,
    double Friction);

/// <summary>Table des propriétés par surface. Valeurs plausibles, sans réglage fin (à calibrer au lot 4).</summary>
public static class SurfaceTable
{
    private static readonly SurfaceProperties[] Table =
    [
        // Tee : herbe tondue ras, dur.
        new(Walkable: true, IsHazard: false, PenaltyStrokes: 0, RollResistance: 0.06, Restitution: 0.50, Friction: 0.25),
        // Green : le plus rapide au roulement, rebond doux.
        new(Walkable: true, IsHazard: false, PenaltyStrokes: 0, RollResistance: 0.05, Restitution: 0.40, Friction: 0.30),
        // Fairway : herbe courte.
        new(Walkable: true, IsHazard: false, PenaltyStrokes: 0, RollResistance: 0.10, Restitution: 0.45, Friction: 0.35),
        // Rough : herbe haute, freine fort et amortit.
        new(Walkable: true, IsHazard: false, PenaltyStrokes: 0, RollResistance: 0.40, Restitution: 0.25, Friction: 0.60),
        // Bunker : sable, la balle s'y plante (obstacle sans pénalité de coup).
        new(Walkable: true, IsHazard: true, PenaltyStrokes: 0, RollResistance: 1.00, Restitution: 0.05, Friction: 0.90),
        // Water : la balle est perdue, un coup de pénalité.
        new(Walkable: false, IsHazard: true, PenaltyStrokes: 1, RollResistance: 1.00, Restitution: 0.00, Friction: 1.00),
        // Trees : bosquet, infranchissable à pied, la balle rebondit mal.
        new(Walkable: false, IsHazard: false, PenaltyStrokes: 0, RollResistance: 0.60, Restitution: 0.20, Friction: 0.70),
        // Path : chemin dur de golfeurettes, roule vite, rebond vif.
        new(Walkable: true, IsHazard: false, PenaltyStrokes: 0, RollResistance: 0.08, Restitution: 0.55, Friction: 0.20),
    ];

    /// <summary>Nombre de surfaces définies.</summary>
    public static int Count => Table.Length;

    public static SurfaceProperties Get(Surface surface)
    {
        var index = (int)surface;
        if (index < 0 || index >= Table.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(surface), surface, "Surface inconnue.");
        }

        return Table[index];
    }
}
