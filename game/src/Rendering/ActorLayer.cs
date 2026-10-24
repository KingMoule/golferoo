using Godot;
using Golferoo.Sim;
using Golferoo.Sim.Golfers;
using Golferoo.Sim.Terrain;

namespace Golferoo.Game.Rendering;

/// <summary>
/// Golfeurs (capsules), balles (sphères) et drapeau provisoires. Les golfeurs et les balles sont interpolés
/// entre l'instantané précédent et le courant. Le lot 7 remplacera ces formes.
/// </summary>
public partial class ActorLayer : Node3D
{
    private const int Capacity = 1024;

    private readonly Dictionary<int, GolferView> _previous = new();
    private MultiMesh? _golfers;
    private MultiMesh? _balls;
    private TerrainGrid? _grid;

    public int VisibleGolfers { get; private set; }

    public void Build(TerrainGrid grid)
    {
        _grid = grid;
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _golfers = NewMultiMesh(new CapsuleMesh { Radius = 0.3f, Height = 1.8f, RadialSegments = 8, Rings = 3 });
        _balls = NewMultiMesh(new SphereMesh { Radius = 0.12f, Height = 0.24f, RadialSegments = 8, Rings = 4 });
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
        MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.8f },
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
    });

    private void BuildFlag(TerrainGrid grid)
    {
        var y = (float)grid.Sample(grid.Hole.X, grid.Hole.Z);
        var root = new Node3D { Name = "Flag", Position = new Vector3((float)grid.Hole.X, y, (float)grid.Hole.Z) };
        root.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.025f, Height = 2.4f, RadialSegments = 6, Rings = 1 },
            Position = new Vector3(0, 1.2f, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.92f, 0.92f, 0.92f) },
        });
        root.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.6f, 0.4f, 0.02f) },
            Position = new Vector3(0.3f, 2.2f, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.15f, 0.12f), CullMode = BaseMaterial3D.CullModeEnum.Disabled },
        });
        AddChild(root);
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
            _golfers.SetInstanceTransform(i, new Transform3D(Basis.Identity, new Vector3((float)pos.X, y + 0.9f, (float)pos.Z)));
            _golfers.SetInstanceColor(i, StateColor(cur.State, cur.Id));

            if (cur.State != GolferState.Roaming && cur.State != GolferState.Done && !cur.Holed)
            {
                _balls.SetInstanceTransform(nb, new Transform3D(Basis.Identity, new Vector3((float)ball.X, (float)ball.Y + 0.12f, (float)ball.Z)));
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
