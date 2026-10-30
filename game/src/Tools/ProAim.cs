using Godot;
using Golferoo.Sim;
using Golferoo.Sim.Golfers;
using Golferoo.Sim.Pro;
using Golferoo.Sim.Shots;
using Golferoo.Sim.Terrain;

namespace Golferoo.Game.Tools;

/// <summary>
/// Mode visée de votre pro (C08) : quand il attend son coup (<see cref="World.AwaitingPro"/>), le jeu se met en pause, la caméra
/// passe derrière lui et un panneau propose le club, la trajectoire et la force ; un clic au sol pose le point visé ; Entrée ou
/// « Jouer » envoie <see cref="World.PlayProShot"/>. La trajectoire prévue vient de <see cref="World.PreviewProShot"/> (vent
/// compris) et l'ellipse montre la dispersion du pro à 1,5 écart type. Aucune règle ici.
/// </summary>
public partial class ProAim : Node3D
{
    private const float EllipseSigmas = 1.5f;
    private const int EllipseSegments = 40;

    private static readonly Club[] FullClubs = [.. Clubs.Full];
    private static readonly string[] ClubNames = ["Driver", "Bois", "Fer long", "Fer moyen", "Fer court", "Wedge"];
    private static readonly ShotShape[] Shapes = [ShotShape.Straight, ShotShape.Draw, ShotShape.Fade, ShotShape.Chip];
    private static readonly string[] ShapeNames = ["Droit", "Draw", "Fade", "Chip"];

    private Main? _main;
    private Ui.UiPanel? _panel;
    private Label? _label;
    private HBoxContainer? _clubRow;
    private HBoxContainer? _shapeRow;
    private HSlider? _force;
    private ImmediateMesh? _mesh;
    private bool _dirty;

    public bool Active { get; private set; }

    public Club Club { get; private set; } = Clubs.Driver;

    public ShotShape Shape { get; private set; } = ShotShape.Straight;

    public Point2 Target { get; private set; }

    public double Force => (_force?.Value ?? 100.0) / 100.0;

    /// <summary>Dernière trajectoire prévue (vide hors visée).</summary>
    public ShotResult Preview { get; private set; }

    public void Init(Main main)
    {
        _main = main;
        _mesh = new ImmediateMesh();
        AddChild(new MeshInstance3D
        {
            Name = "AimLines",
            Mesh = _mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                NoDepthTest = true,
                RenderPriority = 10,
            },
        });

