using Godot;
using Golferoo.Sim;
using Golferoo.Sim.Golfers;
using Golferoo.Sim.Terrain;

namespace Golferoo.Game.Rendering;

/// <summary>
/// Golfeurs, balles, et par trou : drapeau, repère de départ et numéro (maillages de <see cref="ProceduralMeshes"/>). Les golfeurs et les balles sont interpolés
/// entre l'instantané précédent et le courant. C23 : les golfeurs à moins de <see cref="Range"/> de la caméra, au plus <see cref="PoolSize"/>, sont des
/// <see cref="GolferRig"/> animés ; les autres restent des silhouettes du MultiMesh, posées au même endroit.
/// </summary>
public partial class ActorLayer : Node3D
{
    private const int Capacity = 1024;

    /// <summary>Borne du pool de golfeurs animés (cible de la spec C23 : 200 à l'écran).</summary>
    public const int PoolSize = 200;

    /// <summary>Distance à la caméra en deçà de laquelle un golfeur est animé, en mètres.</summary>
    public const float AnimatedRange = 150f;

    /// <summary>Sous cette distance au drapeau (m), le coup est un chip : le swing réduit.</summary>
    private const double ChipRange = 40.0;

    /// <summary>Ticks pour se mettre à l'adresse (ou en repartir) : pivot et pas de côté.</summary>
    private const double StanceTicks = 6.0;

    private readonly Dictionary<int, GolferView> _previous = new();
    private readonly Dictionary<int, Track> _tracks = [];
    private readonly List<GolferRig> _free = [];
    private readonly List<(int Index, float Distance2)> _near = [];
    private readonly List<int> _stale = [];
    private readonly HashSet<int> _chosen = [];
    private GolferModel? _model;
    private int _rigs;
    private int _frame;
    private MultiMesh? _golfers;
    private MultiMesh? _balls;
    private TerrainGrid? _grid;
    private readonly List<(Node3D Flag, Point2 At)> _flags = [];
    private readonly List<Node> _holeNodes = [];

    public int VisibleGolfers { get; private set; }

    /// <summary>Golfeurs animés à cette image (rigs du pool en service).</summary>
    public int AnimatedGolfers { get; private set; }

    /// <summary>Rigs créés : jamais plus de <see cref="PoolSize"/>.</summary>
    public int RigCount => _rigs;

    public float Range { get; set; } = AnimatedRange;

    /// <summary>Point de vue pour la distance d'animation ; par défaut la caméra du viewport.</summary>
    public Vector3? Viewer { get; set; }

    /// <summary>Banc (`--fps-bench-golfers`) : un promeneur sur deux joue le swing en boucle, pour mesurer marche et frappe.</summary>
    public bool BenchSwing { get; set; }

    public GolferModel? Model => _model;

    /// <summary>Le rig d'un golfeur s'il est animé, sinon null.</summary>
    public GolferRig? RigOf(int id) => _tracks.TryGetValue(id, out var t) ? t.Rig : null;

    /// <summary>Position affichée d'un golfeur à la dernière image (rig ou silhouette).</summary>
    public Vector3? PositionOf(int id) => _tracks.TryGetValue(id, out var t) ? t.Shown : null;

    /// <summary>Suivi d'un golfeur d'une image à l'autre : état et tick d'entrée, phase de marche, orientation, mise à l'adresse.</summary>
    private sealed class Track
    {
        public GolferState State = (GolferState)255;
        public long Entered;
        public string Stroke = GolferModel.Swing;
        public double Phase;
        public float Yaw;
        public double Stance;
        public double Display;
        public Vector3 Shown;
        public Vector3 At;
        public bool Placed;
        public int Frame;
        public GolferRig? Rig;
    }

