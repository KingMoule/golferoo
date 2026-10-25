using Golferoo.Sim.Golfers;
using Golferoo.Sim.Pathing;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Trees;

namespace Golferoo.Sim;

/// <summary>
/// Monde simulé, avancé par ticks fixes. Le rendu lit son état et lui envoie des commandes ;
/// il ne connaît pas Godot.
/// </summary>
public sealed partial class World
{
    /// <summary>Fréquence fixe de la simulation (spec C01 : 20 Hz).</summary>
    public const int TicksPerSecond = 20;

    /// <summary>Durée d'un tick en secondes.</summary>
    public const double TickSeconds = 1.0 / TicksPerSecond;

    /// <summary>Durée d'un coup (geste), en ticks.</summary>
    private const int SwingTicksCount = TicksPerSecond;

    /// <summary>Ramassage de la balle après ce nombre de coups : garde-fou contre une partie sans fin.</summary>
    public const int MaxStrokes = 12;

    private const int WaypointPoolSize = 16;

    private readonly DeterministicRng _rng;
    private readonly IShotResolver _resolver;
    private readonly List<Group> _groups = [];
    private readonly List<Golfer> _roamers = [];
    private readonly List<Point2> _waypoints = [];
    private int _nextGolferId;
    private Point2? _exitPoint;
    private long _exitPointVersion = -1;

    public World(ulong seed, TerrainGrid? terrain = null, IShotResolver? resolver = null)
    {
        Seed = seed;
        Terrain = terrain;
        _rng = new DeterministicRng(seed);
        _resolver = resolver ?? (terrain is null ? new StubShotResolver() : new PhysicalShotResolver());
        Flow = terrain is null ? null : new FlowFieldCache(terrain);
        if (terrain is not null)
        {
            Trees = new TreeStand(terrain.CellsX * terrain.CellSize, terrain.CellsZ * terrain.CellSize);
            foreach (var p in terrain.IsolatedTrees)
            {
                Trees.Add(p.X, p.Z, p.Species, p.AgeMonths);
            }

            WoodlandGenerator.Fill(Trees, terrain, new CellRect(0, 0, terrain.CellsX - 1, terrain.CellsZ - 1), NextWoodlandRng());
        }
        else
        {
            Trees = new TreeStand(1.0, 1.0);
        }
    }

    /// <summary>Arbres du monde (nul coût de marche : seul le sous-bois, surface Trees, bloque les golfeurs).</summary>
    public TreeStand Trees { get; }

    private int _woodlandDraws;

    /// <summary>RNG propre au boisement, dérivé de la graine et du rang du tirage : ne touche pas au RNG du tick.</summary>
    private DeterministicRng NextWoodlandRng() => new(Seed ^ (0x7EE5BA5E00000000UL + (ulong)_woodlandDraws++));

    public ulong Seed { get; }

    /// <summary>Terrain du trou, s'il y en a un (son état entre dans la somme de contrôle).</summary>
    public TerrainGrid? Terrain { get; }

    /// <summary>Cache de champs de flux du terrain, s'il y en a un.</summary>
    public FlowFieldCache? Flow { get; }

    /// <summary>Nombre de ticks écoulés depuis la création.</summary>
    public long Tick { get; private set; }

    public IReadOnlyList<Group> Groups => _groups;

    /// <summary>Golfeurs du mode charge.</summary>
    public IReadOnlyList<Golfer> Roamers => _roamers;

    /// <summary>Point où les golfeurs sortent du green (près du green, hors green et hors bunker).</summary>
    public Point2 ExitPoint
    {
        get
        {
            var terrain = RequireTerrain();
            if (_exitPoint is null || _exitPointVersion != terrain.SurfaceVersion)
            {
                _exitPoint = ComputeExitPoint(terrain);
                _exitPointVersion = terrain.SurfaceVersion;
            }

            return _exitPoint.Value;
        }
    }