        // Hors de la pile : la visée s'ouvre et se ferme d'elle-même avec le tour du pro ; en haut à droite, loin de la pile du bas.
        _panel = Ui.UiPanel.Create(Ui.UiIds.Panel.Aim, "Visée de votre pro", "figurine", 600f);
        _panel.Visible = false;
        _panel.CloseButton.Visible = false;
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        _panel.GrowHorizontal = Control.GrowDirection.Begin;
        (_panel.OffsetTop, _panel.OffsetRight) = (16f, -16f);
        var box = _panel.Content;
        _label = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(560f, 0f) };
        box.AddChild(_label);
        _clubRow = new HBoxContainer();
        for (var i = 0; i < FullClubs.Length; i++)
        {
            var club = FullClubs[i];
            var button = new Button { Text = ClubNames[i], ToggleMode = true };
            button.Pressed += () => SetClub(club);
            _clubRow.AddChild(button);
        }

        box.AddChild(_clubRow);
        _shapeRow = new HBoxContainer();
        for (var i = 0; i < Shapes.Length; i++)
        {
            var shape = Shapes[i];
            var button = new Button { Text = ShapeNames[i], ToggleMode = true };
            button.Pressed += () => SetShape(shape);
            _shapeRow.AddChild(button);
        }

        box.AddChild(_shapeRow);
        var forceRow = new HBoxContainer();
        forceRow.AddChild(new Label { Text = "Force" });
        _force = new HSlider { MinValue = 10, MaxValue = 100, Step = 1, Value = 100, CustomMinimumSize = new Vector2(260, 0) };
        _force.ValueChanged += _ => _dirty = true;
        forceRow.AddChild(_force);
        var play = new Button { Text = "Jouer" };
        play.Pressed += Play;
        forceRow.AddChild(play);
        box.AddChild(forceRow);
        main.Ui.AddChild(_panel);
    }

    public override void _Process(double delta) => Sync();

    /// <summary>Ouvre la visée quand le pro attend, la ferme sinon, et redessine si un réglage a changé.</summary>
    public void Sync()
    {
        if (_main is null)
        {
            return;
        }

        // Un coup envoyé attend son tick : sans ce garde, la visée se rouvrait aussitôt sur la cible par défaut et remettait la pause.
        var pro = _main.World.ProShotPending ? null : _main.World.AwaitingPro;
        if (pro is not null && !Active)
        {
            Begin(pro);
        }
        else if (pro is null && Active)
        {
            End();
        }

        if (Active && _dirty)
        {
            Refresh();
        }
    }

    public void SetClub(Club club)
    {
        Club = club;
        if (club.IsPutter)
        {
            Shape = ShotShape.Straight;
        }

        _dirty = true;
    }

    public void SetShape(ShotShape shape)
    {
        Shape = Club.IsPutter ? ShotShape.Straight : shape;
        _dirty = true;
    }

    public void SetTarget(Point2 target)
    {
        Target = target;
        _dirty = true;
    }

    public void SetForce(double force)
    {
        _force!.Value = Math.Clamp(force, 0.1, 1.0) * 100.0;
        _dirty = true;
    }

    /// <summary>Gère une touche pendant la visée ; vrai si elle lui appartient.</summary>
    public bool HandleKey(Key key)
    {
        if (!Active || key is not (Key.Enter or Key.KpEnter))
        {
            return false;
        }

        Play();
        return true;
    }

    /// <summary>Un clic au sol pendant la visée pose le point visé ; vrai si le clic lui appartient.</summary>
    public bool HandleClick(Vector2 mouse)
    {
        var camera = _main?.Camera.View;
        if (!Active || camera is null)
        {
            return false;
        }

        if (Brush.TryPick(_main!.World.Terrain!, camera.ProjectRayOrigin(mouse), camera.ProjectRayNormal(mouse), camera.Far, out var hit))
        {
            SetTarget(new Point2(hit.X, hit.Z));
        }

        return true;
    }

    public void Play()
    {
        if (!Active)
        {
            return;
        }

        _main!.World.PlayProShot(new ProShot(Club, Target, Shape, Force));
        _main.Paused = false;
        End();
    }

    /// <summary>Le pro attend : pause, club et cible par défaut (le trou, avec le club dont la portée le couvre), caméra derrière lui.</summary>
    private void Begin(Golfer pro)
    {
        var ball = pro.Ball;
        Active = true;
        _main!.Paused = true;
        var terrain = _main.World.Terrain!;
        Target = _main.World.HoleOf(pro).Hole;
        var d = Dist(ball, Target);
        Club = terrain.SurfaceAt(ball.X, ball.Z) == Surface.Green
            ? Clubs.Putter
            : FullClubs.LastOrDefault(c => c.TargetRange >= d) ?? Clubs.Driver;
        Shape = ShotShape.Straight;
        _force!.Value = Club.IsPutter ? 100 : Math.Min(1.0, Math.Sqrt(d / Club.TargetRange)) * 100.0;

        // Caméra derrière le pro : le haut de l'écran regarde vers le trou.
        var yaw = Mathf.RadToDeg(Mathf.Atan2((float)(ball.X - Target.X), (float)(ball.Z - Target.Z)));
        var mid = new Vector3((float)(ball.X + Target.X) / 2f, 0f, (float)(ball.Z + Target.Z) / 2f);
        _main.Camera.SetView(yaw, Math.Clamp((float)d * 0.9f, 40f, 250f), mid);
        _panel!.Visible = true;
        _dirty = true;
    }

    private void End()
    {
        Active = false;
        _panel!.Visible = false;
        _mesh!.ClearSurfaces();
        Preview = default;
    }

    private void Refresh()
    {
        _dirty = false;
        var world = _main!.World;
        var pro = world.AwaitingPro;
        if (pro is null)
        {
            return;
        }

        var putting = world.Terrain!.SurfaceAt(pro.Ball.X, pro.Ball.Z) == Surface.Green;
        if (putting != Club.IsPutter)
        {
            Club = putting ? Clubs.Putter : Clubs.Wedge;
            Shape = ShotShape.Straight;
        }

        Preview = world.PreviewProShot(new ProShot(Club, Target, Shape, Force));
        for (var i = 0; i < FullClubs.Length; i++)
        {
            ((Button)_clubRow!.GetChild(i)).SetPressedNoSignal(FullClubs[i] == Club);
        }

        for (var i = 0; i < Shapes.Length; i++)
        {
            ((Button)_shapeRow!.GetChild(i)).SetPressedNoSignal(Shapes[i] == Shape);
        }

        _clubRow!.Visible = !putting;
        _shapeRow!.Visible = !putting;
        _force!.Editable = !putting;
        var skills = world.Pro!;
        var general = ProSkills.OfClub(Club);
        _label!.Text = $"Votre pro : {(putting ? "putting" : $"{ClubName(Club)}, {ShapeNames[Array.IndexOf(Shapes, Shape)].ToLowerInvariant()}")} | " +
                       $"cible à {Dist(pro.Ball, Target):F0} m, balle prévue à {Dist(pro.Ball, Preview.Ball):F0} m ({OutcomeName(Preview.Outcome)})\n" +
                       $"{SkillName(general)} : niveau {skills.Level(general)} | clic au sol : viser\n" +
                       ReadingNote(skills, putting, world.Progression);
        Draw(pro.Ball, Preview, skills.DirectionFactor(Club, Shape));
    }

    /// <summary>Trajectoire prévue (jaune) et ellipse de dispersion au point d'arrivée (blanche).</summary>
    private void Draw(Point2 ball, ShotResult preview, double dispersion)
    {
        var terrain = _main!.World.Terrain!;
        _mesh!.ClearSurfaces();
        if (preview.Trajectory is { Count: > 1 } path)
        {
            _mesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
            _mesh.SurfaceSetColor(new Color(1f, 0.85f, 0.2f));
            foreach (var p in path)
            {
                _mesh.SurfaceAddVertex(new Vector3((float)p.X, (float)p.Y + 0.15f, (float)p.Z));
            }

            _mesh.SurfaceEnd();
        }

        var dist = Dist(ball, preview.Ball);
        if (dist < 0.5)
        {
            return;
        }

        var ux = (preview.Ball.X - ball.X) / dist;
        var uz = (preview.Ball.Z - ball.Z) / dist;
        var across = EllipseSigmas * dist * Club.DirectionSigma * dispersion;
        var along = EllipseSigmas * dist * Club.SpeedSigma * dispersion * 2.0;
        _mesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
        _mesh.SurfaceSetColor(Colors.White);
        for (var i = 0; i <= EllipseSegments; i++)
        {
            var a = i * Math.Tau / EllipseSegments;
            var (c, s) = (Math.Cos(a) * along, Math.Sin(a) * across);
            var x = preview.Ball.X + (ux * c) - (uz * s);
            var z = preview.Ball.Z + (uz * c) + (ux * s);
            _mesh.SurfaceAddVertex(new Vector3((float)x, (float)terrain.Sample(x, z) + 0.2f, (float)z));
        }

        _mesh.SurfaceEnd();
    }

    /// <summary>Ce que l'aperçu vaut (C25) : le vent ou la pente sont lus avec une erreur qui s'efface avec le niveau de l'habileté.</summary>
    private static string ReadingNote(ProSkills skills, bool putting, ProgressionParams p)
    {
        var skill = putting ? ProSkill.GreenReading : ProSkill.Wind;
        var level = skills.Level(skill);
        if (level >= ProSkills.MaxLevel)
        {
            return $"{ProEffects.Name(skill)} : niveau {level}, l'aperçu est juste";
        }

        var amplitude = ProSkills.BiasAmplitude(level);
        var degrees = amplitude * (putting ? p.SlopeBiasAngle : p.WindBiasAngle) * 180.0 / Math.PI;
        var share = amplitude * (putting ? p.SlopeBiasScale : p.WindBiasForce) * 100.0;
        var error = putting ? $"pente lue à ±{share:F0} % et ±{degrees:F0}°" : $"vent lu à ±{share:F0} % de force et ±{degrees:F0}°";
        return $"{ProEffects.Name(skill)} : niveau {level}, écart possible de l'aperçu : {error}";
    }

    private static string ClubName(Club club) => club.IsPutter ? "Putter" : ClubNames[Array.IndexOf(FullClubs, club)];

    private static string SkillName(ProSkill skill) => skill switch
    {
        ProSkill.Driver => "Driver",
        ProSkill.Wood => "Bois",
        ProSkill.LongIrons => "Fers longs",
        ProSkill.MidIrons => "Fers moyens",
        ProSkill.ShortIrons => "Fers courts",
        ProSkill.Wedge => "Wedge",
        _ => "Putter",
    };

    private static string OutcomeName(ShotOutcome outcome) => outcome switch
    {
        ShotOutcome.Holed => "dans le trou",
        ShotOutcome.Green => "green",
        ShotOutcome.Fairway => "fairway",
        ShotOutcome.Rough => "rough",
        ShotOutcome.Bunker => "bunker",
        ShotOutcome.Trees => "arbres",
        ShotOutcome.Water => "eau",
        ShotOutcome.OutOfBounds => "hors limites",
        _ => outcome.ToString(),
    };

    private static double Dist(Point2 a, Point2 b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));
}