    public void Build(TerrainGrid grid)
    {
        _grid = grid;
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _tracks.Clear();
        _free.Clear();
        _rigs = 0;
        _model ??= GolferModel.Load();
        _golfers = NewMultiMesh(ProceduralMeshes.Golfer());
        _balls = NewMultiMesh(ProceduralMeshes.Ball());
        AddMulti("Golfers", _golfers);
        AddMulti("Balls", _balls);
        BuildHoles(grid);
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

    /// <summary>Un drapeau, un repère de départ et le numéro pour chaque trou du parcours.</summary>
    private void BuildHoles(TerrainGrid grid)
    {
        foreach (var node in _holeNodes)
        {
            RemoveChild(node); // tout de suite : le nom (Flag4) doit être libre pour le nœud suivant
            node.QueueFree();
        }

        _holeNodes.Clear();
        _flags.Clear();
        foreach (var hole in grid.Course.Holes)
        {
            var flag = new Node3D { Name = $"Flag{hole.Number}" };
            flag.AddChild(new MeshInstance3D { Name = "FlagMesh", Mesh = ProceduralMeshes.Flag() });
            AddHoleNode(flag);
            _flags.Add((flag, default));
            PlaceFlag(grid, hole);
            if (grid.Blank)
            {
                continue; // terre vierge : le départ n'est pas encore posé
            }

            var y = (float)grid.Sample(hole.Tee.X, hole.Tee.Z);
            var tee = new Vector3((float)hole.Tee.X, y, (float)hole.Tee.Z);
            AddHoleNode(new MeshInstance3D { Name = $"TeeMarker{hole.Number}", Mesh = ProceduralMeshes.TeeMarker(), Position = tee });
            AddHoleNode(new Label3D
            {
                Name = $"HoleNumber{hole.Number}",
                Text = hole.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Shaded = false,
                FontSize = 96,
                PixelSize = 0.06f,
                OutlineSize = 24,
                Modulate = Colors.White,
                OutlineModulate = new Color(0.1f, 0.2f, 0.5f),
                Position = tee + new Vector3(0f, 7f, 0f),
            });
        }
    }

    private void AddHoleNode(Node node)
    {
        AddChild(node);
        _holeNodes.Add(node);
    }

    /// <summary>Pose le drapeau d'un trou sur son drapeau du jour (il change de place chaque jour).</summary>
    private void PlaceFlag(TerrainGrid grid, CourseHole hole)
    {
        var at = hole.Hole;
        _flags[hole.Number - 1] = (_flags[hole.Number - 1].Flag, at);
        _flags[hole.Number - 1].Flag.Position = new Vector3((float)at.X, (float)grid.Sample(at.X, at.Z), (float)at.Z);
    }

    /// <summary>Oriente le fanion (pointé vers +X dans son maillage) dans le sens du vent (x, z du monde) ; sans vent, il ne bouge pas.</summary>
    public void SetWind(Vector2 direction)
    {
        if (direction.LengthSquared() > 1e-4f)
        {
            foreach (var (flag, _) in _flags)
            {
                flag.Rotation = new Vector3(0f, -Mathf.Atan2(direction.Y, direction.X), 0f);
            }
        }
    }

    /// <summary>Met à jour les instances. <paramref name="alpha"/> vaut 0 à l'instantané précédent, 1 au courant.</summary>
    public void Update(WorldSnapshot current, WorldSnapshot previous, float alpha, bool previousChanged)
    {
        if (_golfers is null || _balls is null || _grid is null)
        {
            return;
        }

        if (_grid.Course.Count != _flags.Count)
        {
            BuildHoles(_grid); // trou ajouté ou retiré en jeu
        }

        foreach (var hole in _grid.Course.Holes)
        {
            if (hole.Hole != _flags[hole.Number - 1].At)
            {
                PlaceFlag(_grid, hole);
            }
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
        var display = Lerp(previous.Tick, current.Tick, alpha);
        _frame++;
        ChooseAnimated(current, n);
        var nb = 0;
        var ns = 0;
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

            var track = Follow(cur, new Vector3((float)pos.X, (float)_grid.Sample(pos.X, pos.Z), (float)pos.Z), display, current.Tick, out var basis);
            if (track.Rig is { } rig)
            {
                rig.Transform = new Transform3D(basis, track.Shown);
                Animate(track, cur, rig, display);
            }
            else
            {
                _golfers.SetInstanceTransform(ns, new Transform3D(basis, track.Shown));
                _golfers.SetInstanceColor(ns, StateColor(cur.State, cur.Id));
                ns++;
            }

            if (cur.State != GolferState.Roaming && cur.State != GolferState.Done && !cur.Holed && !cur.PickedUp)
            {
                _balls.SetInstanceTransform(nb, new Transform3D(Basis.Identity, new Vector3((float)ball.X, (float)ball.Y + ProceduralMeshes.BallRadius, (float)ball.Z)));
                _balls.SetInstanceColor(nb, Colors.White);
                nb++;
            }
        }

        Prune();
        _golfers.VisibleInstanceCount = ns;
        _balls.VisibleInstanceCount = nb;
        VisibleGolfers = n;
        AnimatedGolfers = n - ns;
    }

    /// <summary>Les golfeurs à moins de <see cref="Range"/> du point de vue, les plus proches d'abord, au plus <see cref="PoolSize"/>.</summary>
    private void ChooseAnimated(WorldSnapshot current, int n)
    {
        _chosen.Clear();
        if (_model is null)
        {
            return;
        }

        var eye = Viewer ?? Eye(GetViewport()?.GetCamera3D());
        var range2 = Range * Range;
        _near.Clear();
        for (var i = 0; i < n; i++)
        {
            var p = current.Golfers[i].Position;
            var d2 = new Vector2((float)p.X - eye.X, (float)p.Z - eye.Z).LengthSquared() + (eye.Y * eye.Y);
            if (d2 < range2)
            {
                _near.Add((i, d2));
            }
        }

        if (_near.Count > PoolSize)
        {
            _near.Sort((a, b) => a.Distance2.CompareTo(b.Distance2));
        }

        for (var k = 0; k < Math.Min(_near.Count, PoolSize); k++)
        {
            _chosen.Add(current.Golfers[_near[k].Index].Id);
        }

        // Rend d'abord les rigs des golfeurs sortis du choix, pour que les entrants les trouvent libres.
        foreach (var (id, track) in _tracks)
        {
            if (track.Rig is not null && !_chosen.Contains(id))
            {
                Release(track);
            }
        }
    }

    /// <summary>Position de la caméra en perspective ; en vue iso (orthographique), le point qu'elle vise au sol.</summary>
    private static Vector3 Eye(Camera3D? camera)
    {
        if (camera is null)
        {
            return Vector3.Zero;
        }

        var pos = camera.GlobalPosition;
        var forward = -camera.GlobalBasis.Z;
        return camera.Projection == Camera3D.ProjectionType.Orthogonal && forward.Y < -0.01f ? pos + (forward * (-pos.Y / forward.Y)) : pos;
    }

    /// <summary>Met à jour le suivi d'un golfeur et rend sa position affichée et son orientation (regard vers +Z du modèle).</summary>
    private Track Follow(GolferView cur, Vector3 at, double display, long tick, out Basis basis)
    {
        if (!_tracks.TryGetValue(cur.Id, out var t))
        {
            t = new Track { Display = display };
            _tracks[cur.Id] = t;
        }

        t.Frame = _frame;
        if (cur.State != t.State)
        {
            t.State = cur.State;
            t.Entered = tick;
            if (cur.State is GolferState.Swinging or GolferState.Putting)
            {
                var toPin = new Vector2((float)(cur.Pin.X - cur.Ball.X), (float)(cur.Pin.Z - cur.Ball.Z)).Length();
                t.Stroke = cur.State == GolferState.Putting ? GolferModel.Putt : toPin < ChipRange ? GolferModel.Chip : GolferModel.Swing;
            }
        }

        // À l'adresse, le golfeur se tient de côté, la balle devant lui (ball du modèle) et la cible vers +X du modèle.
        var addressing = cur.State is GolferState.Swinging or GolferState.Putting or GolferState.BallInFlight or GolferState.AwaitingShot;
        var step = Math.Clamp((display - t.Display) / StanceTicks, 0.0, 1.0);
        t.Display = display;
        t.Stance = Math.Clamp(t.Stance + (addressing ? step : -step), 0.0, 1.0);
        var walked = t.Placed ? new Vector2(at.X - t.At.X, at.Z - t.At.Z) : Vector2.Zero;
        if (addressing)
        {
            var aim = new Vector2((float)(cur.Pin.X - cur.Ball.X), (float)(cur.Pin.Z - cur.Ball.Z));
            if (aim.LengthSquared() > 1e-4f)
            {
                t.Yaw = Mathf.LerpAngle(t.Yaw, Mathf.Atan2(-aim.Y, aim.X), (float)step);
            }
        }
        else if (walked.LengthSquared() > 1e-6f)
        {
            t.Yaw = Mathf.LerpAngle(t.Yaw, Mathf.Atan2(walked.X, walked.Y), (float)Math.Max(step, 0.5));
        }

        basis = new Basis(Vector3.Up, t.Yaw).Scaled(Vector3.One * ProceduralMeshes.GolferScale);
        var offset = _model is null ? Vector3.Zero : basis * _model.Ball * (float)t.Stance;
        var shown = at - new Vector3(offset.X, 0f, offset.Z);
        if (!addressing)
        {
            t.Phase += walked.Length();
        }

        t.Shown = shown;
        t.At = at;
        t.Placed = true;
        if (_chosen.Contains(cur.Id) && t.Rig is null)
        {
            t.Rig = Acquire();
        }

        return t;
    }

    /// <summary>Clip et instant selon l'état : marche calée sur la distance parcourue, geste calé sur le tick d'impact de la sim.</summary>
    private void Animate(Track t, GolferView cur, GolferRig rig, double display)
    {
        var model = _model!;
        switch (cur.State)
        {
            case GolferState.Roaming when BenchSwing && cur.Id % 2 == 1:
                rig.Pose(GolferModel.Swing, ((display * World.TickSeconds) + (cur.Id * 0.37)) % model.Length(GolferModel.Swing));
                break;
            case GolferState.Arriving or GolferState.WalkingToBall or GolferState.Exiting or GolferState.ToNextTee or GolferState.Roaming or GolferState.Leaving or GolferState.Quitting or GolferState.Visiting:
                var cycle = model.WalkCycle * ProceduralMeshes.GolferScale;
                rig.Pose(GolferModel.Walk, cycle > 0 ? t.Phase / cycle % 1.0 * model.Length(GolferModel.Walk) : 0.0);
                break;
            case GolferState.Swinging or GolferState.Putting:
                // La balle part au tick Entered + SwingTicksCount : l'image d'impact y tombe ; si la sim attend encore son plan, le geste s'arrête à l'impact.
                var impact = model.Impact(t.Stroke);
                rig.Pose(t.Stroke, Math.Clamp(impact + ((display - (t.Entered + World.SwingTicksCount)) * World.TickSeconds), 0.0, impact));
                break;
            case GolferState.BallInFlight:
                rig.Pose(t.Stroke, Math.Clamp(model.Impact(t.Stroke) + ((display - t.Entered) * World.TickSeconds), 0.0, model.Length(t.Stroke)));
                break;
            default:
                rig.Pose(GolferModel.Idle, ((display * World.TickSeconds) + (cur.Id * 0.53)) % model.Length(GolferModel.Idle));
                break;
        }
    }

    private GolferRig Acquire()
    {
        GolferRig rig;
        if (_free.Count > 0)
        {
            rig = _free[^1];
            _free.RemoveAt(_free.Count - 1);
        }
        else
        {
            rig = GolferRig.New(_model!, _rigs++);
            AddChild(rig);
        }

        rig.Visible = true;
        return rig;
    }

    private void Release(Track track)
    {
        track.Rig!.Visible = false;
        _free.Add(track.Rig);
        track.Rig = null;
    }

    /// <summary>Oublie les golfeurs absents de l'instantané (partis, mode charge coupé) et rend leurs rigs.</summary>
    private void Prune()
    {
        _stale.Clear();
        foreach (var (id, track) in _tracks)
        {
            if (track.Frame != _frame)
            {
                _stale.Add(id);
                if (track.Rig is not null)
                {
                    Release(track);
                }
            }
        }

        foreach (var id in _stale)
        {
            _tracks.Remove(id);
        }
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
