using Godot;
using Golferoo.Sim;
using Golferoo.Sim.Golfers;
using Golferoo.Sim.Terrain;

namespace Golferoo.Game.Rendering;

/// <summary>
/// Golfeurs, balles et drapeau (maillages de <see cref="ProceduralMeshes"/>). Les golfeurs et les balles sont interpolés
/// entre l'instantané précédent et le courant.
/// </summary>
public partial class ActorLayer : Node3D
{
    private const int Capacity = 1024;

    private readonly Dictionary<int, GolferView> _previous = new();
    private MultiMesh? _golfers;
    private MultiMesh? _balls;
    private TerrainGrid? _grid;
    private Node3D? _flag;

    public int VisibleGolfers { get; private set; }

    public void Build(TerrainGrid grid)
    {
        _grid = grid;
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _golfers = NewMultiMesh(ProceduralMeshes.Golfer());
        _balls = NewMultiMesh(ProceduralMeshes.Ball());
        AddMulti("Golfers", _golfers);
        AddMulti("Balls", _balls);
        BuildFlag(grid);
    }

    private static MultiMesh NewMultiMesh(Mesh mesh) => new()
    {
        TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
        UseColors = true,
        Mesh = mesh,
        InstanceCount = Capacity,
        VisibleInstanceCount = 0,
    };

    private void AddMulti(string name, MultiMesh multi) => AddChild(new MultiMeshInstance3D
    {
        Name = name,
        Multimesh = multi,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
    });

    private void BuildFlag(TerrainGrid grid)
    {
        var y = (float)grid.Sample(grid.Hole.X, grid.Hole.Z);
        _flag = new Node3D { Name = "Flag", Position = new Vector3((float)grid.Hole.X, y, (float)grid.Hole.Z) };
        _flag.AddChild(new MeshInstance3D { Name = "FlagMesh", Mesh = ProceduralMeshes.Flag() });
        AddChild(_flag);
    }

    /// <summary>Oriente le fanion (pointé vers +X dans son maillage) dans le sens du vent (x, z du monde) ; sans vent, il ne bouge pas.</summary>
    public void SetWind(Vector2 direction)
    {
        if (_flag is not null && direction.LengthSquared() > 1e-4f)
        {
            _flag.Rotation = new Vector3(0f, -Mathf.Atan2(direction.Y, direction.X), 0f);
        }
    }

    /// <summary>Met à jour les instances. <paramref name="alpha"/> vaut 0 à l'instantané précédent, 1 au courant.</summary>
    public void Update(WorldSnapshot current, WorldSnapshot previous, float alpha, bool previousChanged)
    {
        if (_golfers is null || _balls is null || _grid is null)
        {
            return;
        }

        if (previousChanged)
        {
            _previous.Clear();
            foreach (var view in previous.Golfers)
            {
                _previous[view.Id] = view;
            }
        }

        var n = Math.Min(current.Golfers.Count, Capacity);
        var nb = 0;
        for (var i = 0; i < n; i++)
        {
            var cur = current.Golfers[i];
            var pos = cur.Position;
            var ball = cur.Ball3D;
            var hasPrev = _previous.TryGetValue(cur.Id, out var prev);
            if (hasPrev)
            {
                pos = new Point2(Lerp(prev.Position.X, cur.Position.X, alpha), Lerp(prev.Position.Z, cur.Position.Z, alpha));
                ball = new Vec3(Lerp(prev.Ball3D.X, cur.Ball3D.X, alpha), Lerp(prev.Ball3D.Y, cur.Ball3D.Y, alpha), Lerp(prev.Ball3D.Z, cur.Ball3D.Z, alpha));
            }

            var y = (float)_grid.Sample(pos.X, pos.Z);
            _golfers.SetInstanceTransform(i, new Transform3D(Basis.Identity, new Vector3((float)pos.X, y, (float)pos.Z)));
            _golfers.SetInstanceColor(i, StateColor(cur.State, cur.Id));

            if (cur.State != GolferState.Roaming && cur.State != GolferState.Done && !cur.Holed)
            {
                _balls.SetInstanceTransform(nb, new Transform3D(Basis.Identity, new Vector3((float)ball.X, (float)ball.Y + ProceduralMeshes.BallRadius, (float)ball.Z)));
                _balls.SetInstanceColor(nb, Colors.White);
                nb++;
            }
        }

        _golfers.VisibleInstanceCount = n;
        _balls.VisibleInstanceCount = nb;
        VisibleGolfers = n;
    }

    private static double Lerp(double a, double b, float t) => a + ((b - a) * t);

    private static Color StateColor(GolferState state, int id) => state switch
    {
        GolferState.Swinging or GolferState.Putting => new Color(0.95f, 0.75f, 0.1f),
        GolferState.BallInFlight => new Color(0.95f, 0.5f, 0.15f),
        GolferState.WaitingTurn => new Color(0.9f, 0.9f, 0.95f),
        GolferState.Roaming => RoamerColor(id),
        GolferState.Exiting or GolferState.Done => new Color(0.55f, 0.55f, 0.6f),
        _ => new Color(0.2f, 0.45f, 0.9f),
    };

    private static Color RoamerColor(int id) => Color.FromHsv((id * 0.618f) % 1f, 0.55f, 0.95f);
}