    /// <summary>Ajoute une paire de golfeurs à l'entrée du tee. Elle joue quand la précédente a quitté le green.</summary>
    public Group SpawnGroup()
    {
        var terrain = RequireTerrain();
        var spawn = new Point2(terrain.Tee.X - 10.0, terrain.Tee.Z);
        var golfers = new List<Golfer>(2);
        for (var i = 0; i < 2; i++)
        {
            var p = GridQueries.NearestWalkablePoint(terrain, new Point2(spawn.X, spawn.Z + ((i * 3.0) - 1.5)));
            var golfer = new Golfer(_nextGolferId++, p, GolferState.Arriving) { Ball = terrain.Tee };
            golfers.Add(golfer);
        }

        var group = new Group(_groups.Count, golfers);
        _groups.Add(group);
        return group;
    }

    /// <summary>
    /// Mode charge : remplace les golfeurs du mode charge par <paramref name="golfers"/> marcheurs qui vont en boucle
    /// entre des points tirés au RNG sur des cellules franchissables, sans jouer.
    /// </summary>
    public void SetLoadMode(int golfers)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(golfers);
        var terrain = RequireTerrain();
        _roamers.Clear();
        _waypoints.Clear();
        if (golfers == 0)
        {
            return;
        }

        // Seules les cellules reliées au tee comptent : un golfeur ne vise jamais une poche isolée.
        var reference = Flow!.Get(terrain.Tee);
        for (var tries = 0; _waypoints.Count < WaypointPoolSize && tries < 2000; tries++)
        {
            if (TryRandomReachableCell(terrain, reference, out var p))
            {
                _waypoints.Add(p);
            }
        }

        if (_waypoints.Count == 0)
        {
            _waypoints.Add(terrain.CellCenter(reference.DestX, reference.DestZ));
        }

