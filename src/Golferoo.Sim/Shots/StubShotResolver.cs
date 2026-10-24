using Golferoo.Sim.Pathing;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim.Shots;

/// <summary>
/// Coup provisoire et déterministe : avance vers le trou d'une distance fixe par club avec une petite dispersion.
/// Sur le green, la balle va au trou en un ou deux putts. Une balle tombée dans l'eau ou les arbres est reposée
/// sur la cellule franchissable la plus proche (pas de pénalité).
/// </summary>
public sealed class StubShotResolver : IShotResolver
{
    public const double DriveRange = 220.0;
    public const double ApproachRange = 140.0;

    /// <summary>Dispersion latérale maximale, en fraction de la portée.</summary>
    private const double Spread = 0.06;

    /// <summary>Rayon d'atterrissage autour du trou pour un coup qui atteint le trou.</summary>
    private const double GreenScatter = 10.0;

    private const double MakeDistance = 1.0;
    private const double FirstPuttHoleChance = 0.4;
    private const double LagDistance = 0.5;

    public ShotResult Resolve(in ShotRequest request, TerrainGrid terrain, DeterministicRng rng)
    {
        var ex = request.Hole.X - request.Ball.X;
        var ez = request.Hole.Z - request.Ball.Z;
        var dist = Math.Sqrt((ex * ex) + (ez * ez));

        if (terrain.SurfaceAt(request.Ball.X, request.Ball.Z) == Surface.Green)
        {
            if (dist <= MakeDistance || rng.NextDouble() < FirstPuttHoleChance)
            {
                return new ShotResult(request.Hole, true);
            }

            // Putt trop court ou trop long : la balle s'arrête près du trou, le suivant est tombé.
            var angle = rng.NextDouble() * 2 * Math.PI;
            var lag = new Point2(request.Hole.X + (Math.Cos(angle) * LagDistance), request.Hole.Z + (Math.Sin(angle) * LagDistance));
            return new ShotResult(GridQueries.NearestWalkablePoint(terrain, lag), false);
        }

        var range = request.StrokeNumber == 0 ? DriveRange : ApproachRange;
        Point2 landing;
        if (dist <= range)
        {
            var angle = rng.NextDouble() * 2 * Math.PI;
            var radius = Math.Sqrt(rng.NextDouble()) * GreenScatter;
            landing = new Point2(request.Hole.X + (Math.Cos(angle) * radius), request.Hole.Z + (Math.Sin(angle) * radius));
        }
        else
        {
            var carry = range * (0.9 + (0.1 * rng.NextDouble()));
            var side = (rng.NextDouble() - 0.5) * 2 * Spread * range;
            var ux = ex / dist;
            var uz = ez / dist;
            landing = new Point2(
                request.Ball.X + (ux * carry) - (uz * side),
                request.Ball.Z + (uz * carry) + (ux * side));
        }

        return new ShotResult(GridQueries.NearestWalkablePoint(terrain, landing), false);
    }
}
