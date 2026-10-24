using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Shots;

/// <summary>Un coup à résoudre.</summary>
/// <param name="Ball">Position de la balle, en mètres.</param>
/// <param name="Hole">Position du trou, en mètres.</param>
/// <param name="StrokeNumber">Nombre de coups déjà joués par ce golfeur sur le trou (0 pour le drive).</param>
public readonly record struct ShotRequest(Point2 Ball, Point2 Hole, int StrokeNumber);

/// <summary>Résultat d'un coup.</summary>
/// <param name="Ball">Nouvelle position de la balle, en mètres (franchissable à pied).</param>
/// <param name="Holed">Vrai si la balle est dans le trou.</param>
public readonly record struct ShotResult(Point2 Ball, bool Holed);

/// <summary>
/// Résout un coup. Le lot 4 y branche le vrai vol et roulement ; la machine à états des golfeurs n'en dépend que par cette interface.
/// Toute l'aléa vient du RNG passé en argument.
/// </summary>
public interface IShotResolver
{
    ShotResult Resolve(in ShotRequest request, TerrainGrid terrain, DeterministicRng rng);
}