        for (var i = 0; i < golfers; i++)
        {
            var start = TryRandomReachableCell(terrain, reference, out var p) ? p : _waypoints[0];
            _roamers.Add(new Golfer(_nextGolferId++, start, GolferState.Roaming)
            {
                WaypointIndex = (int)(_rng.NextUInt64() % (ulong)_waypoints.Count),
            });
        }
    }

    public void Step()
    {
        // Le tirage garde l'état du RNG lié au temps : la somme de contrôle prouve le déterminisme même à vide.
        _rng.NextUInt64();
        ApplyPendingEdits();
        if (Terrain is not null)
        {
            for (var i = 0; i < _groups.Count; i++)
            {
                UpdateGroup(Terrain, i);
            }

            for (var i = 0; i < _roamers.Count; i++)
            {
                UpdateRoamer(Terrain, _roamers[i]);
            }
        }

        Tick++;
    }

    public void Step(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            Step();
        }
    }

    /// <summary>Instantané en lecture seule pour le rendu. Réutilise <paramref name="reuse"/> si fourni.</summary>
    public WorldSnapshot Snapshot(WorldSnapshot? reuse = null)
    {
        var snap = reuse ?? new WorldSnapshot();
        snap.Tick = Tick;
        if (snap.TreesVersion != Trees.Version)
        {
            snap.TreeItems.Clear();
            snap.TreeItems.AddRange(Trees.Trees);
            snap.TreesVersion = Trees.Version;
        }

        var items = snap.Items;
        items.Clear();
        foreach (var group in _groups)
        {
            foreach (var g in group.Golfers)
            {
                items.Add(new GolferView(g.Id, group.Id, g.Position, g.Ball, g.State, g.Strokes, g.Holed, BallPosition(g)));
            }
        }

        foreach (var g in _roamers)
        {
            items.Add(new GolferView(g.Id, -1, g.Position, g.Ball, g.State, g.Strokes, g.Holed, BallPosition(g)));
        }

        return snap;
    }

    private Vec3 BallPosition(Golfer g)
    {
        if (g.State == GolferState.BallInFlight && g.FlightPath is { } path && path.Count > 0)
        {
            return path[Math.Min(g.FlightIndex, path.Count - 1)];
        }

        return new Vec3(g.Ball.X, Terrain?.Sample(g.Ball.X, g.Ball.Z) ?? 0.0, g.Ball.Z);
    }

    /// <summary>Somme de contrôle de l'état (FNV-1a 64 bits) : même graine et mêmes commandes, même valeur.</summary>
    public ulong Checksum()
    {
        var hash = 0xCBF29CE484222325UL;
        Mix(ref hash, Seed);
        Mix(ref hash, (ulong)Tick);
        Mix(ref hash, _rng.StateHash());
        if (Terrain is not null)
        {
            Mix(ref hash, Terrain.Checksum());
        }

        MixPendingEdits(ref hash);
        Mix(ref hash, (ulong)Trees.Count);
        foreach (var t in Trees.Trees)
        {
            Mix(ref hash, (ulong)t.Id);
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(t.X));
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(t.Z));
            Mix(ref hash, ((ulong)t.Species << 32) ^ (uint)t.AgeMonths ^ ((ulong)t.State << 48));
        }

        Mix(ref hash, (ulong)_groups.Count);
        foreach (var group in _groups)
        {
            Mix(ref hash, group.Started ? 1UL : 0UL);
            foreach (var g in group.Golfers)
            {
                MixGolfer(ref hash, g);
            }
        }

        Mix(ref hash, (ulong)_roamers.Count);
        foreach (var g in _roamers)
        {
            MixGolfer(ref hash, g);
        }

        return hash;
    }

    private static void MixGolfer(ref ulong hash, Golfer g)
    {
        Mix(ref hash, (ulong)g.Id);
        Mix(ref hash, (ulong)g.State);
        Mix(ref hash, (ulong)g.Strokes);
        Mix(ref hash, (ulong)g.SwingTicks);
        Mix(ref hash, (ulong)g.WaypointIndex);
        Mix(ref hash, g.Holed ? 1UL : 0UL);
        Mix(ref hash, g.PickedUp ? 1UL : 0UL);
        Mix(ref hash, (ulong)g.FlightIndex);
        if (g.State == GolferState.BallInFlight && g.FlightPath is { } path && path.Count > 0)
        {
            var p = path[Math.Min(g.FlightIndex, path.Count - 1)];
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(p.X));
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(p.Y));
            Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(p.Z));
        }

        Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(g.Position.X));
        Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(g.Position.Z));
        Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(g.Ball.X));
        Mix(ref hash, (ulong)BitConverter.DoubleToInt64Bits(g.Ball.Z));
    }

    private static void Mix(ref ulong hash, ulong value)
    {
        for (var i = 0; i < 8; i++)
        {
            hash ^= (value >> (i * 8)) & 0xFF;
            hash *= 0x100000001B3UL;
        }
    }

    private static Point2 ComputeExitPoint(TerrainGrid terrain)
    {
        var want = new Point2(terrain.Green.X, terrain.Green.Z + 20.0);
        var best = double.MaxValue;
        var found = false;
        var result = want;
        for (var cz = 0; cz < terrain.CellsZ; cz++)
        {
            for (var cx = 0; cx < terrain.CellsX; cx++)
            {
                var s = terrain.GetCellSurface(cx, cz);
                if (s is Surface.Green or Surface.Bunker || !terrain.IsWalkable(cx, cz))
                {
                    continue;
                }

                var c = terrain.CellCenter(cx, cz);
                var d = ((c.X - want.X) * (c.X - want.X)) + ((c.Z - want.Z) * (c.Z - want.Z));
                if (d < best)
                {
                    best = d;
                    result = c;
                    found = true;
                }
            }
        }

        return found ? result : GridQueries.NearestWalkablePoint(terrain, want);
    }

    private TerrainGrid RequireTerrain() =>
        Terrain ?? throw new InvalidOperationException("Ce monde n'a pas de terrain.");

    private bool TryRandomReachableCell(TerrainGrid terrain, FlowField reference, out Point2 p)
    {
        var cx = (int)(_rng.NextUInt64() % (ulong)terrain.CellsX);
        var cz = (int)(_rng.NextUInt64() % (ulong)terrain.CellsZ);
        if (terrain.IsWalkable(cx, cz) && reference.IsReachable(cx, cz))
        {
            p = terrain.CellCenter(cx, cz);
            return true;
        }

        p = default;
        return false;
    }

    private void UpdateRoamer(TerrainGrid terrain, Golfer g)
    {
        if (_waypoints.Count == 0)
        {
            return;
        }

        var target = _waypoints[g.WaypointIndex];
        var field = Flow!.Get(target);
        var pos = g.Position;
        var result = FlowWalker.Advance(terrain, field, ref pos, target, TickSeconds);
        g.Position = pos;
        if (result != WalkResult.Moving)
        {
            g.WaypointIndex = (int)(_rng.NextUInt64() % (ulong)_waypoints.Count);
        }
    }

    private void UpdateGroup(TerrainGrid terrain, int index)
    {
        var group = _groups[index];
        foreach (var g in group.Golfers)
        {
            switch (g.State)
            {
                case GolferState.Arriving:
                case GolferState.WalkingToBall:
                    if (Walk(terrain, g, g.Ball))
                    {
                        g.State = GolferState.WaitingTurn;
                    }

                    break;
                case GolferState.Exiting:
                    if (Walk(terrain, g, ExitPoint))
                    {
                        g.State = GolferState.Done;
                    }

                    break;
                case GolferState.Swinging:
                case GolferState.Putting:
                    if (--g.SwingTicks <= 0)
                    {
                        Hit(terrain, g);
                    }

                    break;
                case GolferState.BallInFlight:
                    if (++g.FlightIndex >= g.FlightPath!.Count - 1)
                    {
                        Land(g);
                    }

                    break;
                default:
                    break;
            }
        }

        // Un coup à la fois dans le groupe ; le plus loin du trou joue.
        if (!group.Started && index > 0 && !_groups[index - 1].Finished)
        {
            return;
        }

        Golfer? shooter = null;
        var farthest = -1.0;
        foreach (var g in group.Golfers)
        {
            if (g.State is GolferState.Swinging or GolferState.Putting or GolferState.BallInFlight)
            {
                return;
            }

            if (g.Holed || g.PickedUp || g.State == GolferState.Exiting || g.State == GolferState.Done)
            {
                continue;
            }

            var dx = terrain.Hole.X - g.Ball.X;
            var dz = terrain.Hole.Z - g.Ball.Z;
            var d2 = (dx * dx) + (dz * dz);
            if (d2 > farthest)
            {
                farthest = d2;
                shooter = g;
            }
        }

        if (shooter is not null && shooter.State == GolferState.WaitingTurn)
        {
            group.Started = true;
            shooter.State = terrain.SurfaceAt(shooter.Ball.X, shooter.Ball.Z) == Surface.Green ? GolferState.Putting : GolferState.Swinging;
            shooter.SwingTicks = SwingTicksCount;
        }
    }

    private bool Walk(TerrainGrid terrain, Golfer g, Point2 target)
    {
        var field = Flow!.Get(target);
        var pos = g.Position;
        var result = FlowWalker.Advance(terrain, field, ref pos, target, TickSeconds);
        g.Position = pos;
        if (result == WalkResult.Stuck)
        {
            // Poche inatteignable : on repose le golfeur sur la cellule franchissable la plus proche de sa destination.
            g.Position = GridQueries.NearestWalkablePoint(terrain, target);
            return true;
        }

        return result == WalkResult.Arrived;
    }

    private void Hit(TerrainGrid terrain, Golfer g)
    {
        var request = new ShotRequest(g.Ball, terrain.Hole, g.Strokes);
        var result = _resolver.Resolve(request, terrain, _rng);
        g.Pending = result;
        g.FlightPath = result.Trajectory;
        g.FlightIndex = 0;
        if (result.Trajectory is null || result.Trajectory.Count <= 1)
        {
            Land(g);
            return;
        }

        // La balle part : les coups et pénalités ne sont comptés qu'à son arrêt.
        g.State = GolferState.BallInFlight;
    }

    private void Land(Golfer g)
    {
        var result = g.Pending;
        g.Strokes += 1 + result.PenaltyStrokes;
        g.Ball = result.Ball;

        // La trajectoire a été calculée avant une éventuelle retouche : si la balle retombe dans une cellule
        // repeinte en eau ou en arbres, on la repose comme les balles au repos (sans pénalité).
        if (Terrain is { } terrain && !result.Holed && !terrain.IsWalkable(terrain.CellXOf(g.Ball.X), terrain.CellZOf(g.Ball.Z)))
        {
            g.Ball = GridQueries.NearestWalkablePoint(terrain, g.Ball);
        }

        g.FlightPath = null;
        g.FlightIndex = 0;
        g.Pending = default;
        if (result.Holed || g.Strokes >= MaxStrokes)
        {
            g.Holed = result.Holed;
            g.PickedUp = !result.Holed;
            g.State = GolferState.Exiting;
        }
        else
        {
            g.State = GolferState.WalkingToBall;
        }
    }
}
