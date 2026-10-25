using Godot;
using Golferoo.Sim;
using Golferoo.Sim.Terrain;
using Golferoo.Sim.Time;
using Golferoo.Sim.Turf;

namespace Golferoo.Game.Rendering;

/// <summary>
/// Tondeuses du matin, au rendu seulement : pendant le premier quart de la journée, une tondeuse par zone tondue ce jour-là
/// (<see cref="TurfGrid.IsMowedOn"/>, composantes connexes d'une même surface) la parcourt en allers-retours le long des bandes de tonte.
/// Elle s'arrête quand un golfeur est à moins de <see cref="StopDistance"/> m. Rien de ce qu'elle fait ne revient dans la simulation.
/// Au plus <see cref="MaxMowers"/> zones servies, les plus proches du trou d'abord.
/// </summary>
public partial class MowerLayer : Node3D
{
    private const int MaxMowers = 10;
    private const int MinCells = 4;
    private const float StripeWidth = 6f; // stripe_width de terrain.gdshader : les bandes courent le long de z
    private const float Speed = 1.5f; // m par seconde de temps simulé
    private const float GreenMowerScale = 2f; // le modèle de green fait 0,7 m : trop petit pour se voir de la vue de jeu
    private const float MorningEnd = 0.25f;
    private const float StopDistance = 15f;
    private const float DaySeconds = Calendar.TicksPerDay * (float)World.TickSeconds;

    private readonly List<Mower> _mowers = [];
    private TerrainGrid? _grid;
    private int _day = -1;
    private double _last;

    public int ActiveMowers => Visible ? _mowers.Count(m => m.Node.Visible) : 0;

    /// <summary>Position de la première tondeuse visible (pour les captures), sinon null.</summary>
    public Vector3? FirstPosition => Visible && _mowers.FirstOrDefault(m => m.Node.Visible)?.Node.Position is { } p ? p : null;

    public void Build(TerrainGrid grid) => _grid = grid;

    /// <summary><paramref name="dayFraction"/> vaut 0 à minuit et 1 à la minute d'avant ; les golfeurs viennent de l'instantané courant.</summary>
    public void Update(int dayIndex, double dayFraction, WorldSnapshot current)
    {
        if (_grid is null)
        {
            return;
        }

        var d = Math.Max(dayFraction - _last, 0.0);
        _last = dayFraction;
        if (dayFraction >= MorningEnd)
        {
            Visible = false;
            _day = -1;
            return;
        }

        if (_day != dayIndex)
        {
            _day = dayIndex;
            BuildMowers(dayIndex, dayFraction);
            d = 0.0;
        }

        Visible = true;
        var advance = (float)(d * DaySeconds * Speed);
        foreach (var m in _mowers)
        {
            if (m.S >= m.Length)
            {
                m.Node.Visible = false;
                continue;
            }

            var (p, dir) = m.At();
            // Le trajet suit l'enveloppe de la bande : cachée là où il sort de la zone (eau, sable, coude).
            m.Node.Visible = _grid.SurfaceAt(p.X, p.Y) == m.Surface;
            m.Node.Position = new Vector3(p.X, (float)_grid.Sample(p.X, p.Y), p.Y);
            m.Node.Rotation = new Vector3(0f, Mathf.Atan2(dir.X, dir.Y), 0f);
            if (!GolferNear(current, p))
            {
                m.S += advance;
            }
        }
    }

    private static bool GolferNear(WorldSnapshot current, Vector2 p)
    {
        foreach (var g in current.Golfers)
        {
            var dx = (float)g.Position.X - p.X;
            var dz = (float)g.Position.Z - p.Y;
            if ((dx * dx) + (dz * dz) < StopDistance * StopDistance)
            {
                return true;
            }
        }

        return false;
    }

