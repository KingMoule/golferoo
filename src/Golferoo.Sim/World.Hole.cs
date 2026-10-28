using Golferoo.Sim.Terrain;

namespace Golferoo.Sim;

/// <summary>
/// Pose du départ et du trou (C07), en file comme <see cref="PaintSurface"/> : appliquée au début du tick suivant,
/// comptée dans <see cref="Checksum"/> (par le terrain pour l'état, par la file pour les commandes en attente).
/// </summary>
public sealed partial class World
{
    private readonly Queue<(int Hole, bool IsTee, Point2 Point)> _holeCommands = new();
    private bool[] _holesOpen = [true];
    private long _holeCheckedVersion = -1;

    /// <summary>
    /// Vrai si des golfeurs peuvent jouer le trou 1 : toujours, sauf sur une terre vierge tant que <see cref="HoleCheck"/> y trouve un manque.
    /// Se recalcule au début d'un tick, après une retouche ou une pose.
    /// </summary>
    public bool HoleOpen => _holesOpen[0];

    /// <summary>Vrai si le trou de ce numéro (1 à N) est ouvert aux golfeurs ; même règle que <see cref="HoleOpen"/>.</summary>
    public bool IsHoleOpen(int number) => number >= 1 && number <= _holesOpen.Length
        ? _holesOpen[number - 1]
        : throw new ArgumentOutOfRangeException(nameof(number), number, $"Trou inexistant (1 à {_holesOpen.Length}).");

    /// <summary>Met en file la pose du départ du trou <paramref name="hole"/> en <paramref name="point"/> ; refusée (faux) si ce n'est pas sur une cellule de départ ou si le trou n'existe pas.</summary>
    public bool PlaceTee(int hole, Point2 point) => QueueHolePoint(hole, true, point, Surface.Tee);

    /// <summary>Met en file la pose du drapeau du trou <paramref name="hole"/> en <paramref name="point"/> ; refusée (faux) si ce n'est pas sur le green ou si le trou n'existe pas.</summary>
    public bool PlaceHole(int hole, Point2 point) => QueueHolePoint(hole, false, point, Surface.Green);

    private bool QueueHolePoint(int hole, bool isTee, Point2 point, Surface required)
    {
        var terrain = RequireTerrain();
        if (hole < 1 || hole > terrain.Course.Count || !double.IsFinite(point.X) || !double.IsFinite(point.Z) || terrain.SurfaceAt(point.X, point.Z) != required)
        {
            return false;
        }

        _holeCommands.Enqueue((hole, isTee, point));
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
            var (number, isTee, point) = _holeCommands.Dequeue();
            if (isTee && terrain.SurfaceAt(point.X, point.Z) == Surface.Tee)
            {
                terrain.Course[number].MoveTee(point);
                _holeCheckedVersion = -1;
            }
            else if (!isTee && terrain.SurfaceAt(point.X, point.Z) == Surface.Green)
            {
                terrain.Course[number].MovePin(point);
                _holeCheckedVersion = -1;
            }
        }

        if (terrain.Blank && _holeCheckedVersion != terrain.Version)
        {
            _holesOpen = [.. terrain.Course.Holes.Select(h => HoleCheck.Check(terrain, h).Count == 0)];
            _holeCheckedVersion = terrain.Version;
        }
    }

    private void MixHoleCommands(ref ulong hash)
    {
        foreach (var (hole, isTee, point) in _holeCommands)
        {
            Mix(ref hash, (isTee ? 1UL : 2UL) | ((ulong)(hole - 1) << 8));
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(point.X));
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(point.Z));
        }
    }
}
