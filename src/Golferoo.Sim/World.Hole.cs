using Golferoo.Sim.Rating;
using Golferoo.Sim.Scoring;
using Golferoo.Sim.Terrain;

namespace Golferoo.Sim;

/// <summary>
/// Pose du départ et du trou (C07), en file comme <see cref="PaintSurface"/> : appliquée au début du tick suivant,
/// comptée dans <see cref="Checksum"/> (par le terrain pour l'état, par la file pour les commandes en attente).
/// </summary>
public sealed partial class World
{
    private readonly Queue<(int Hole, bool IsTee, Point2 Point)> _holeCommands = new();
    private readonly Queue<(bool Add, Point2 Tee, Point2 Pin)> _courseCommands = new();
    private bool[] _holesOpen = [true];
    private bool _ratingStale;
    private readonly List<(int Round, (int MemberId, ScoreCard Card)[] Cards, long Tick)> _heldCards = [];

    /// <summary>Ajouts et retraits de trou refusés à l'application (un groupe a pris le trou, ou la place a changé depuis la mise en file) : le jeu le dit au joueur.</summary>
    public int HoleCommandsRefused { get; private set; }

    /// <summary>
    /// Applique une note. Une note qui ne compte pas le nombre de trous du parcours est ignorée (calculée avant un ajout ou un retrait).
    /// Une note valable après un changement de parcours rend à la ligue les cartes gardées en attente, avec leur écart.
    /// </summary>
    private void ApplyRating(CourseRating rating)
    {
        if (Terrain is { } terrain && rating.Holes != terrain.Course.Count)
        {
            return;
        }

        CourseRating = rating;
        if (!_ratingStale)
        {
            return;
        }

        _ratingStale = false;
        foreach (var (round, cards, tick) in _heldCards)
        {
            League!.GroupFinished(round, cards, rating, tick);
        }

        _heldCards.Clear();
    }

    private void MixHeldCards(ref ulong hash)
    {
        if (_ratingStale || HoleCommandsRefused > 0)
        {
            Mix(ref hash, (_ratingStale ? 5UL : 6UL) | ((ulong)HoleCommandsRefused << 8) | ((ulong)_heldCards.Count << 40));
        }

        foreach (var (round, cards, tick) in _heldCards)
        {
            Mix(ref hash, ((ulong)round << 32) ^ (ulong)tick);
            foreach (var (id, card) in cards)
            {
                Mix(ref hash, (ulong)id);
                Mix(ref hash, (ulong)card.Gross);
            }
        }
    }
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

    /// <summary>
    /// Met en file l'ajout d'un trou en fin de parcours : départ en <paramref name="tee"/> (cellule de départ), drapeau en <paramref name="pin"/> (green),
    /// numéro suivant, par d'après la distance (<see cref="TerrainGrid.DeducedPar"/>). Refusé (faux) si le parcours a déjà 18 trous, si les cellules ne conviennent pas,
    /// ou si <see cref="HoleCheck"/> local y trouve un manque. Jamais refusé à cause des groupes : un groupe déjà parti garde sa carte de N trous et finit après son dernier trou prévu, les groupes lancés ensuite jouent N+1. Invalide <see cref="CourseRating"/>.
    /// </summary>
    public bool AddHole(Point2 tee, Point2 pin)
    {
        var terrain = RequireTerrain();
        if (ProjectedHoleCount(terrain) >= Course.MaxHoles || !double.IsFinite(tee.X) || !double.IsFinite(tee.Z) || !double.IsFinite(pin.X) || !double.IsFinite(pin.Z))
        {
            return false;
        }

        if (NewHoleIssues(terrain, tee, pin).Count != 0)
        {
            return false;
        }

        _courseCommands.Enqueue((true, tee, pin));
        return true;
    }

    /// <summary>Raisons pour lesquelles un trou de départ <paramref name="tee"/> et de drapeau <paramref name="pin"/> ne convient pas (vide si tout va bien).</summary>
    public IReadOnlyList<HoleIssue> NewHoleIssues(Point2 tee, Point2 pin) => NewHoleIssues(RequireTerrain(), tee, pin);

    private static List<HoleIssue> NewHoleIssues(TerrainGrid terrain, Point2 tee, Point2 pin) =>
        HoleCheck.Check(terrain, new CourseHole(terrain.Course.Count + 1, tee, pin, pin, TerrainGrid.DeducedPar(tee, pin)), true);

    /// <summary>Met en file le retrait du dernier trou. Refusé (faux) s'il ne reste qu'un trou ou si un groupe non fini a ce trou dans sa carte (il le joue, y marche ou y jouera). Invalide <see cref="CourseRating"/>.</summary>
    public bool RemoveLastHole()
    {
        var count = ProjectedHoleCount(RequireTerrain());
        if (count <= 1 || LastHoleInUse(count))
        {
            return false;
        }

        _courseCommands.Enqueue((false, default, default));
        return true;
    }

    /// <summary>Un groupe non fini a le trou <paramref name="last"/> (le dernier) dans sa carte de score.</summary>
    private bool LastHoleInUse(int last) => _groups.Any(g => !g.Finished && g.Golfers.Any(o => o.Card!.HoleCount >= last));

    private int ProjectedHoleCount(TerrainGrid terrain) => terrain.Course.Count + _courseCommands.Sum(c => c.Add ? 1 : -1);

    private void ApplyCourseCommands(TerrainGrid terrain)
    {
        while (_courseCommands.Count > 0)
        {
            var (add, tee, pin) = _courseCommands.Dequeue();
            if (add && terrain.Course.Count < Course.MaxHoles && NewHoleIssues(terrain, tee, pin).Count == 0)
            {
                terrain.Course.Add(new CourseHole(terrain.Course.Count + 1, tee, pin, pin, TerrainGrid.DeducedPar(tee, pin)));
                _holesOpen = [.. _holesOpen, true];
            }
            else if (!add && terrain.Course.Count > 1 && !LastHoleInUse(terrain.Course.Count))
            {
                terrain.Course.RemoveLast();
                _holesOpen = _holesOpen[..^1];
                foreach (var g in _groups.Where(g => g.Hole > terrain.Course.Count))
                {
                    g.Hole = terrain.Course.Count; // groupe fini : il ne doit pas pointer un trou retiré
                }
            }
            else
            {
                HoleCommandsRefused++;
                continue;
            }

            _holeCheckedVersion = -1;
            _ratingStale = League is not null;
            CourseRating = null; // la note d'un autre nombre de trous ne vaut plus ; le jeu la refait
        }
    }

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

        ApplyCourseCommands(terrain);
        while (_holeCommands.Count > 0)
        {
            var (number, isTee, point) = _holeCommands.Dequeue();
            if (number > terrain.Course.Count)
            {
                continue;
            }

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
        foreach (var (add, tee, pin) in _courseCommands)
        {
            Mix(ref hash, add ? 3UL : 4UL);
            foreach (var v in new[] { tee.X, tee.Z, pin.X, pin.Z })
            {
                Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(v));
            }
        }

        foreach (var (hole, isTee, point) in _holeCommands)
        {
            Mix(ref hash, (isTee ? 1UL : 2UL) | ((ulong)(hole - 1) << 8));
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(point.X));
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(point.Z));
        }
    }
}
