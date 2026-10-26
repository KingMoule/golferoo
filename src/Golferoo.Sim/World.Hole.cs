using Golferoo.Sim.Terrain;

namespace Golferoo.Sim;

/// <summary>
/// Pose du départ et du trou (C07), en file comme <see cref="PaintSurface"/> : appliquée au début du tick suivant,
/// comptée dans <see cref="Checksum"/> (par le terrain pour l'état, par la file pour les commandes en attente).
/// </summary>
public sealed partial class World
{
    private readonly Queue<(bool IsTee, Point2 Point)> _holeCommands = new();
    private bool _holeOpen = true;
    private long _holeCheckedVersion = -1;

    /// <summary>
    /// Vrai si des golfeurs peuvent jouer le trou : toujours, sauf sur une terre vierge tant que <see cref="HoleCheck"/> y trouve un manque.
    /// Se recalcule au début d'un tick, après une retouche ou une pose.
    /// </summary>
    public bool HoleOpen => _holeOpen;

    /// <summary>Met en file la pose du départ en <paramref name="point"/> ; refusée (faux) si ce n'est pas sur une cellule de départ.</summary>
    public bool PlaceTee(Point2 point) => QueueHolePoint(true, point, Surface.Tee);

    /// <summary>Met en file la pose du trou en <paramref name="point"/> ; refusée (faux) si ce n'est pas sur le green.</summary>
    public bool PlaceHole(Point2 point) => QueueHolePoint(false, point, Surface.Green);

    private bool QueueHolePoint(bool isTee, Point2 point, Surface required)
    {
        var terrain = RequireTerrain();
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Z) || terrain.SurfaceAt(point.X, point.Z) != required)
        {
            return false;
        }

        _holeCommands.Enqueue((isTee, point));
        return true;
    }

    private void ApplyHoleCommands()
    {
        if (Terrain is not { } terrain)
        {
            return;
        }

        while (_holeCommands.Count > 0)
        {
            var (isTee, point) = _holeCommands.Dequeue();
            if (isTee && terrain.SurfaceAt(point.X, point.Z) == Surface.Tee)
            {
                terrain.MoveTee(point);
                _holeCheckedVersion = -1;
            }
            else if (!isTee && terrain.SurfaceAt(point.X, point.Z) == Surface.Green)
            {
                terrain.MovePin(point);
                _holeCheckedVersion = -1;
            }
        }

        if (terrain.Blank && _holeCheckedVersion != terrain.Version)
        {
            _holeOpen = HoleCheck.Check(terrain).Count == 0;
            _holeCheckedVersion = terrain.Version;
        }
    }

    private void MixHoleCommands(ref ulong hash)
    {
        foreach (var (isTee, point) in _holeCommands)
        {
            Mix(ref hash, isTee ? 1UL : 2UL);
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(point.X));
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(point.Z));
        }
    }
}
