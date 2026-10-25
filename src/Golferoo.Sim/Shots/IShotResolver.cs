using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Shots;

/// <summary>Un coup à résoudre.</summary>
/// <param name="Ball">Position de la balle, en mètres.</param>
/// <param name="Hole">Position du trou, en mètres.</param>
/// <param name="StrokeNumber">Nombre de coups déjà joués par ce golfeur sur le trou (0 pour le drive).</param>
/// <param name="Skill">Niveau de jeu du golfeur (1 = référence) : règle la dispersion et la longueur à l'exécution, pas la planification.</param>
public readonly record struct ShotRequest(Point2 Ball, Point2 Hole, int StrokeNumber, double Skill = 1.0);

/// <summary>Issue d'un coup : où la balle s'est arrêtée, ou ce qui lui est arrivé.</summary>
public enum ShotOutcome : byte
{
    Fairway = 0,
    Tee = 1,
    Green = 2,
    Rough = 3,
    Bunker = 4,
    Path = 5,

    /// <summary>La balle a heurté un bosquet et est tombée à son pied.</summary>
    Trees = 6,

    /// <summary>Balle à l'eau : un coup de pénalité et un drop côté tee.</summary>
    Water = 7,

    /// <summary>Balle hors carte : un coup de pénalité, on rejoue du point précédent.</summary>
    OutOfBounds = 8,

    Holed = 9,
}

/// <summary>Résultat d'un coup.</summary>
/// <param name="Ball">Nouvelle position de la balle, en mètres (franchissable à pied), après drop éventuel.</param>
/// <param name="Holed">Vrai si la balle est dans le trou.</param>
/// <param name="PenaltyStrokes">Coups de pénalité à ajouter au coup joué.</param>
/// <param name="Outcome">Issue du coup.</param>
/// <param name="Trajectory">
/// Positions 3D de la balle, une par tick de simulation (1/20 s), la première au point de départ et la dernière au repos.
/// Nul ou d'un seul point : la balle arrive aussitôt.
/// </param>
public readonly record struct ShotResult(
    Point2 Ball,
    bool Holed,
    int PenaltyStrokes = 0,
    ShotOutcome Outcome = ShotOutcome.Fairway,
    IReadOnlyList<Vec3>? Trajectory = null);

/// <summary>
/// Résout un coup. Le résolveur physique (lot 4) simule le vol et le roulement ; la machine à états des golfeurs n'en dépend que par cette interface.
/// Toute l'aléa vient du RNG passé en argument.
/// </summary>
public interface IShotResolver
{
    ShotResult Resolve(in ShotRequest request, TerrainGrid terrain, DeterministicRng rng);
}