    private void BuildMowers(int dayIndex, double dayFraction)
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _mowers.Clear();
        var grid = _grid!;
        var hole = new Vector2((float)grid.Hole.X, (float)grid.Hole.Z);
        var zones = FindZones(grid, dayIndex);
        zones.Sort((a, b) => a.Centroid.DistanceSquaredTo(hole).CompareTo(b.Centroid.DistanceSquaredTo(hole)));
        foreach (var zone in zones.Take(MaxMowers))
        {
            if (LoadModel(zone) is not { } node)
            {
                continue;
            }

            node.Scale = zone.Surface == Surface.Green ? Vector3.One * GreenMowerScale : Vector3.One;
            AddChild(node);
            var mower = new Mower(node, zone.Path()) { Surface = zone.Surface };
            mower.S = (float)(dayFraction * DaySeconds * Speed);
            _mowers.Add(mower);
        }
    }

    private static Node3D? LoadModel(Zone zone)
    {
        var kind = zone.Surface switch { Surface.Green => "green", Surface.Rough => "rough", _ => "fairway" };
        var path = $"res://assets/maintenance/{kind}_mower_0{1 + (int)(((uint)zone.FirstCell * 2654435761u) >> 16) % 3}.glb";
        return ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path)?.Instantiate<Node3D>() : null;
    }

    /// <summary>Composantes connexes (4 voisins) des cellules d'une même surface tondue ce jour-là.</summary>
    private static List<Zone> FindZones(TerrainGrid grid, int dayIndex)
    {
        var nx = grid.CellsX;
        var nz = grid.CellsZ;
        var cs = (float)grid.CellSize;
        var seen = new bool[nx * nz];
        var stack = new Stack<int>();
        Span<int> around = stackalloc int[4];
        var zones = new List<Zone>();
        for (var start = 0; start < seen.Length; start++)
        {
            var surface = grid.GetCellSurface(start % nx, start / nx);
            if (seen[start] || !TurfGrid.IsMowedOn(surface, dayIndex))
            {
                continue;
            }

            var zone = new Zone(surface, start);
            seen[start] = true;
            stack.Push(start);
            while (stack.Count > 0)
            {
                var i = stack.Pop();
                var cx = i % nx;
                var cz = i / nx;
                zone.Add((cx + 0.5f) * cs, (cz + 0.5f) * cs);
                around[0] = cx > 0 ? i - 1 : -1;
                around[1] = cx < nx - 1 ? i + 1 : -1;
                around[2] = cz > 0 ? i - nx : -1;
                around[3] = cz < nz - 1 ? i + nx : -1;
                foreach (var j in around)
                {
                    if (j >= 0 && !seen[j] && grid.GetCellSurface(j % nx, j / nx) == surface)
                    {
                        seen[j] = true;
                        stack.Push(j);
                    }
                }
            }

            if (zone.Count >= MinCells)
            {
                zones.Add(zone);
            }
        }

        return zones;
    }

    private sealed class Zone(Surface surface, int firstCell)
    {
        private readonly Dictionary<int, Rect2> _bands = [];
        private float _sumX;
        private float _sumZ;

        public Surface Surface { get; } = surface;

        public int FirstCell { get; } = firstCell;

        public int Count { get; private set; }

        public Vector2 Centroid => new(_sumX / Count, _sumZ / Count);

        /// <summary>Enveloppe des cellules de la bande de tonte (centres de cellules) : position = (x min, z min), taille = étendue.</summary>
        public void Add(float x, float z)
        {
            Count++;
            _sumX += x;
            _sumZ += z;
            var b = (int)MathF.Floor(x / StripeWidth);
            _bands[b] = _bands.TryGetValue(b, out var r) ? r.Expand(new Vector2(x, z)) : new Rect2(x, z, 0f, 0f);
        }

        /// <summary>Allers-retours : une passe par bande, le long de z, dans le sens inverse de la précédente.</summary>
        public Vector2[] Path()
        {
            var points = new List<Vector2>();
            var forward = true;
            foreach (var b in _bands.Keys.Order())
            {
                var r = _bands[b];
                var x = r.Position.X + (r.Size.X / 2f);
                Vector2 lo = new(x, r.Position.Y), hi = new(x, r.End.Y);
                points.Add(forward ? lo : hi);
                points.Add(forward ? hi : lo);
                forward = !forward;
            }

            return [.. points];
        }
    }

    private sealed class Mower
    {
        private readonly Vector2[] _path;
        private readonly float[] _cumulative;

        public Mower(Node3D node, Vector2[] path)
        {
            Node = node;
            _path = path;
            _cumulative = new float[path.Length];
            for (var i = 1; i < path.Length; i++)
            {
                _cumulative[i] = _cumulative[i - 1] + path[i].DistanceTo(path[i - 1]);
            }

            Length = _cumulative[^1];
        }

        public Node3D Node { get; }

        public Surface Surface { get; init; }

        public float Length { get; }

        /// <summary>Distance parcourue sur le trajet, en m.</summary>
        public float S { get; set; }

        /// <summary>Position (x, z) et direction unitaire (x, z) à la distance <see cref="S"/>.</summary>
        public (Vector2 Position, Vector2 Direction) At()
        {
            var i = 1;
            while (i < _path.Length - 1 && _cumulative[i] < S)
            {
                i++;
            }

            var span = _cumulative[i] - _cumulative[i - 1];
            var t = span > 0f ? Mathf.Clamp((S - _cumulative[i - 1]) / span, 0f, 1f) : 0f;
            var dir = span > 0f ? (_path[i] - _path[i - 1]) / span : Vector2.Down;
            return (_path[i - 1].Lerp(_path[i], t), dir);
        }
    }
}
